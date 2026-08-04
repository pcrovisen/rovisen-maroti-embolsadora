using log4net;
using ModbusServer.Data;
using Wenco.Contracts;
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

        // --------------------------------------------------------------------
        // Register encoding
        // --------------------------------------------------------------------
        //
        // Registers are 16-bit, so the two composite values the PLC exchanges with
        // the PC are packed by hand. This used to be done by formatting to hex,
        // slicing the string and parsing the pieces back, which threw
        // FormatException on any word whose hex form was shorter than expected
        // (an injector id of 0 produced a 2-character string and an empty slice)
        // and silently shifted every field if a value overflowed its nibble.
        //
        // The ID word packs three fields:
        //
        //   bits 15-8  injector visual id (Data/VisualID.cs)
        //   bits 7-4   recipe
        //   bits 3-0   label flag + queue id
        //
        // Queue ids cycle 1..7. Bit 3 set (value > 8) means "will be labeled" and
        // the id is the remaining low bits, so 0 and 8 never appear as ids — see
        // "Known quirks" in docs/ARCHITECTURE.md.

        const int IdQueueMask = 0xF;
        const int IdLabelFlag = 8;
        const int IdRecipeShift = 4;
        const int IdRecipeMask = 0xF;
        const int IdInjectorShift = 8;
        const int IdInjectorMask = 0xFF;

        public static short PackId(int injector, int recipe, int labelAndId)
        {
            // Out-of-range values used to shift the neighbouring fields instead of
            // being noticed, because each was formatted independently.
            if (injector > IdInjectorMask || recipe > IdRecipeMask || labelAndId > IdQueueMask
                || injector < 0 || recipe < 0 || labelAndId < 0)
            {
                Log.ErrorFormat("ID word out of range: injector {0}, recipe {1}, labelAndId {2}. Truncating",
                    injector, recipe, labelAndId);
            }
            return (short)(((injector & IdInjectorMask) << IdInjectorShift)
                | ((recipe & IdRecipeMask) << IdRecipeShift)
                | (labelAndId & IdQueueMask));
        }

        // The QR id is a plain 32-bit int spread little-endian over two registers.
        public static int ReadQrId(Memory qrIndex)
        {
            return ((ushort)ReadMemory(qrIndex + 1) << 16) | (ushort)ReadMemory(qrIndex);
        }

        public static void WriteQrId(Memory qrIndex, int id)
        {
            SetMemory(qrIndex, (short)id);
            SetMemory(qrIndex + 1, (short)(id >> 16));
        }

        public static async Task<Pallet> GetPalletInfo(Memory qrIndex, Memory idIndex)
        {
            var qrString = await GetQr(qrIndex);

            if(qrString == null)
            {
                throw new Exception("Could not get the QR from the id");
            }

            var word = (ushort)ReadMemory(idIndex);
            if (qrString == "" || word == 0)
            {
                return null;
            }

            var labelAndId = word & IdQueueMask;
            var labeling = labelAndId > IdLabelFlag;
            var id = labeling ? labelAndId - IdLabelFlag : labelAndId;
            var recipe = (word >> IdRecipeShift) & IdRecipeMask;
            var inj = VisualID.GetVisualId((ushort)((word >> IdInjectorShift) & IdInjectorMask));

            return new Pallet()
            {
                Qr = qrString,
                Id = id.ToString(),
                Injector = inj,
                Recipe = recipe.ToString(),
                Labeling = labeling,
            };
        }

        public static async Task<string> GetQr(Memory qrIndex)
        {
            int id = ReadQrId(qrIndex);

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
            var id = await VisualID.GetQrId(qrString);
            if(id < 0)
            {
                return false;
            }

            WriteQrId(qrIndex, id);
            return true;
        }

        public static async Task<bool> SetQrAndId(Memory qrIndex, Memory idIndex, Pallet pallet)
        {
            if(await SetQr(qrIndex, pallet.Qr))
            {
                var injector = VisualID.GetId(pallet.Injector);
                var queueId = Convert.ToInt16(pallet.Id);
                var labelAndId = pallet.Labeling ? IdLabelFlag + queueId : queueId;
                SetMemory(idIndex, PackId(injector, Convert.ToInt16(pallet.Recipe), labelAndId));
                return true;
            }

            return false;
        }

    }
}
