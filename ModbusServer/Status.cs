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
using System.Threading;
using System.Threading.Tasks;
using System.Web.Security.AntiXss;
using static Topshelf.Runtime.Windows.NativeMethods;
using Wenco.Contracts;

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

        // Set by whoever mutates a section, cleared at the top of every
        // MainMachine.Step(); HMIConnection uses them to send the big collections
        // only when they changed. Server bookkeeping, not part of the contract.
        public bool Packager1Updated { get; set; }
        public bool Packager2Updated { get; set; }
        public bool CarUpdated { get; set; }
        public bool StatesUpdated { get; set; }

        static SemaphoreSlim semph1 = new SemaphoreSlim(1, 1);
        static SemaphoreSlim semph2 = new SemaphoreSlim(1, 1);


        public static void Init()
        {
            Instance = new Status()
            {
                EntryPallet = null,
                Packager1 = new Packager() { Queue = new List<Pallet>(), LabelPallet = null, ExitPallet = null },
                Packager2 = new Packager() { Queue = new List<Pallet>(), LabelPallet = null, ExitPallet = null },
                Car = new Car() { CarPosition = Car.Position.Unknown, HasPallet = false, Pallet = null },
                Connections = new Connections() { MasterPLC = false, SlavePLC = false, QrReader = false, Packager1 = false, Packager2 = false, WencoDB = false},
                StateMachine = new StateMachineStatus() { Machines = new Dictionary<string, int>(), MachinesStates = new Dictionary<string, Dictionary<int, string>>() },
                ErrorMessages = new ErrorMessages() { BDC1Error = "", BDC2Error = "", EntryError = "", CarError = "",}
            };
        }

        public static void Reset()
        {
            Instance.CarUpdated = false;
            Instance.Packager1Updated = false;
            Instance.Packager2Updated = false;
            Instance.StatesUpdated = false;
        }

        public static async Task InitQueues()
        {
            await UpdateQueue(Instance.Packager1, FatekPLC.Memory.FIFO2Len, FatekPLC.Memory.FIFO11a, FatekPLC.Memory.FIFO21);
            Instance.Packager1Updated = true;
            await UpdateQueue(Instance.Packager2, FatekPLC.Memory.FIFO4Len, FatekPLC.Memory.FIFO31a, FatekPLC.Memory.FIFO41);
            Instance.Packager2Updated = true;
        }

        /// <summary>
        /// Re-reads one lane's queue from PLC memory.
        ///
        /// Runs on a thread-pool task while HMIConnection may be serializing the
        /// current list on the step thread, so it builds a new list and swaps the
        /// reference instead of mutating the live one.
        /// </summary>
        private static async Task UpdateQueue(Packager packager, FatekPLC.Memory lengthAt,
            FatekPLC.Memory startFIFO, FatekPLC.Memory startId)
        {
            int lenght = FatekPLC.ReadMemory(lengthAt);
            var newQueue = new List<Pallet>();

            for (ushort i = 0; i < lenght; i++)
            {
                newQueue.Add(await FatekPLC.GetPalletInfo(2 * i + startFIFO, i + startId));
            }

            packager.Queue = newQueue;

            FatekPLC.Memory aux = startFIFO + 20;
            packager.LabelPallet = await FatekPLC.GetPalletInfo(aux, aux + 2);

            aux += 3;
            packager.ExitPallet = await FatekPLC.GetPalletInfo(aux, aux + 2);
        }

        internal static async Task UpdateFIFO1()
        {
            await semph1.WaitAsync();
            try
            {
                await UpdateQueue(Instance.Packager1, FatekPLC.Memory.FIFO2Len, FatekPLC.Memory.FIFO11a, FatekPLC.Memory.FIFO21);
                Instance.Packager1Updated = true;
            }
            finally
            {
                semph1.Release();
            }
            
        }

        internal static async Task UpdateFIFO2()
        {
            await semph2.WaitAsync();
            try
            {
                await UpdateQueue(Instance.Packager2, FatekPLC.Memory.FIFO4Len, FatekPLC.Memory.FIFO31a, FatekPLC.Memory.FIFO41);
                Instance.Packager2Updated = true;
            }
            finally
            {
                semph2.Release();
            }
            
        }

        internal static async Task SetCarPallet(bool hasPallet)
        {
            Instance.CarUpdated = true;
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
            Instance.CarUpdated = true;
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

    internal class StateMachineStatus
    {
        public Dictionary<string, int> Machines { get; set; }
        public Dictionary<string, Dictionary<int, string>> MachinesStates { get; set; }
    }
}
