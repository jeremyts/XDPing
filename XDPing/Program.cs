using System;
using System.Text;
// Required for sockets
using System.Net.Sockets;

namespace XDPing
{
    class Program
    {
        static int Main(string[] args)
        {
            string deliverycontroller = string.Empty;
            int port = 80;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLower();
                if (arg == "-deliverycontroller" || arg == "--deliverycontroller" || arg == "/deliverycontroller")
                {
                    if (args.Length >= i + 2)
                    {
                        deliverycontroller = args[i + 1];
                    }
                }

                if (arg == "-port" || arg == "--port" || arg == "/port")
                {
                    if (args.Length >= i + 2)
                    {
                        int.TryParse(args[i + 1], out port);
                    }
                }
            }

            if (string.IsNullOrEmpty(deliverycontroller))
            {
                Console.WriteLine("Valid command line arguments must be supplied:");
                Console.WriteLine("-deliverycontroller, --deliverycontroller or /deliverycontroller is a required flag. This must be followed by the name of a Delivery Controller or Cloud Connector.");
                Console.WriteLine("-port, --port or /port is an optional flag. It will default to 80 if not supplied. This is the port the Broker's Registrar service listens on.");
                return -1;
            }

            XDPing(deliverycontroller, port);

            return 0;
        }

        /// <summary>
        /// Performs an XDPing to make sure the Delivery Controller or Cloud Connector is in a healthy state.
        /// It test whether the Broker service is reachable, listening and processing requests on its configured port.
        /// We do this by issuing a blank HTTP POST requests to the Broker's Registrar service.
        /// Including "Expect: 100-continue" in the body will ensure we receive a respose of "HTTP/1.1 100 Continue",
        /// which is what we use to verify that it's in a healthy state.
        /// </summary>
        /// <param name="deliverycontroller"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        static private bool XDPing(string deliverycontroller, int port)
        {
            // This code has essentially been taken from the Citrix Health Assistant Tool and improved for reliability and troubleshooting purposes.
            // I was able to reverse engineer the process by decompiling the VDAAssistant.Backend.dll, which is a component of the Citrix Health
            // Assistant Tool.
            string service = "http://" + deliverycontroller + ":" + port +"/Citrix/CdsController/IRegistrar";
            string s = string.Format("POST {0} HTTP/1.1\r\nContent-Type: application/soap+xml; charset=utf-8\r\nHost: {1}:{2}\r\nContent-Length: 1\r\nExpect: 100-continue\r\nConnection: Close\r\n\r\n", (object)service, (object)deliverycontroller, (object)port);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Attempting an XDPing against " + deliverycontroller + " on TCP port number " + port.ToString());
            bool listening = false;
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.Connect(deliverycontroller, port);
                    if (socket.Connected)
                    {
                        stringBuilder.AppendLine("- Socket connected");
                        byte[] bytes = Encoding.ASCII.GetBytes(s);
                        // Send the string as bytes.
                        socket.Send(bytes, bytes.Length, SocketFlags.None);
                        stringBuilder.AppendLine("- Sent the data");
                        byte[] numArray = new byte[21];
                        socket.ReceiveTimeout = 5000;
                        socket.Receive(numArray);
                        stringBuilder.AppendLine("- Received the following 21 byte array: " + BitConverter.ToString(numArray));
                        // ASCII conversion - string from bytes
                        string strASCII = Encoding.ASCII.GetString(numArray, 0, numArray.Length);
                        // UTF conversion - String from bytes  
                        string strUTF8 = Encoding.UTF8.GetString(numArray, 0, numArray.Length);
                        stringBuilder.AppendLine("- Converting the byte array to an ASCII string we get the output between the quotes: \"" + strASCII + "\"");
                        stringBuilder.AppendLine("- Converting the byte array to a UTF8 string we get the output between the quotes: \"" + strUTF8 + "\"");
                        // Send an additional single byte of 32 (space) as 1 byte with no flags.
                        socket.Send(new byte[1] { (byte)32 }, 1, SocketFlags.None);
                        stringBuilder.AppendLine("- Sending the following string as a byte to close the connection: \"" + BitConverter.ToString(new byte[1] { 32 }) + "\"");
                        if (strASCII.Trim().IndexOf("HTTP/1.1 100 Continue", StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            listening = true;
                            stringBuilder.AppendLine("- The service is listening and healthy");
                        }
                        else
                        {
                            stringBuilder.AppendLine("- The service is not listening");
                        }
                    }
                    else
                    {
                        stringBuilder.AppendLine("- Socket failed to connect");
                    }
                }
                catch (SocketException se)
                {
                    stringBuilder.AppendLine("- Failed to connect to service");
                    stringBuilder.AppendLine("- ERROR: " + se.Message);
                }
                catch (Exception e)
                {
                    stringBuilder.AppendLine("- Failed with an unexpected error");
                    stringBuilder.AppendLine("- ERROR: " + e.Message);
                }
                if (socket.Connected)
                {
                    try
                    {
                        socket.Close();
                        stringBuilder.AppendLine("- Socket closed");
                    }
                    catch (SocketException se)
                    {
                        stringBuilder.AppendLine("- Failed to close the socket");
                        stringBuilder.AppendLine("- ERROR: " + se.Message);
                    }
                    catch (Exception e)
                    {
                        stringBuilder.AppendLine("- Failed with an unexpected error");
                        stringBuilder.AppendLine("- ERROR: " + e.Message);
                    }
                }
                socket.Dispose();
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine("- Failed to create a socket");
                stringBuilder.AppendLine("- ERROR: " + e.Message);
            }
            Console.WriteLine(stringBuilder.ToString().Substring(0, stringBuilder.ToString().Length - 1));
            return listening;
        }
    }
}
