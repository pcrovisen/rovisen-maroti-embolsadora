using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.Devices
{
    internal class NetworkPrinter
    {
        TcpClient client;
        NetworkStream stream;
        public string ip;

        public bool Connected
        {
            get { return client != null && client.Connected; }
        }

        public NetworkPrinter(string ip)
        {
            this.ip = ip;
        }

        public async Task<bool> Connect()
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(ip, 9100);
                return true;
            }
            catch
            {
                client = null;
                return false;
            }
        }

        public void Disconnect()
        {
            stream.Close();
            client.Close();
        }


        public async Task<bool> Print(string msg)
        {
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(msg);
                stream = client.GetStream();
                await stream.WriteAsync(bytes, 0, bytes.Length);
                return true;
            }
            catch
            {
                client = null;
                return false;
            }
            
        }
    }
}
