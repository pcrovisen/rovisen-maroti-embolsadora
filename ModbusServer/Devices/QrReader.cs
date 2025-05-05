using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.Devices
{
    public class QrReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        TcpClient client;
        NetworkStream stream;
        readonly string ip;

        public bool IsConnected
        {
            get { return client != null && client.Connected; }
        }

        public string Ip 
        {
            get { return ip; }
        }

        public QrReader(string ip)
        {
            this.ip = ip;
        }

        public async Task<bool> Connect()
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(ip, 50010);
                return true;
            }
            catch
            {
                Log.Warn("Qr reader not responding");
                client = null;
                return false;
            }
        }

        public void Disconnect()
        {
            stream.Close();
            client.Close();
        }

        public async Task<string> Read()
        {
            String result = await SendMessageO2I("T?");

            if (result == "!" || result == String.Empty)
                return String.Empty;

            string[] message = result.Split(';');

            if (message.Length != 4)
                return String.Empty;

            if (message[0] != "star")
                return String.Empty;

            if (message[3] != "stop")
                return String.Empty;

            return message[2];
        }

        private async Task<string> SendMessageO2I(string msg)
        {
            try
            {
                String commandO2iV3 = String.Empty;

                //built valid protocol V3 string 

                String ticketnumber = "1234";
                //length msg + 6 bytes, format with leading zeros
                String length = "L" + (msg.Length + 6).ToString().PadLeft(9, '0');
                //built up command string (ticket + length + /r/n + ticket + data /r/n) 
                //1234L000000008{0D}{0A}1234T?{0D}{0A}1234L000000043{0D}{0A}
                String command = ticketnumber + length + "\r\n" + ticketnumber + msg + "\r\n";

                //NetworkStream 
                stream = client.GetStream();


                ASCIIEncoding encoder = new ASCIIEncoding();
                byte[] buffer = encoder.GetBytes(command);

                await stream.WriteAsync(buffer, 0, buffer.Length);
                stream.Flush();

                // Receive the TcpServer response. 

                // Buffer to store the response bytes. 
                Byte[] data = new Byte[256];

                // String to store the response ASCII representation. 
                String responseData = String.Empty;

                Int32 bytes;

                // Read the first batch of the TcpServer response bytes. 
                bytes = await stream.ReadAsync(data, 0, data.Length);
                responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);

                // Read the second batch of the TcpServer response bytes. 
                bytes = await stream.ReadAsync(data, 0, data.Length);
                responseData = responseData + System.Text.Encoding.ASCII.GetString(data, 0, bytes);

                //response data sampel: 2 codes with content "ABC-abc-1234"
                //1234L000000043{0D}{0A}1234star;1;ABC-abc-1234;ABC-abc-1234;stop
                //ticket + length + /r/n + ticket + data + /r/n
                //extract data from receive string
                //extract length information
                int lengthData = int.Parse(responseData.Substring(5, 9));
                //extract data from string
                string stringData = responseData.Substring(20, (lengthData - 6));
                //return result data
                return stringData;
            }
            catch(Exception ex)
            {
                Log.Error($"Could not send message to the qr reader. Error: {ex.Message}");
                client = null;
                return string.Empty;
            }
            
        }
    }
}
