using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Forms;
using System.Text.Json;
using System.Configuration;
using System.Security.Policy;
using System.Reflection;
using System.IO;

namespace TcpHMIClient
{
    public class HMIClient
    {
        enum State
        {
            Init,
            AskInit,
            AskUpdate,
            AskDelete,
            WaitingResponse,
            WaitingDelete,
        }

        public enum PalletEntryStates
        {
            Waiting,
            ReadingQR,
            WaitingAvailability,
            DefaultBehavior,
            AskingDB,
            SendingID,
            WaitForBocedi1,
            WaitEnterBocedi,
            UpdateFIFO1,
            WaitForCar,
            WaitEnterCar,
            UpdateCar,
            ReadingQrInError,
        }

        public enum SignalsNames
        {
            ReadQR,
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
        } 


        public class SystemStatus
        {
            public Config Config { get; set; }
            public Pallet EntryPallet { get; set; }
            public Packager Packager1 { get; set; }
            public Packager Packager2 { get; set; }
            public Car Car { get; set; }
            public Dictionary<string, int> MachineState { get; set; }
            public bool[] Signals { get; set; }
            public Connections Connections { get; set; }
            public Dictionary<string, Dictionary<int, string>> States { get; set; }
            public ErrorMessages ErrorMessages { get; set; }
        }

        public class Config
        {
            public int QrRetries { get; set; }
            public bool ContinueIfNoQr { get; set; }
            public bool ContinueIfNoDB { get; set; }
            public int DefaultRecipe { get; set; }
        }

        public class Status
        {
            public List<Pallet> emb1Queue { get; set; }
            public List<Pallet> emb2Queue { get; set; }
            public Car Car { get; set; }
            public bool QrReaderConnected { get; set; }
            public bool MasterPLCConnected { get; set; }
            public bool SlavePLCConnected { get; set; }
        }

        public class Packager
        {
            public List<Pallet> Queue { get; set; }
            public Pallet LabelPallet { get; set; }
            public Pallet ExitPallet { get; set; }
        }
        public class Pallet
        {
            public string Qr { get; set; }
            public string Id { get; set; }
            public string Recipe { get; set; }
            public string Injector { get; set; }
            public bool Labeling { get; set; }
        }

        public class DeletePallet
        {
            public Pallet Pallet { get; set; }
            public int Packager { get; set; }
            public int Position { get; set; }
        }

        public class Car
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
        }
        public class Connections
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

        public class ErrorMessages
        {
            public string BDC1Error { get; set; }
            public string BDC2Error { get; set; }
            public string EntryError { get; set; }
            public string CarError { get; set; }
        }

        State state;
        TcpClient tcpClient;
        BackgroundWorker worker;
        public DeletePallet deletePallet;
        public bool needToDelete;
        public PalletDetail detailForm;

        public HMIClient(BackgroundWorker worker)
        {
            this.worker = worker;
            this.state = State.Init;
        }

        public void Step()
        {
            switch (state)
            {
                case State.Init:
                    Thread.Sleep(1000);
                    Console.WriteLine("Trying to connect");
                    if (Connect())
                    {
                        Console.WriteLine("Conected");
                        state = State.AskInit;
                    }
                    else
                    {
                        Console.WriteLine("Connection Failed");
                    }

                    break;
                case State.AskInit:
                    Console.WriteLine("Ask init");
                    if (Request("init"))
                    {
                        state = State.WaitingResponse;
                    }
                    else
                    {
                        Console.WriteLine("Request Failed");
                        state = State.Init;
                        
                    }
                    break;
                case State.AskUpdate:
                    Console.WriteLine("Ask update");
                    if (Request("update"))
                    {
                        state = State.WaitingResponse;
                    }
                    else
                    {
                        Console.WriteLine("Request Failed");
                        state = State.Init;
                        
                    }
                    break;
                case State.WaitingResponse:
                    Console.WriteLine("Waiting response");
                    if (WaitResponse())
                    {
                        if (needToDelete)
                        {
                            needToDelete = false;
                            state = State.AskDelete;
                            break;
                        }
                        state = State.AskUpdate;
                    }
                    else
                    {
                        Console.WriteLine("Request Failed");
                        state = State.Init;
                        
                    }
                    break;
                case State.AskDelete:
                    Console.WriteLine("Ask delete");
                    if (Request(CreateDeleteMessage()))
                    {
                        state = State.WaitingDelete;
                    }
                    else
                    {
                        state = State.Init;
                    }
                    break;
                case State.WaitingDelete:
                    Console.WriteLine("Wait delete");
                    if (WaitDeletion())
                    {
                        if (detailForm != null)
                        {
                            detailForm.Invoke((MethodInvoker)delegate
                            {
                                detailForm.DialogResult = DialogResult.OK;
                                detailForm.Close();
                            }); 
                        }
                        state = State.AskUpdate;
                    }
                    else
                    {
                        Console.WriteLine("Could not delete");
                        state = State.AskInit;
                        
                    }
                    break;
            }
        }

        private bool WaitDeletion()
        {
            try
            {
                var stream = tcpClient.GetStream();
                var buffer = new byte[20];
                var byteCount = stream.Read(buffer, 0, buffer.Length);
                var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                if(request == "OK")
                    return true;
                else
                    return false;
            }
            catch
            {
                worker.ReportProgress(0);
                return false;
            }
        }

        private string CreateDeleteMessage()
        {
            return "del" + JsonSerializer.Serialize(deletePallet);
        }

        public bool Connect()
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress.Parse(ConfigurationManager.AppSettings["serverIp"]), 8153);
                return true;
            }
            catch
            {
                tcpClient.Close();
                tcpClient = null;
                return false;
            }
        }

        public bool Request(string message)
        {
            try
            {
                var stream = tcpClient.GetStream();
                var echoBytes = Encoding.UTF8.GetBytes(message);
                stream.Write(echoBytes, 0, echoBytes.Length);
                return true;
            }
            catch
            {
                worker.ReportProgress(0);
                return false;
            }
            
        }

        public bool WaitResponse()
        {
            try
            {
                var stream = tcpClient.GetStream();
                var bufferSize = 4096;
                var buffer = new byte[bufferSize];
                var completeMessage = "";
                var byteCount = 0;

                do
                {
                    byteCount = stream.Read(buffer, 0, buffer.Length);
                    var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    completeMessage += request;
                }
                while (byteCount == bufferSize);

                SystemStatus status = JsonSerializer.Deserialize<SystemStatus>(completeMessage);
                worker.ReportProgress(100, status);
                return true;
            }
            catch (IOException)
            {
                worker.ReportProgress(0);
                return false;
            }
            catch (JsonException)
            {
                worker.ReportProgress(0);
                return true;
            }
        }

        internal void Terminate()
        {
            Request("terminate");
            tcpClient.Close();
        }
    }
}
