using log4net;
using ModbusServer.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal class AcceptHMIs : Machine
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public enum States
        {
            Init,
            Listening,
            Connecting,
            Adding,
            Pause,
        }

        TcpListener listener;
        readonly Dictionary<string, HMIConnection> hmis;
        Task<TcpClient> connectionTask;
        CancellationTokenSource cts;

        public AcceptHMIs() : base(States.Init)
        {
            hmis = new Dictionary<string, HMIConnection>();
        }   

        public override void Step()
        {
            switch (State)
            {
                case States.Init:
                    this.listener = TcpListener.Create(8153);
                    listener.Start(10);
                    NextState(States.Listening);
                    break;
                case States.Listening:
                    if (listener.Pending())
                    {
                        cts = new CancellationTokenSource();
                        Log.Info("Connect attempt");
                        connectionTask = Connect(cts.Token);
                        NextState(States.Connecting);
                    }
                    break;
                case States.Connecting:
                    if (connectionTask.IsCompleted)
                    {
                        if(connectionTask.IsCanceled || connectionTask.IsFaulted)
                        {
                            Log.Info("Connect failed");
                            NextState(States.Listening);
                        }
                        else
                        {
                            Log.InfoFormat("New HMI added with ip {0}", connectionTask.Result.Client.RemoteEndPoint.ToString());
                            NextState(States.Adding);
                        }
                    }
                    else
                    {
                        if(StateTime.ElapsedMilliseconds > 5000)
                        {
                            Log.Info("Connect canceled");
                            cts.Cancel();
                        }
                    }
                    break;
                case States.Adding:
                    IPEndPoint iPEndPoint = connectionTask.Result.Client.RemoteEndPoint as IPEndPoint;
                    if (!hmis.ContainsKey(iPEndPoint.Address.ToString()))
                    {
                        hmis.Add(iPEndPoint.Address.ToString(), new HMIConnection(new TcpDevice(connectionTask.Result)));
                        Log.InfoFormat("HMI {0} added to the list", iPEndPoint.Address.ToString());
                        Log.InfoFormat("Total HMIs: {0}", hmis.Count);
                    }
                    else
                    {
                        Log.ErrorFormat("HMI with ip {0} already exist. Replacing", iPEndPoint.Address.ToString());
                        hmis[iPEndPoint.Address.ToString()].Remove();
                        hmis[iPEndPoint.Address.ToString()] = new HMIConnection(new TcpDevice(connectionTask.Result));
                    }
                    NextState(States.Pause);
                    break;
                case States.Pause:
                    if(StateTime.ElapsedMilliseconds > 1000)
                    {
                        Log.Info("Accepting new connections");
                        NextState(States.Listening);
                    }
                    break;

            }

            try
            {
                foreach(var hmi in hmis.Keys.Where(hmi =>
                {
                    hmis[hmi].Step();
                    if(hmis[hmi].Terminated)
                    {
                        Log.InfoFormat("HMI {0} removed", hmis[hmi].Name);
                        hmis[hmi].Remove();
                        return true;
                    }
                    return false;
                }).ToList())
                {
                    hmis.Remove(hmi);
                }

                /*_ = keys.RemoveAll(connection =>
                {
                    connection.Step();
                    if (connection.Terminated)
                    {
                        Log.InfoFormat("HMI {0} removed", connection.Name);
                        connection.Remove();
                        return true;
                    }
                    return false;
                });*/
            }
            catch (Exception ex)
            {
                hmis.Clear();
                Log.Error(ex.Message);
                listener.Stop();
                listener = null;
                NextState(States.Init);
            }
        }

        public async Task<TcpClient> Connect(CancellationToken cancellationToken)
        {
            // Throw an OperationCancelledException if the supplied token is already cancelled
            // Will throw an ObjectDisposedException if the associated CancellationTokenSource is disposed
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await listener.AcceptTcpClientAsync();
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                // Depending on the exact state of the socket, ex could be one of three exceptions:
                //    * SocketException
                //    * ObjectDisposedException
                //    * InvalidOperationException
                //         or
                //    * An as yet unidentified Exception
                // Bubble up the exception as an InnerException of an OperationCancelledException
                throw new OperationCanceledException("AcceptTcpClientAsync() was cancelled.", ex, cancellationToken);
            }
            catch (Exception e)
            {
                Log.Fatal(e.Message);
                throw;
            }
        }

    }
}
