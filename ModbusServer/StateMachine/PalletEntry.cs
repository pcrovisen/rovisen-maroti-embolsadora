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
    internal class PalletEntry : Machine
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

        public PalletEntry() : base(States.Waiting)
        {
            qrReader = new QrReader(ConfigurationManager.AppSettings["ipQrReader"]);
            qrReadCode = new QrReadMachine(qrReader);
            qrReaderConnection = new QrReaderConnection(qrReader);
            bocedi1Working = false;
            bocedi2Working = false;
        }

        public override void Step()
        {
            qrReaderConnection.Step();

            NotifyBocediStates();
            if (FatekPLC.ReadBit(FatekPLC.Signals.WaitingPallet) && 
                (States)State != States.Waiting &&
                (States)State != States.UpdateFIFO1 &&
                (States)State != States.UpdateCar
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
                            Log.InfoFormat("Utilizando receta por defecto #{0}", Config.Instance.DefaultRecipe);
                        }
                        else
                        {
                            qrReadCode.Reset();
                            NextState(States.ReadingQrInError);
                        }
                    }
                    break;
                case States.WaitingSetQr:
                    if (qrTask.IsCompleted)
                    {
                        if (qrTask.Result)
                        {
                            NextState(States.WaitingSetEntryPallet);
                            qrTask = Status.SetEntryPallet();
                            Log.Info("Set entry QR");
                        }
                        else
                        {
                            qrTask = SetReadedQR(qrReadCode.Result);
                            Log.Error("Could not set the QR");
                        }
                    }
                    break;
                case States.WaitingSetEntryPallet:
                    if (qrTask.IsFaulted)
                    {
                        qrTask = Status.SetEntryPallet();
                        Log.ErrorFormat("Could not set entry pallet. Error: {0}", qrTask.Exception);
                    }
                    if (qrTask.IsCompleted)
                    {
                        if(qrTask.Result)
                        {
                            Log.Info("Waiting availability");
                            NextState(States.WaitingAvailability);
                        }
                        else
                        {
                            qrTask = Status.SetEntryPallet();
                            Log.Error("Could not set entry pallet");
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
                    if (sqlRequest.IsCompleted)
                    {
                        if (sqlRequest.Result != null)
                        {
                            if (sqlRequest.Result.Packager == 0)
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
                                Log.InfoFormat("Received packager: {0}, recipe: {1}, injector:{2}", sqlRequest.Result.Packager, sqlRequest.Result.Recipe, sqlRequest.Result.Injector);
                                SetPackagerAndRecipe(sqlRequest.Result);
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
                    if (writeTask.IsFaulted)
                    {
                        Log.Error("Could not write fifo 1");
                        writeTask = Status.UpdateFIFO1();
                    }
                    if (writeTask.IsCompleted)
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
                    if (writeTask.IsFaulted)
                    {
                        Log.Error("Could not write car");
                        writeTask = Status.SetCarPallet(true);
                    }
                    if (writeTask.IsCompleted)
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
            if (result.Packager == 1)
            {
                FatekPLC.SetBit(FatekPLC.Signals.ToEmb1);
                FatekPLC.ResetBit(FatekPLC.Signals.ToEmb2);
                var injector = VisualID.GetId(result.Injector);
                var labelAndId = !result.Labeling ? 8 + currentIdEmb1 : currentIdEmb1;
                var injRecipeId = string.Format("{0}{1}{2}",injector.ToString("X"), result.Recipe.ToString("X"), labelAndId.ToString("X"));
                FatekPLC.SetMemory(FatekPLC.Memory.ID, Convert.ToInt16(injRecipeId, 16));
            }
            else
            {
                FatekPLC.SetBit(FatekPLC.Signals.ToEmb2);
                FatekPLC.ResetBit(FatekPLC.Signals.ToEmb1);
                var injector = VisualID.GetId(result.Injector);
                var labelAndId = !result.Labeling ? 8 + currentIdEmb2 : currentIdEmb2;
                var injRecipeId = string.Format("{0}{1}{2}", injector.ToString("X"), result.Recipe.ToString("X"), labelAndId.ToString("X"));
                FatekPLC.SetMemory(FatekPLC.Memory.ID, Convert.ToInt16(injRecipeId, 16));
            }
        }

        public override void Reset()
        {
            base.Reset();
            Log.Info("Waiting new pallet");
            if (FatekPLC.ReadMemory(FatekPLC.Memory.FIFO2Len) != 0)
            {
                currentIdEmb1 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO21) + 1;
            }
            currentIdEmb1 %= 8;
            if(currentIdEmb1 == 0)
            {
                currentIdEmb1 = 1;
            }

            if (FatekPLC.ReadMemory(FatekPLC.Memory.FIFO4Len) != 0)
            {
                currentIdEmb2 = FatekPLC.ReadMemory(FatekPLC.Memory.FIFO41) + 1;
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
            if(FatekPLC.ReadBit(FatekPLC.Signals.BCD1OK) ^ bocedi1Working)
            {
                bocedi1Working = FatekPLC.ReadBit(FatekPLC.Signals.BCD1OK);
                if(bocedi1Working)
                {
                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.embolsadora_funcionando, packager: 1);
                }
                else
                {
                    _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.embolsadora_detenida, packager: 1);
                }
            }

            if (FatekPLC.ReadBit(FatekPLC.Signals.BCD2OK) ^ bocedi2Working)
            {
                bocedi2Working = FatekPLC.ReadBit(FatekPLC.Signals.BCD2OK);
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
