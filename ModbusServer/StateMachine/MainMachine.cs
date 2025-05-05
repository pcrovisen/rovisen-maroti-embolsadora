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
        internal AcceptHMIs acceptHMIs;
        //internal CarConnection carConnection;
        public MainMachine()
        {
            fatekPLCCommunication = new FatekPLCCommunication();
            acceptHMIs = new AcceptHMIs();
            //carConnection = new CarConnection();
        }

        public void Step()
        {
            Status.Reset();
            fatekPLCCommunication.Step();
            //carConnection.Step();

            //Last
            acceptHMIs.Step();
        }
    }
}
