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
        const int ReadTimeoutMs = 5000;
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
                //response data sampel: 2 codes with content "ABC-abc-1234"
                //1234L000000043{0D}{0A}1234star;1;ABC-abc-1234;ABC-abc-1234;stop
                //ticket + length + /r/n + ticket + data + /r/n
                //the length field counts the data plus 6 bytes (ticket + trailing /r/n)
                Byte[] data = new Byte[256];
                String responseData = String.Empty;
                int expectedLength = -1;

                // Read until the length declared in the header is satisfied. The reader
                // may deliver the response in one or several packets.
                while (expectedLength < 0 || responseData.Length < expectedLength)
                {
                    var readTask = stream.ReadAsync(data, 0, data.Length);
                    if (await Task.WhenAny(readTask, Task.Delay(ReadTimeoutMs)) != readTask)
                    {
                        Log.Error("Timeout waiting for the qr reader response");
                        client.Close();
                        client = null;
                        return string.Empty;
                    }

                    int bytes = await readTask;
                    if (bytes == 0)
                    {
                        Log.Error("Connection closed by the qr reader");
                        client.Close();
                        client = null;
                        return string.Empty;
                    }

                    responseData += System.Text.Encoding.ASCII.GetString(data, 0, bytes);

                    if (expectedLength < 0 && responseData.Length >= 16)
                    {
                        //header (16) + ticket (4) + data (length field - ticket - /r/n)
                        expectedLength = 20 + int.Parse(responseData.Substring(5, 9)) - 6;
                    }
                }

                return responseData.Substring(20, expectedLength - 20);
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
