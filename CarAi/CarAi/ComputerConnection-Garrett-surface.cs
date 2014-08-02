using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using NGCP.IO;

namespace CarAi
{
    class ComputerConnection
    {
        // Cached Socket object that will be used by each call for the lifetime of this class
        Socket _socket = null;
        // Signaling object used to notify when an asynchronous operation is completed
        static ManualResetEvent _clientDone = new ManualResetEvent(false);
        // Define a timeout in milliseconds for each asynchronous call. If a response is not received within this
        // timeout period, the call is aborted.
        const int TIMEOUT_MILLISECONDS = 5000;
        // The maximum size of the data buffer to use with the asynchronous socket methods
        const int MAX_BUFFER_SIZE = 2048;
        //UdpClientSocket socket;

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
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            
            MessageBuffer = new List<Message>();
            BufferMutex = new Mutex();
            port = 3456;
            ListeningThread = new Thread(new ThreadStart(Listner));
            
        }
        public void StartListening()
        {
            ListeningThread.Start();
        }
        public bool SetEndPoint(string ipAddress, int PortNumber)
        {
            IPAddress ip;
            if(IPAddress.TryParse(ipAddress,out ip))
            {
                
                Target = new IPEndPoint(ip, PortNumber);
                return true;
            }
            return false;
        }
        private void Send(IPEndPoint endpoint, string data)
        {
            string response = "Operation Timeout";
            if (_socket != null)
            {
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = endpoint;//new IPEndPoint(ipAddress, portNumber);
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                    {
                        response = e.SocketError.ToString();
                        _clientDone.Set();
                    });
                byte[] payload = Encoding.UTF8.GetBytes(data);
                socketEventArg.SetBuffer(payload, 0, payload.Length);
                
                _clientDone.Reset();
                _socket.SendToAsync(socketEventArg);
                _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
            }
            else
            {
                response = "Socket is not initialized";
            }
        }
        private string Receive(int portNumber)
        {
            string response = "TO";
            if (_socket != null)
            {
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.RemoteEndPoint = new IPEndPoint(IPAddress.Any, portNumber);
                socketEventArg.SetBuffer(new Byte[MAX_BUFFER_SIZE], 0, MAX_BUFFER_SIZE);
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            
                            response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                            response = response.Trim('\0');
                        }
                        else
                        {
                            response = "ER " + e.SocketError.ToString();
                        }
                        _clientDone.Set();
                    });
                _clientDone.Reset();
                _socket.ReceiveFromAsync(socketEventArg);
                _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
            }
            else
            {
                response = "SNI";
            }
            return response;
        }
        private void Close()
        {
            if (_socket != null)
            {
                _socket.Close();
            }

        }
        private void Listner()
        {
            while (true)
            {
                string result = Receive(port);
                if (!result.StartsWith("TO") && !result.StartsWith("ER") && !result.StartsWith("SNI"))
                {

                    Message newMes = new Message();
                    if (result.StartsWith("HB"))
                    {
                        newMes.MessageType = MessageTypes.HeartBeat;
                    }
                    else if (result.StartsWith("ST"))
                    {
                        newMes.MessageType = MessageTypes.SetThrottle;
                    }
                    else if (result.StartsWith("SS"))
                    {
                        newMes.MessageType = MessageTypes.SetSteering;
                    }
                    else if (result.StartsWith("SL"))
                    {
                        newMes.MessageType = MessageTypes.SetLight;
                    }
                    else if (result.StartsWith("TL"))
                    {
                        newMes.MessageType = MessageTypes.Telemetry;
                    }
                    else
                    {
                        newMes.MessageType = MessageTypes.NULL;
                    }
                    if (newMes.MessageType != MessageTypes.NULL)
                    {
                        newMes.Data = result.Remove(2);
                        if (BufferMutex.WaitOne(100))
                        {
                            MessageBuffer.Add(newMes);
                            BufferMutex.ReleaseMutex();
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
                    return output;
                }
            }
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
            Send(Target, sendData + data);
        }
        public void SendTelemetry(int Throttle, int Steering, long time, float AccX, float AccY)
        {

        }
    }
}
