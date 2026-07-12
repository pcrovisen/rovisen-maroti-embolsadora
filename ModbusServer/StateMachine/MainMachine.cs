using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModbusServer.Devices;

namespace ModbusServer.StateMachine
{
    public class MainMachine
    {
        internal FatekPLCCommunication fatekPLCCommunication;
        internal AcceptHMIs acceptHMIs;
        public MainMachine()
        {
            fatekPLCCommunication = new FatekPLCCommunication();
            acceptHMIs = new AcceptHMIs();
        }

        public void Step()
        {
            Status.Reset();
            fatekPLCCommunication.Step();

            //Last
            acceptHMIs.Step();
        }
    }
}
