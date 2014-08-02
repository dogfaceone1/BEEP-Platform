using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Car_Control
{
    class BluetoothManager
    {
        private StreamSocket socket;
        private DataWriter dataWriter;
        private DataReader dataReader;
        private BackgroundWorker dataReadWorker;
        public delegate void MessageReceivedHandler(string message);
        public delegate void ConnectedHandler();
        public event MessageReceivedHandler MessageReceived;
        public event ConnectedHandler Connected;
        public void Initalize()
        {
            socket = new StreamSocket();
            dataReadWorker = new BackgroundWorker();
            dataReadWorker.WorkerSupportsCancellation = true;
            dataReadWorker.DoWork += new DoWorkEventHandler(ReceiveMessages);
        }
        public void Terminate()
        {
            if (socket != null)
            {
                socket.Dispose();
            }
            if (dataReadWorker != null)
            {
                dataReadWorker.CancelAsync();
            }
        }

        public async void Connect(HostName deviceHostName)
        {
            if (socket != null)
            {
                await socket.ConnectAsync(deviceHostName, "1");
                
                dataReader = new DataReader(socket.InputStream);
                dataReadWorker.RunWorkerAsync();
                dataWriter = new DataWriter(socket.OutputStream);
                Connected();
            }
        }

        private async void ReceiveMessages(object sender, DoWorkEventArgs e)
        {
            try
            {
                while (true)
                {
                    uint sizeFeildCount = await dataReader.LoadAsync(1);
                    if (sizeFeildCount != 1)
                    {
                        return;
                    }
                    uint messageLength = dataReader.ReadByte();
                    uint actualMessageLength = await dataReader.LoadAsync(messageLength);
                    if (messageLength != actualMessageLength)
                    {
                        return;
                    }
                    string message = dataReader.ReadString(actualMessageLength);
                    MessageReceived(message);
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        public async Task<uint> SendCommand(string command)
        {
            uint sentCommandSize = 0;
            if (dataWriter != null)
            {
                uint commandSize = dataWriter.MeasureString(command);
                dataWriter.WriteByte((byte)commandSize);
                sentCommandSize = dataWriter.WriteString(command);
                await dataWriter.StoreAsync();
            }
            return sentCommandSize;
        }
    }
}
