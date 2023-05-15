using ModbusServer.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using ModbusServer.Devices;
using log4net;
using System.Reflection;

namespace ModbusServer
{
    public class MainProcess
    {
        static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        bool shouldStop = false;
        public MainMachine machine;

        public MainProcess()
        {
            machine = new MainMachine();
        }

        public void DoWork()
        {
            try
            {
                Log.Info("Starting State Process");
                while (!shouldStop)
                {
                    machine.Step();
                    Thread.Sleep(100);
                }
            }
            catch(Exception ex)
            {
                Log.Fatal($"Error in main process. Error: {ex.Message}. From: {ex.StackTrace}");
                System.Environment.Exit(-1);
            }
        }

        public void StopWork()
        {
            shouldStop = true;
        }
    }
}
