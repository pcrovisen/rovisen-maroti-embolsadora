using log4net;
using ModbusServer.Data;
using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace ModbusServer.Devices
{
    public class FatekPLC
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static FatekPLC Instance { get; private set; }
        EasyModbus.ModbusServer modbusServer;
        bool verbose = false;
        Stopwatch sw;

        public static bool IsConnected => Instance.sw.ElapsedMilliseconds < 5000 && Instance.modbusServer.NumberOfConnections > 0;

        public enum Memory
        {
            QR1 = 1,
            QR2,
            ID,
            Recipe,
            FIFO11a = 10,
            FIFO11b,
            FIFO12a,
            FIFO12b,
            FIFO13a,
            FIFO13b,
            FIFO14a,
            FIFO14b,
            FIFO15a,
            FIFO15b,
            FIFO16a,
            FIFO16b,
            FIFO1Len,
            FIFO21,
            FIFO22,
            FIFO23,
            FIFO24,
            FIFO25,
            FIFO26,
            FIFO2Len,
            LABEL1a,
            LABEL1b,
            LABEL1Id,
            EXIT1a,
            EXIT1b,
            EXIT1Id,
            FIFO31a,
            FIFO31b,
            FIFO32a,
            FIFO32b,
            FIFO33a,
            FIFO33b,
            FIFO34a,
            FIFO34b,
            FIFO35a,
            FIFO35b,
            FIFO36a,
            FIFO36b,
            FIFO3Len,
            FIFO41,
            FIFO42,
            FIFO43,
            FIFO44,
            FIFO45,
            FIFO46,
            FIFO4Len,
            LABEL2a,
            LABEL2b,
            LABEL2Id,
            EXIT2a,
            EXIT2b,
            EXIT2Id,
            CARQRa,
            CARQRb,
            CARID,
            CARRECIPE,
            DEL1a = 70,
            DEL1b,
            DEL1ID,
            DEL1Pos1,
            DEL1Pos2,
            DEL2a,
            DEL2b,
            DEL2ID,
            DEL2Pos1,
            DEL2Pos2,
        }

        public enum Signals
        {
            ReadingPallet = 1,
            SendingQR,
            Labeling1,
            Labeling2,
            ToEmb1,
            ToEmb2,
            ReceivingFIFOs,
            Alive,
            DelEmb1,
            DelEmb2,
            ConfirmUpdate,
            ConfirmUpdate2,
            WeightOk1,
            WeightOk2,
            PalletLeave1,
            PalletLeave2,
            ErrorQr,
            Waiting,
            ElevatorAuth,
            ElevatorFailedQr,
            ReadQR = 21,
            Label1,
            Label2,
            SendingFIFOs,
            Ready,
            Del1Valid,
            Del1Error,
            Del2Valid,
            Del2Error,
            SendUpdate,
            SendUpdate2,
            CarWithPallet,
            CarInB1,
            CarInB2,
            bcd1Avaliable,
            bcd2Avaliable,
            Leave1,
            Leave2,
            WaitBocedi,
            WaitCar,
            SlaveConnected,
            WaitingPallet,
            PLCStarting,
            PLCLabeling1,
            PLCLabeling2,
            WaitCorrection1,
            WaitCorrection2,
            BCD2EntryError,
            CarEntryError,
            BCD1OK,
            BCD2OK,
            Pause,
            ElevatorRequest,
            LabelNull1,
            LabelNull2,
            WaitLabel1,
            WaitLabel2,
        }

        public static void Init()
        {
            Instance = new FatekPLC();
            Instance.StartCommunication();
        }

        private void StartCommunication()
        {
            modbusServer = new EasyModbus.ModbusServer();
            modbusServer.Listen();
            modbusServer.HoldingRegistersChanged += new EasyModbus.ModbusServer.HoldingRegistersChangedHandler(holdingRegistersChanged);
            modbusServer.CoilsChanged += new EasyModbus.ModbusServer.CoilsChangedHandler(coilChanged);
            sw = Stopwatch.StartNew();
        }

        public static void StopCommunication()
        {
            Instance.modbusServer.StopListening();
        }
        public void SetVerbose(bool verbose)
        {
            this.verbose = verbose;
        }

        private void holdingRegistersChanged(int startingAddress, int quantity)
        {
            sw.Restart();
            if (verbose)
            {
                var value = this.modbusServer.holdingRegisters[startingAddress];
                Console.WriteLine(String.Format("Changes in Holding Register address:{0}, quantity{1}, value: {2}", startingAddress, quantity, value));
            }
        }

        private void coilChanged(int coil, int numberOfCoils)
        {
            sw.Restart();
            if (verbose)
            {
                var value = this.modbusServer.coils[coil];
                Console.WriteLine(String.Format("Changes in coil address:{0}, numberOfCoils{1}, value: {2}", coil, numberOfCoils, value));
            }
        }

        public static void SetBit(Signals signal)
        {
            Instance.modbusServer.coils[(int)signal] = true;
        }

        public static void ResetBit(Signals signal)
        {
            Instance.modbusServer.coils[(int)signal] = false;
        }

        public static bool ReadBit(Signals signal)
        {
            return Instance.modbusServer.coils[(int)signal];
        }

        public static bool[] ReadSignals(Signals signal, int lenght)
        {
            var signals = new bool[lenght];
            Array.Copy(Instance.modbusServer.coils.localArray, (int)signal, signals, 0, lenght);
            return signals;
        }

        public static void SetMemory(Memory memory, short value)
        {
            Instance.modbusServer.holdingRegisters[(int)memory] = value;
        }

        public static short ReadMemory(Memory memory)
        {
            return Instance.modbusServer.holdingRegisters[(int)memory];
        }

        public void PrintFIFOs()
        {
            foreach(Memory memory in Enum.GetValues(typeof(Memory)))
            {
                Console.WriteLine(String.Format(
                    "{0}: {1}",
                    memory,
                    modbusServer.holdingRegisters[(int)memory].ToString("X")));
            }
        }

        public static async Task<Pallet> GetPalletInfo(Memory qrIndex, Memory idIndex)
        {
            var qrString = await GetQr(qrIndex);

            if(qrString == null)
            {
                throw new Exception("Could not get the QR from the id");
            }

            var plcId = ReadMemory(idIndex).ToString("X");
            if (qrString != "" && plcId != "0")
            {
                var labelAndId = Convert.ToInt16(plcId.Substring(plcId.Length - 1), 16);
                var labeling = labelAndId > 8;
                var id = labeling ? (labelAndId - 8).ToString() : labelAndId.ToString();
                var recipe = Convert.ToInt16(plcId.Substring(plcId.Length - 2, 1), 16).ToString();
                var inj = VisualID.GetVisualId(Convert.ToUInt16(plcId.Substring(0, plcId.Length - 2), 16));
                return new Pallet() { Qr = qrString, Id = id, Injector = inj, Recipe = recipe, Labeling = labeling };
            }
            else
            {
                return null;
            }
        }

        public static async Task<string> GetQr(Memory qrIndex)
        {
            var idString =
                    ReadMemory(qrIndex + 1).ToString("X") +
                    ReadMemory(qrIndex).ToString("X4");

            int id = Convert.ToInt32(idString, 16);

            if(id != 0)
            {
                return await VisualID.GetQrString(id);
            }
            else
            {
                return "";
            }
        }

        public static async Task<bool> SetQr(Memory qrIndex, string qrString)
        {
            string firstHalf;
            string secondHalf;

            /*
            if (IsQrValid(qrString))
            {
                string hexID = qrString.Substring(1);
                int len = hexID.Length;
                firstHalf = hexID.Substring(len - 4);
                secondHalf = hexID.Substring(0, len - 4);
            }
            else */
            //{
            var id = await VisualID.GetQrId(qrString);
            if(id < 0)
            {
                return false;
            }
            var stringId = id.ToString("X");
            int len = stringId.Length;

            if(len > 4)
            {
                firstHalf = stringId.Substring(len - 4);
                secondHalf = stringId.Substring(0, len - 4);
            }
            else
            {
                firstHalf = stringId;
                secondHalf = "0";
            }
            
            //}

            int value1 = Convert.ToInt32(firstHalf, 16);
            int value2 = Convert.ToInt32(secondHalf, 16);
            SetMemory(qrIndex, (short)value1);
            SetMemory(qrIndex + 1, (short)value2);

            return true;
        }

        public static async Task<bool> SetQrAndId(Memory qrIndex, Memory idIndex, Pallet pallet)
        {
            if(await SetQr(qrIndex, pallet.Qr))
            {
                var injector = VisualID.GetId(pallet.Injector);
                var labelAndId = pallet.Labeling ? 8 + Convert.ToInt16(pallet.Id) : Convert.ToInt16(pallet.Id);
                var injRecipeId = string.Format("{0}{1}{2}", injector.ToString("X"), Convert.ToInt16(pallet.Recipe).ToString("X"), labelAndId.ToString("X"));
                SetMemory(idIndex, Convert.ToInt16(injRecipeId, 16));
                return true;
            }

            return false;
        }

        public static bool IsQrValid(string qrCode)
        {
            string hexa = qrCode.Substring(1);
            try
            {
                int a = Convert.ToInt16(hexa, 16);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
