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
        // Every message is prefixed with its byte length as a 4-byte little-endian
        // int. TcpHMIClient uses the same framing; both sides must change together.
        const int MaxMessageSize = 1 << 20;
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
                var header = await ReadExactly(stream, 4, ctk);
                if (header == null)
                {
                    return null;
                }
                var length = BitConverter.ToInt32(header, 0);
                if (length <= 0 || length > MaxMessageSize)
                {
                    throw new InvalidOperationException($"Invalid message length {length}");
                }
                var payload = await ReadExactly(stream, length, ctk);
                if (payload == null)
                {
                    return null;
                }
                return Encoding.UTF8.GetString(payload);
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
                var header = BitConverter.GetBytes(echoBytes.Length);
                await stream.WriteAsync(header, 0, header.Length, ctk);
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

        static async Task<byte[]> ReadExactly(NetworkStream stream, int count, CancellationToken ctk)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer, offset, count - offset, ctk);
                if (read == 0)
                {
                    // Connection closed by the peer.
                    return null;
                }
                offset += read;
            }
            return buffer;
        }
    }
}
