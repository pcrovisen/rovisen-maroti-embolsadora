using Microsoft.Win32;
using ModbusServer.Data;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web.Security.AntiXss;
using static Topshelf.Runtime.Windows.NativeMethods;

namespace ModbusServer
{
    internal class Status
    {
        public static Status Instance { get; private set; }
        public Pallet EntryPallet { get; set; }
        public Packager Packager1 { get; private set; }
        public Packager Packager2 { get; private set; }
        public Car Car { get; set; }
        public Connections Connections { get; set; }
        public StateMachineStatus StateMachine { get; set; }
        public ErrorMessages ErrorMessages { get; set; }


        public static void Init()
        {
            Instance = new Status()
            {
                EntryPallet = null,
                Packager1 = new Packager() { Queue = new List<Pallet>(), LabelPallet = null, ExitPallet = null, Updated = false},
                Packager2 = new Packager() { Queue = new List<Pallet>(), LabelPallet = null, ExitPallet = null, Updated = false},
                Car = new Car() { CarPosition = Car.Position.Unknown, HasPallet = false, Pallet = null, Updated = false},
                Connections = new Connections() { MasterPLC = false, SlavePLC = false, QrReader = false, Packager1 = false, Packager2 = false, WencoDB = false},
                StateMachine = new StateMachineStatus() { Machines = new Dictionary<string, object>(), MachinesStates = new Dictionary<string, Dictionary<int, string>>() },
                ErrorMessages = new ErrorMessages() { BDC1Error = "hola1", BDC2Error = "hola2", EntryError = "hola3", CarError = "",}
            };
        }

        public static void Reset()
        {
            Instance.Car.Updated = false;
            Instance.Packager1.Reset();
            Instance.Packager2.Reset();
            Instance.StateMachine.Updated = false;
        }

        public static async Task InitQueues()
        {
            await Instance.Packager1.UpdateQueue(FatekPLC.ReadMemory(FatekPLC.Memory.FIFO2Len), FatekPLC.Memory.FIFO11a, FatekPLC.Memory.FIFO21);
            await Instance.Packager2.UpdateQueue(FatekPLC.ReadMemory(FatekPLC.Memory.FIFO4Len), FatekPLC.Memory.FIFO31a, FatekPLC.Memory.FIFO41);
        }

        internal static async Task UpdateFIFO1()
        {
            await Instance.Packager1.UpdateQueue(FatekPLC.ReadMemory(FatekPLC.Memory.FIFO2Len), FatekPLC.Memory.FIFO11a, FatekPLC.Memory.FIFO21);
        }

        internal static async Task UpdateFIFO2()
        {
            await Instance.Packager2.UpdateQueue(FatekPLC.ReadMemory(FatekPLC.Memory.FIFO4Len), FatekPLC.Memory.FIFO31a, FatekPLC.Memory.FIFO41);
        }

        internal static async Task SetCarPallet(bool hasPallet)
        {
            Instance.Car.Updated = true;
            Instance.Car.HasPallet = hasPallet;
            if (Instance.Car.HasPallet)
            {
                Instance.Car.Pallet = await FatekPLC.GetPalletInfo(FatekPLC.Memory.CARQRa, FatekPLC.Memory.CARID);
            }
            else
            {
                Instance.Car.Pallet = null;
            }
        }
        internal static void SetCarPosition(Car.Position position)
        {
            Instance.Car.Updated = true;
            Instance.Car.CarPosition = position;
        }

        internal static async Task<bool> SetEntryPallet(bool withId = false)
        {
            if(!withId)
            {
                Instance.EntryPallet = new Pallet()
                {
                    Id = "",
                    Qr = await FatekPLC.GetQr(FatekPLC.Memory.QR1),
                    Injector = "",
                    Recipe = "",
                    Labeling = false,
                };

                return Instance.EntryPallet.Qr != "";
            }
            else
            {
                Instance.EntryPallet = await FatekPLC.GetPalletInfo(FatekPLC.Memory.QR1, FatekPLC.Memory.ID);
                return true;
            }
            
        }

        internal static void ResetEntryPallet()
        {
            Instance.EntryPallet = null;
        }
    }

    internal class Packager
    {
        public List<Pallet> Queue { get; set; }
        public Pallet LabelPallet { get; set; }
        public Pallet ExitPallet { get; set; }
        [JsonIgnore]
        public bool Updated { get; set; }

        public void Reset()
        {
            Updated = false;
        }

        public async Task UpdateQueue(int lenght, FatekPLC.Memory startFIFO, FatekPLC.Memory startId)
        {
            if (Queue == null) 
            {
                Queue = new List<Pallet>();
            }

            Queue.Clear();
            Updated = true;

            for (ushort i = 0; i < lenght; i++)
            {
                Queue.Add(await FatekPLC.GetPalletInfo(2 * i + startFIFO, i + startId));
            }

            // Label Pallet
            FatekPLC.Memory aux = startFIFO + 20;
            LabelPallet = await FatekPLC.GetPalletInfo(aux, aux + 2);
           
            // Exit Pallet
            aux += 3;
            ExitPallet = await FatekPLC.GetPalletInfo(aux, aux + 2);
        }
    }

    public class Pallet
    {
        public string Qr { get; set; }
        public string Id { get; set; }
        public string Recipe { get; set; }
        public string Injector { get; set; }
        public bool Labeling { get; set; }
    }

    internal class DeletePallet
    {
        public Pallet Pallet { get; set; }
        public int Packager { get; set; }
        public int Position { get; set; }
    }

    internal class Car
    {
        public enum Position
        {
            Unknown,
            GoingToB1,
            InB1,
            GoingToB2,
            InB2,
        }
        public Position CarPosition { get; set; }
        public bool HasPallet { get; set; }
        public Pallet Pallet { get; set; }
        [JsonIgnore]
        public bool Updated { get; set; }
    }

    internal class Connections
    {
        public bool QrReader { get; set; }
        public bool MasterPLC { get; set; }
        public bool SlavePLC { get; set; }
        public bool WencoDB { get; set; }
        public bool Packager1 { get; set; }
        public bool Packager2 { get; set; }
        public bool Labeler1 { get; set; }
        public bool Labeler2 { get; set; }
    }

    internal class StateMachineStatus
    {
        public Dictionary<string, object> Machines { get; set; }
        public Dictionary<string, Dictionary<int, string>> MachinesStates { get; set; }
        [JsonIgnore]
        public bool Updated { get; set; }
    }

    public class ErrorMessages
    {
        public string BDC1Error { get; set; }
        public string BDC2Error { get; set; }
        public string EntryError { get; set; }
        public string CarError { get; set; }
    }
}
