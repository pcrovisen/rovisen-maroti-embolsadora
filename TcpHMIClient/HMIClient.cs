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
using Wenco.Contracts;

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

        // The DTOs and the enums the HMI keys on live in Wenco.Contracts, shared
        // with the server. They used to be re-declared here by hand, which is what
        // CLAUDE.md rule 3 existed to police.

        const int MaxMessageSize = 1 << 20;
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
                        if (detailForm != null)
                        {
                            detailForm.Invoke((MethodInvoker)delegate
                            {
                                detailForm.DialogResult = DialogResult.Abort;
                                detailForm.Close();
                            });
                        }
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
                return ReadMessage() == "OK";
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
                var header = BitConverter.GetBytes(echoBytes.Length);
                stream.Write(header, 0, header.Length);
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
                SystemStatus status = JsonSerializer.Deserialize<SystemStatus>(ReadMessage());
                worker.ReportProgress(100, status);
                return true;
            }
            catch
            {
                worker.ReportProgress(0);
                return false;
            }
        }

        // Every message is prefixed with its byte length as a 4-byte little-endian
        // int. The server (TcpDevice) uses the same framing; both sides must change
        // together.
        private string ReadMessage()
        {
            var stream = tcpClient.GetStream();
            var length = BitConverter.ToInt32(ReadExactly(stream, 4), 0);
            if (length <= 0 || length > MaxMessageSize)
            {
                throw new IOException($"Invalid message length {length}");
            }
            return Encoding.UTF8.GetString(ReadExactly(stream, length));
        }

        private byte[] ReadExactly(NetworkStream stream, int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read == 0)
                {
                    throw new IOException("Connection closed");
                }
                offset += read;
            }
            return buffer;
        }

        internal void Terminate()
        {
            Request("terminate");
            tcpClient.Close();
        }
    }
}
