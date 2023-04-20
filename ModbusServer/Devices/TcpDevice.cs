using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusServer.Devices
{
    internal class TcpDevice
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        TcpClient client;
        public string Name { get; private set; }

        public bool Connected
        {
            get { return client != null; }
        }

        public TcpDevice(TcpClient client)
        {
            this.client = client;
            Name = ((IPEndPoint)client.Client.RemoteEndPoint).ToString();
        }

        public async Task<string> Receive(CancellationToken ctk)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var byteCount = await stream.ReadAsync(buffer, 0, buffer.Length, ctk);
                var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                return request;
            }
            catch(Exception ex)
            {
                Log.Error($"Error with the device {Name}. Error: {ex.Message}");
                client.Close();
                client = null;
                return null;
            }
        }

        public async Task<bool> Send(string message, CancellationToken ctk)
        {
            try
            {
                var stream = client.GetStream();
                var echoBytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(echoBytes, 0, echoBytes.Length, ctk);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error with the device {Name}. Error: {ex.Message}");
                client.Close();
                client = null;
                return false;
            }
        }
    }
}
