using log4net;
using ModbusServer.Data;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class CarMachine : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        enum States
        {
            UnknownPosition,
            WaitingCarInB1,
            WaitingCarWithPallet,
            WaitingCarInB2,
            WaitingCarEmpty,
        }
        public CarMachine() : base(States.UnknownPosition)
        {

        }

        public override void Step()
        {

            switch (State)
            {
                case States.UnknownPosition:
                    Status.Instance.ErrorMessages.CarError = "";
                    Status.SetCarPosition(Car.Position.Unknown);
                    if (FatekPLC.ReadBit(FatekPLC.Signals.CarInB1))
                    {
                        Log.Info("Car waiting for pallet");
                        NextState(States.WaitingCarWithPallet);
                        Status.SetCarPosition(Car.Position.InB1);
                    }
                    if (FatekPLC.ReadBit(FatekPLC.Signals.CarInB2))
                    {
                        Log.Info("Car waiting to leave pallet");
                        NextState(States.WaitingCarEmpty);
                        Status.SetCarPosition(Car.Position.InB2);
                    }
                    break;
                case States.WaitingCarInB1:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.CarInB1))
                    {
                        Log.Info("Car waiting for pallet");
                        FatekPLC.ResetBit(FatekPLC.Signals.ConfirmUpdate2);
                        NextState(States.WaitingCarWithPallet);
                        Status.SetCarPosition(Car.Position.InB1);
                    }
                    break;
                case States.WaitingCarWithPallet:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.CarWithPallet))
                    {
                        Log.Info("Car going to B2");
                        NextState(States.WaitingCarInB2);
                        Status.SetCarPosition(Car.Position.GoingToB2);
                    }
                    break;
                case States.WaitingCarInB2:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.CarInB2))
                    {
                        Log.Info("Car waiting to leave pallet");
                        NextState(States.WaitingCarEmpty);
                        Status.SetCarPosition(Car.Position.InB2);
                    }
                    break;
                case States.WaitingCarEmpty:
                    if (FatekPLC.ReadBit(FatekPLC.Signals.BCD2EntryError))
                    {
                        var qrString = FatekPLC.GetQr(FatekPLC.Memory.CARQRa);
                        _ = SqlDatabase.NotifyError(SqlDatabase.SystemErrors.error_entrega_a_carro, code: qrString);
                        Status.Instance.ErrorMessages.CarError = "El carro no pudo entregar el pallet. Volver a posicionar el pallet sobre el carro y pasar el carro a modo LOCAL y alejarlo de la máquina unos centimetros.Finalmente poner el carro en REMOTO.";
                    }
                    else
                    {
                        Status.Instance.ErrorMessages.CarError = "";
                    }
                    if (FatekPLC.ReadBit(FatekPLC.Signals.SendUpdate2))
                    {
                        Status.Instance.ErrorMessages.CarError = "";
                        var qrString = FatekPLC.GetQr(FatekPLC.Memory.CARQRa);
                        Log.Info("Car going to B1");
                        _ = SqlDatabase.NotifyPalletIn(qrString, 2);
                        Status.UpdateFIFO2();
                        Status.SetCarPallet(false);
                        FatekPLC.SetBit(FatekPLC.Signals.ConfirmUpdate2);
                        NextState(States.WaitingCarInB1);
                        Status.SetCarPosition(Car.Position.GoingToB1);
                    }
                    break;
            }
        }
    }
}
