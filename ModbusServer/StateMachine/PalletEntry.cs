using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using ModbusServer.Data;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    internal class PalletEntry : Machine<PalletEntry.States>
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Waiting,
            ReadingQR,
            WaitingSetQr,
            WaitingSetEntryPallet,
            WaitingAvailability,
            DefaultBehavior,
            AskingDB,
            SendingID,
            WaitForBocedi1,
            WaitEnterBocedi,
            WaitUpdateFIFO1,
            UpdateFIFO1,
            WaitForCar,
            WaitEnterCar,
            WaitUpdateCar,
            UpdateCar,
            ReadingQrInError,
            Paused,
        }

        readonly QrReader qrReader;
        readonly QrReadMachine qrReadCode;
        readonly QrReaderConnection qrReaderConnection;

        bool bocedi1Working;
        bool bocedi2Working;
        Task<SqlDatabase.PackagerPreference> sqlRequest;
        Task<bool> qrTask;
        Task writeTask;
        int currentIdEmb1 = 0;
        int currentIdEmb2 = 0;

        // Absent on purpose: Waiting (idle between pallets), WaitingAvailability
        // (both Bocedis legitimately full), Paused and ReadingQrInError (waiting
        // for an operator), and the four WaitFor*/WaitEnter* handshakes, whose
        // duration is conveyor movement and depends on how the line is running.
        static readonly IReadOnlyDictionary<States, int> Timeouts = new Dictionary<States, int>
        {
            { States.WaitingSetQr, 60000 },
            { States.WaitingSetEntryPallet, 60000 },
            { States.AskingDB, 120000 },
            { States.SendingID, 60000 },
            { States.WaitUpdateFIFO1, 60000 },
            { States.UpdateFIFO1, 60000 },
            { States.WaitUpdateCar, 60000 },
            { States.UpdateCar, 60000 },
        };

        protected override IReadOnlyDictionary<States, int> StateTimeouts
        {
            get { return Timeouts; }
        }

        protected override void OnStuck(States state)
        {
            Status.Instance.ErrorMessages.EntryError =
                $"El ingreso de pallets se detuvo en la etapa {state}. Revisar la conexión con el PLC y con la base de datos.";
        }

        public PalletEntry() : base(States.Waiting)
        {
            qrReader = new QrReader(ConfigurationManager.AppSettings["ipQrReader"]);
            qrReadCode = new QrReadMachine(qrReader);
            qrReaderConnection = new QrReaderConnection(qrReader);
            bocedi1Working = false;
            bocedi2Working = false;
        }

        protected override void OnStep()
        {
            qrReaderConnection.Step();

            NotifyBocediStates();
            if (FatekPLC.ReadBit(FatekPLC.Signals.WaitingPallet) && 
                State != States.Waiting &&
                State != States.UpdateFIFO1 &&
                State != States.UpdateCar
                )
            {
                Log.Info("Skip to waiting");
                NextState(States.Waiting);
            }
            switch (State)
            {
                case States.Waiting:
                    Status.ResetEntryPallet();
                    FatekPLC.ResetBit(FatekPLC.Signals.SendingQR);
                    FatekPLC.ResetBit(FatekPLC.Signals.ReadingPallet);
                    FatekPLC.ResetBit(FatekPLC.Signals.ErrorQr);
                    FatekPLC.SetBit(FatekPLC.Signals.Waiting);
                    Status.Instance.ErrorMessages.EntryError = "";
                    if (FatekPLC.ReadBit(FatekPLC.Signals.Pause))
                    {
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.sistema_detenido);
                        Log.Info("System on pause");
                        NextState(States.Paused);
                        break;
                    }
                    if (FatekPLC.ReadBit(FatekPLC.Signals.ReadQR))
                    {
                        NextState(States.ReadingQR);
                        Log.Info("Pallet arrived.");
                        qrReadCode.Reset();
                        FatekPLC.ResetBit(FatekPLC.Signals.ConfirmUpdate);
                        break;
                    }
                    break;
                case States.ReadingQR:
                    FatekPLC.ResetBit(FatekPLC.Signals.Waiting);
                    FatekPLC.SetBit(FatekPLC.Signals.ReadingPallet);
                    qrReadCode.Step();
                    if (qrReadCode.Completed)
                    {
                        Log.InfoFormat("Code {0} readed", qrReadCode.Result);
                        qrTask = SetReadedQR(qrReadCode.Result);
                        NextState(States.WaitingSetQr);
                    }
                    if (qrReadCode.Failed)
                    {
                        Log.Warn("QR code not found");
                        Status.Instance.ErrorMessages.EntryError = "No se pudo encontrar QR. Pegar un código al pallet o estirar y centrar el código existente.";
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.qr_no_detectado);
                        if (Config.Instance.ContinueIfNoQr)
                        {
                            NextState(States.DefaultBehavior);
                        }
                        else
                        {
                            qrReadCode.Reset();
                            NextState(States.ReadingQrInError);
                        }
                    }
                    break;
                case States.WaitingSetQr:
                    if (TryComplete(ref qrTask, () => SetReadedQR(qrReadCode.Result), "Writing the entry QR", out bool qrWritten))
                    {
                        if (qrWritten)
                        {
                            Log.Info("Set entry QR");
                            qrTask = Status.SetEntryPallet();
                            NextState(States.WaitingSetEntryPallet);
                        }
                        else if (StateTime.ElapsedMilliseconds > RetryDelayMs)
                        {
                            // The QR has no id in the database yet. Back off instead
                            // of asking again on every step.
                            Log.Error("Could not set the QR. Retrying");
                            qrTask = SetReadedQR(qrReadCode.Result);
                            StateTime.Restart();
                        }
                    }
                    break;
                case States.WaitingSetEntryPallet:
                    if (TryComplete(ref qrTask, () => Status.SetEntryPallet(), "Reading back the entry pallet", out bool entrySet))
                    {
                        if (entrySet)
                        {
                            Log.Info("Waiting availability");
                            NextState(States.WaitingAvailability);
                        }
                        else if (StateTime.ElapsedMilliseconds > RetryDelayMs)
                        {
                            Log.Error("Could not set entry pallet. Retrying");
                            qrTask = Status.SetEntryPallet();
                            StateTime.Restart();
                        }
                    }
                    break;
                case States.WaitingAvailability:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.bcd1Avaliable) || FatekPLC.ReadBit(FatekPLC.Signals.bcd2Avaliable))
                    {
                        NextState(States.AskingDB);
                        Log.InfoFormat("Asking db for code {0}", qrReadCode.Result);
                        sqlRequest = SqlDatabase.AskForPackager(qrReadCode.Result);
                    }
                    break;
                case States.AskingDB:
                    Status.Instance.ErrorMessages.EntryError = "";
                    if (TryComplete(ref sqlRequest, () => SqlDatabase.AskForPackager(qrReadCode.Result),
                            "Asking the database for the packager", out var preference))
                    {
                        if (preference != null)
                        {
                            if (preference.Packager == 0)
                            {
                                if(StateTime.ElapsedMilliseconds > 1000)
                                {
                                    NextState(States.AskingDB);
                                    Log.InfoFormat("Get packager == 0, asking db for code {0}", qrReadCode.Result);
                                    sqlRequest = SqlDatabase.AskForPackager(qrReadCode.Result);
                                }
                            }
                            else
                            {
                                Log.InfoFormat("Received packager: {0}, recipe: {1}, injector:{2}", preference.Packager, preference.Recipe, preference.Injector);
                                SetPackagerAndRecipe(preference);
                                NextState(States.SendingID);
                            }
                        }
                        else
                        {
                            Status.Instance.ErrorMessages.EntryError = "La base de datos responde ID inválido. Remover el pallet o cambiar el código QR.";
                            if (StateTime.ElapsedMilliseconds > 1000)
                            {
                                Log.InfoFormat("Get NULL from the database to the qrcode {0}. Reading QR again", qrReadCode.Result);
                                NextState(States.ReadingQR);
                                qrReadCode.Reset();
                            }
                        }
                    }
                    break;
                case States.SendingID:
                    Status.Instance.ErrorMessages.EntryError = "";
                    FatekPLC.SetBit(FatekPLC.Signals.SendingQR);
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.ReadQR))
                    {
                        FatekPLC.ResetBit(FatekPLC.Signals.SendingQR);
                        _ = Status.SetEntryPallet(true);
                        if (FatekPLC.ReadBit(FatekPLC.Signals.ToEmb1))
                        {
                            Log.InfoFormat("Waiting Bocedi1 to accept pallet code {0}", qrReadCode.Result);
                            NextState(States.WaitForBocedi1);
                        }
                        else
                        {
                            Log.Info("Waiting for car");
                            NextState(States.WaitForCar);
                        }                     
                    }
                    break;
                case States.WaitForBocedi1:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.WaitBocedi))
                    {
                        Log.InfoFormat("Waiting for pallet {0} to enter Bocedi1", qrReadCode.Result);
                        NextState(States.WaitEnterBocedi);
                    }
                    break;
                case States.WaitEnterBocedi:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.SendUpdate))
                    {
                        currentIdEmb1 = (currentIdEmb1 + 1) % 8;
                        if (currentIdEmb1 == 0)
                        {
                            currentIdEmb1 = 1;
                        }
                        FatekPLC.SetBit(FatekPLC.Signals.ConfirmUpdate);
                        NextState(States.WaitUpdateFIFO1);
                        writeTask = Status.UpdateFIFO1();
                    }
                    break;
                case States.WaitUpdateFIFO1:
                    if (TryComplete(ref writeTask, Status.UpdateFIFO1, "Re-reading FIFO 1"))
                    {
                        Log.Info("Fifo updated");
                        NextState(States.UpdateFIFO1);
                    }
                    break;
                case States.UpdateFIFO1:
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.SendUpdate))
                    {
                        Log.InfoFormat("Pallet {0} enter Bocedi1 with ID {1}", qrReadCode.Result, currentIdEmb1);
                        Log.Info("Waiting new pallet");
                        NextState(States.Waiting);
                        _ = SqlDatabase.NotifyPalletIn(qrReadCode.Result, 1);
                    }
                    break;
                case States.WaitForCar:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.WaitCar))
                    {
                        Log.InfoFormat("Waiting for pallet {0} to enter car", qrReadCode.Result);
                        NextState(States.WaitEnterCar);
                    }
                    break;
                case States.WaitEnterCar:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.SendUpdate))
                    {
                        Log.InfoFormat("Pallet {0} enter to the car", qrReadCode.Result);
                        writeTask = Status.SetCarPallet(true);
                        NextState(States.WaitUpdateCar);
                    }
                    if (FatekPLC.ReadBit(FatekPLC.Signals.CarEntryError))
                    {
                        Status.Instance.ErrorMessages.EntryError = "El pallet no pudo ingresar al carro. Volver a posicionar el pallet en la estación de lectura y presionar el botón Start. Asegurar que el carro no se alejó del conveyor.";
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.error_entrega_a_carro, code: qrReadCode.Result);
                        NextState(States.Paused);
                    }
                    break;
                case States.WaitUpdateCar:
                    if (TryComplete(ref writeTask, () => Status.SetCarPallet(true), "Writing the car pallet"))
                    {
                        Log.Info("Fifo updated");
                        NextState(States.UpdateCar);
                        currentIdEmb2 = (currentIdEmb2 + 1) % 8;
                        if (currentIdEmb2 == 0)
                        {
                            currentIdEmb2 = 1;
                        }
                    }
                    break;
                case States.UpdateCar:
                    FatekPLC.SetBit(FatekPLC.Signals.ConfirmUpdate);
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.SendUpdate))
                    {
                        Log.Info("Waiting new pallet");
                        NextState(States.Waiting);
                    }
                    break;
                case States.DefaultBehavior:
                    // Entry without a QR was never implemented: the DB routing and the
                    // PLC queues both require a pallet code, so there is nothing valid
                    // to send. Fall back to the same retry loop as ContinueIfNoQr=false.
                    Log.Warn("ContinueIfNoQr is enabled but entry without QR is not implemented. Retrying QR read");
                    qrReadCode.Reset();
                    NextState(States.ReadingQrInError);
                    break;
                case States.ReadingQrInError:
                    FatekPLC.SetBit(FatekPLC.Signals.ErrorQr);
                    qrReadCode.Step();
                    if (qrReadCode.Completed)
                    {
                        Log.InfoFormat("Code {0} readed", qrReadCode.Result);
                        FatekPLC.ResetBit(FatekPLC.Signals.ErrorQr);
                        qrTask = SetReadedQR(qrReadCode.Result);
                        NextState(States.WaitingSetQr);              
                        break;
                    }
                    if (qrReadCode.Failed)
                    {
                        Log.Warn("QR code not found");
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.qr_no_detectado);
                        qrReadCode.Reset();
                        break;
                    }
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.ReadQR))
                    {
                        NextState(States.Waiting);
                        break;
                    }
                    break;
                case States.Paused:
                    if (!FatekPLC.ReadBit(FatekPLC.Signals.Pause))
                    {
                        Log.Info("System running");
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.sistema_en_funcionamiento);
                        NextState(States.Waiting);
                    }
                    break;
            }
        }

        public void SetPackagerAndRecipe(SqlDatabase.PackagerPreference result)
        {
            var toBocedi1 = result.Packager == 1;
            FatekPLC.SetBit(toBocedi1 ? FatekPLC.Signals.ToEmb1 : FatekPLC.Signals.ToEmb2);
            FatekPLC.ResetBit(toBocedi1 ? FatekPLC.Signals.ToEmb2 : FatekPLC.Signals.ToEmb1);

            // PackagerPreference.Labeling comes from @out_omitir_proceso_etiquetado
            // and means *omit* labeling, while the label flag in the ID word means
            // *do* label. The negation is correct — see docs/ARCHITECTURE.md.
            var queueId = toBocedi1 ? currentIdEmb1 : currentIdEmb2;
            var labelAndId = !result.Labeling ? 8 + queueId : queueId;

            FatekPLC.SetMemory(FatekPLC.Memory.ID,
                FatekPLC.PackId(VisualID.GetId(result.Injector), result.Recipe, labelAndId));
        }

        public override void Reset()
        {
            base.Reset();
            Log.Info("Waiting new pallet");
            // Ids are assigned incrementally, so the next id follows the one of the
            // newest pallet, which sits at the tail of the queue (index len - 1),
            // not at the head. The %8 strips the label bit and the injector/recipe
            // nibbles from the ID word (both are multiples of 8).
            var len1 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO2Len);
            if (len1 != 0)
            {
                currentIdEmb1 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO21 + (len1 - 1)) + 1;
            }
            currentIdEmb1 %= 8;
            if(currentIdEmb1 == 0)
            {
                currentIdEmb1 = 1;
            }

            var len2 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO4Len);
            if (len2 != 0)
            {
                currentIdEmb2 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO41 + (len2 - 1)) + 1;
            }
            currentIdEmb2 %= 8;

            if(currentIdEmb2 == 0)
            {
                currentIdEmb2 =1;
            }
        }

        public async Task<bool> SetReadedQR(string value)
        {
            return await FatekPLC.SetQr(FatekPLC.Memory.QR1, value);
        }

        public void NotifyBocediStates()
        {
            bool currentBCD1Status = FatekPLC.ReadBit(FatekPLC.Signals.BCD1OK);
            if(currentBCD1Status ^ bocedi1Working)
            {
                bocedi1Working = currentBCD1Status;
                if(bocedi1Working)
                {
                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.embolsadora_funcionando, packager: 1);
                }
                else
                {
                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.embolsadora_detenida, packager: 1);
                }
            }

            bool currentBCD2Status = FatekPLC.ReadBit(FatekPLC.Signals.BCD2OK);
            if (currentBCD2Status ^ bocedi2Working)
            {
                bocedi2Working = currentBCD2Status;
                if (bocedi2Working)
                {
                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.embolsadora_funcionando, packager: 2);
                }
                else
                {
                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.embolsadora_detenida, packager: 2);
                }
            }
        }
    }
}
