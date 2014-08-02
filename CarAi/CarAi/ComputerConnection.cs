using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

namespace CarAi
{
    class ComputerConnection
    {

        SocketClient Client;
        public enum MessageTypes {NULL,HeartBeat,SetThrottle,SetSteering,SetLight,Telemetry}; 
        public struct Message
        {
            public MessageTypes MessageType;
            public string Data;
        }
        List<Message> MessageBuffer;
        Mutex BufferMutex;
        Thread ListeningThread;
        int port;
        IPEndPoint Target;
        public ComputerConnection()
        {
            Client = new SocketClient();
            ListeningThread = new Thread(new ThreadStart(Listner));
            port = 34560;
            BufferMutex = new Mutex();
            MessageBuffer = new List<Message>();
        }
        public void Connect(string Hostname)
        {
           string result = Client.Connect(Hostname, port);
           ListeningThread.Start();

        }
        //private void PacketRecived(byte[] data)
        //{
        //    string result = Encoding.UTF8.GetString(data, 0, data.Length);
        //    Message newMes = new Message();
        //    if (result.StartsWith("HB"))
        //    {
        //        newMes.MessageType = MessageTypes.HeartBeat;
        //    }
        //    else if (result.StartsWith("ST"))
        //    {
        //        newMes.MessageType = MessageTypes.SetThrottle;
        //    }
        //    else if (result.StartsWith("SS"))
        //    {
        //        newMes.MessageType = MessageTypes.SetSteering;
        //    }
        //    else if (result.StartsWith("SL"))
        //    {
        //        newMes.MessageType = MessageTypes.SetLight;
        //    }
        //    else if (result.StartsWith("TL"))
        //    {
        //        newMes.MessageType = MessageTypes.Telemetry;
        //    }
        //    else
        //    {
        //        newMes.MessageType = MessageTypes.NULL;
        //    }
        //    if (newMes.MessageType != MessageTypes.NULL)
        //    {
        //        newMes.Data = result.Remove(2);
        //        if (BufferMutex.WaitOne(100))
        //        {
        //            MessageBuffer.Add(newMes);
        //            BufferMutex.ReleaseMutex();
        //        }
        //    }
        //}
        //public bool SetEndPoint(string ipAddress, int PortNumber)
        //{
        //    IPAddress ip;
        //    if(IPAddress.TryParse(ipAddress,out ip))
        //    {
                
        //        Target = new IPEndPoint(ip, PortNumber);
                
        //        return true;
        //    }
        //    return false;
        //}

        public void Disconnect()
        {
            Client.Close();
        }

        private void Listner()
        {
            string overflow = "";
            while (true)
            {
                string result = Client.Receive();
                if (!result.StartsWith("Operation Timeout") && !result.StartsWith("ER") && !result.StartsWith("Socket is not initialized"))
                {
                    while(result.Contains('\n'))
                    {
                        int index = result.IndexOf('\n');
                        string cmd = result.Remove(index);
                        result = result.Remove(0, index + 1);
                        
                        Message newMes = new Message();
                        if (cmd.StartsWith("HB"))
                        {
                            newMes.MessageType = MessageTypes.HeartBeat;
                        }
                        else if (cmd.StartsWith("ST"))
                        {
                            newMes.MessageType = MessageTypes.SetThrottle;
                        }
                        else if (cmd.StartsWith("SS"))
                        {
                            newMes.MessageType = MessageTypes.SetSteering;
                        }
                        else if (cmd.StartsWith("SL"))
                        {
                            newMes.MessageType = MessageTypes.SetLight;
                        }
                        else if (cmd.StartsWith("TL"))
                        {
                            newMes.MessageType = MessageTypes.Telemetry;
                        }
                        else
                        {
                            newMes.MessageType = MessageTypes.NULL;
                        }
                        if (newMes.MessageType != MessageTypes.NULL)
                        {

                            newMes.Data = cmd.Remove(0,2);
                            if (BufferMutex.WaitOne(100))
                            {
                                MessageBuffer.Add(newMes);
                                BufferMutex.ReleaseMutex();
                            }
                        }
                    }

                }
                
            }
            
        }
        public Message[] GetMessages(int timeout)
        {
            
            if (BufferMutex.WaitOne(timeout))
            {
                if(MessageBuffer.Count > 0)
                {
                    Message[] output = new Message[MessageBuffer.Count];
                    MessageBuffer.CopyTo(output);
                    MessageBuffer.Clear();
                    BufferMutex.ReleaseMutex();
                    return output;
                }
            }
            BufferMutex.ReleaseMutex();
            return null;

        }
        public void SendMessage(MessageTypes Type, string data)
        {
            string sendData;
            switch (Type)
            {
                case MessageTypes.HeartBeat:
                    sendData = "HB";
                    break;
                case MessageTypes.SetThrottle:
                    sendData = "ST";
                    break;
                case MessageTypes.SetSteering:
                    sendData = "SS";
                    break;
                case MessageTypes.SetLight:
                    sendData = "SL";
                    break;
                case MessageTypes.Telemetry:
                    sendData = "TL";
                    break;
                default:
                    sendData = "NN";
                    break;
            }

            Client.Send(sendData + data + '\n');
        }
        public void SendTelemetry(int Throttle, int Steering, long time, float AccX, float AccY)
        {
            Client.Send("TL" + time.ToString() +"," + Throttle.ToString() + "," + Steering.ToString() + "," + AccX.ToString("0.00") + "," + AccY.ToString("0.00") + '\n');
        }
    }
}
