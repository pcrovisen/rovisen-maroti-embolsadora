using System;
using System.Threading;
using Spectre.Console;
using ModbusServer.Devices;
using log4net;
using System.Reflection;
using System.Configuration;
using Topshelf;
using ModbusServer.Data;

namespace ModbusServer
{
    public class IndustrialPCServer
    {
        MainProcess process;
        private readonly static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        Thread worker;

        public IndustrialPCServer()
        {
            Config.Init();
            Status.Init();
            VisualID.Init();
            SqlDatabase.Init();
        }

        public void StartServer()
        {
            Log.Info("Program started");
            FatekPLC.Init();

            process = new MainProcess();
            worker = new Thread(new ThreadStart(process.DoWork))
            {
                Name = "MainProcess"
            };
            worker.Start();
        }

        public void StopServer()
        {
            process.StopWork();
            worker.Join();
            FatekPLC.StopCommunication();
            Log.Info("Program finished");
        }

    }
    public class Program
    {
        public static void Main()
        {
            var rc = HostFactory.Run(x =>
            {
                x.Service<IndustrialPCServer>(s =>
                {
                    s.ConstructUsing(name => new IndustrialPCServer());
                    s.WhenStarted(tc => tc.StartServer());
                    s.WhenStopped(tc => tc.StopServer());
                });
                x.RunAsLocalSystem();
                x.StartAutomatically();

                x.SetDescription("Industrial PC server as service");
                x.SetDisplayName("Server Wenco Embolsado");
                x.SetServiceName("IndustrialPCServer");

                x.UseLog4Net("log4net.config");

                x.EnableServiceRecovery(r =>
                {
                    //should this be true for crashed or non-zero exits
                    r.OnCrashOnly();

                    r.RestartService(delayInMinutes: 0);
                    r.RestartService(delayInMinutes: 0);
                    // Corresponds to ‘Subsequent failures: Restart the Service’
                    r.RestartService(delayInMinutes: 1);

                    //number of days until the error count resets
                    r.SetResetPeriod(1);
                });
            });

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;
        }
    }
}