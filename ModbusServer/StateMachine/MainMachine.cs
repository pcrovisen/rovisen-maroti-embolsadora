using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    public class MainMachine
    {
        public static object dictLock = new object(); 

        internal FatekPLCCommunication fatekPLCCommunication;
        internal QrReaderConnection qrReaderConnection;
        internal AcceptHMIs acceptHMIs;
        //internal CarConnection carConnection;
        public MainMachine()
        {
            fatekPLCCommunication = new FatekPLCCommunication();
            qrReaderConnection = new QrReaderConnection();
            acceptHMIs = new AcceptHMIs();
            //carConnection = new CarConnection();
        }

        public void Step()
        {
            Status.Reset();
            qrReaderConnection.Step();
            fatekPLCCommunication.Step();
            //carConnection.Step();

            //Last
            acceptHMIs.Step();
        }
    }
}
