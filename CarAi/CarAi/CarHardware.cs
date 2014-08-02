using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Car_Control;
using System.Threading;
using Windows.Networking.Proximity;
using System.Diagnostics;
namespace CarAi
{
    class CarHardware
    {
        private BluetoothManager bluetooth;
        public enum States {Disconnected,Connecting,Connected};
        private States State;
        private SynchronizationContext _context;
        private volatile bool BeatBack = false;
        private const string BluetoothName = "HC-06";
        public struct Pose
        {
            public Pose(int v)
            {
                Throttle = 0;
                Steering = 50;
                Headlights = false;
                SonarScan = 0;
                Tilt = 0;
                RearLeft = new int[3];
                RearRight = new int[3];
                SonarReading = new float[3];
            }
            public int Throttle;
            public int Steering;
            public int Tilt;
            public bool Headlights;
            public int SonarScan;
            public int[] RearLeft;
            public int[] RearRight;
            public float[] SonarReading;
        }
        private Pose VehicalPose;
        private int SonarIndex;
        public delegate void SonarUpdatedHandler(Pose newPose);
        public event SonarUpdatedHandler SonarUpdated;
        public delegate void ConnectionChangedHandler(States current);
        public event ConnectionChangedHandler ConnectionChanged;
        public bool Retry;
        public CarHardware()
        {
            bluetooth = new BluetoothManager();
            bluetooth.MessageReceived += MessageRecieved;
            _context = SynchronizationContext.Current;
            State = States.Disconnected;
            VehicalPose = new Pose(0);
            bluetooth.Initalize();
            SonarIndex = -1;
            
        }
        public async Task<bool> Search()
        {
            //PeerFinder.Start();
            //PeerFinder.AlternateIdentities["Bluetooth:Paired"] = "";
            try
            {
                var pairedDevices = await PeerFinder.FindAllPeersAsync();//await PeerFinder.FindAllPeersAsync();
                if (pairedDevices.Count == 0)
                {
                    
                    return false;
                }
                foreach (var pairedDevice in pairedDevices)
                {
                    if (pairedDevice.DisplayName == "HC-06")
                    {
                        
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);

                return false;
            }
        }
        public async void Connect()
        {
            
            
            PeerFinder.AlternateIdentities["Bluetooth:Paired"] = "";
            var pairedDevices = await PeerFinder.FindAllPeersAsync();//await PeerFinder.FindAllPeersAsync();

            if (pairedDevices.Count == 0)
            {
                Debug.WriteLine("No paired devices were found.");

            }
            else
            {
                try
                {

                        foreach (var pairedDevice in pairedDevices)
                        {
                            if (pairedDevice.DisplayName == "HC-06")
                            {
                                bluetooth.Connected += Connected;
                                bluetooth.Connect(pairedDevice.HostName);
                                break;
                            }
                        }
                        State = States.Connecting;
                    

                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    Debug.WriteLine(ex.Message);
                    //Connect();
                }

            }
        }
        protected async void Connected()
        {
            await bluetooth.SendCommand("Lili Says Hi");
        }
        private void HeartBeatLoop()
        {

            while (State == States.Connected)
            {
                if (BeatBack)
                {
                    BeatBack = false;
                    bluetooth.SendCommand("Beat");
                    Thread.Sleep(1000);

                }
                else
                {
                    State = States.Disconnected;
                    ConnectionChanged(State);
                    
                    //_context.Post((SendOrPostCallback)(_ =>
                    //{
                    //    stateText.Text = "Disconnected";
                    //    connectBT.Content = "Connect";
                    //}), null);
                }
            }
        }
        protected async void MessageRecieved(string message)
        {
            if (message == "I'm Alive")
            {

            }
            if (message == "Hello Lili")
            {
                //_context.Post((SendOrPostCallback)(_ =>
                //{
                //    stateText.Text = "Connected";
                //    connectBT.Content = "Connected";
                //}
                //    ), null);
                BeatBack = true;
                State = States.Connected;
                ThreadStart heartbeat = new ThreadStart(HeartBeatLoop);
                Thread beatThread = new Thread(heartbeat);
                beatThread.Start();
                ConnectionChanged(State);
            }
            if (message.StartsWith("SONAR"))
            {
                string range = message.Remove(0, 5);
                VehicalPose.SonarReading[SonarIndex] = float.Parse(range);
                SonarIndex = -1;
                SonarUpdated(VehicalPose);
                //_context.Post((SendOrPostCallback)(_ =>
                //{
                //    sonarBT.Content = range + "cm";
                //}), null);
            }
            if (message == "BOP")
            {
                BeatBack = true;
            }
        }
        public void OnNavigateFrom()
        {
            bluetooth.Terminate();
            State = States.Disconnected;
        }
        public void SteerTo(int value)
        {
            if (State == States.Connected)
            {
                int limited = Math.Min(100, Math.Max(0,value));
                bluetooth.SendCommand("st" + limited.ToString());
                VehicalPose.Steering = limited;
            }
        }
        public void SetThrottleTo(int value)
        {
            if (State == States.Connected)
            {
                int limited = Math.Min(100, Math.Max(0, value));
                bluetooth.SendCommand("th" + limited.ToString());
                VehicalPose.Throttle = limited;
            }
        }
        public void SetHeadLights(bool value)
        {
            if (State == States.Connected)
            {
                if (value)
                {
                    bluetooth.SendCommand("LEDlf1");
                    bluetooth.SendCommand("LEDrf1");
                    VehicalPose.Headlights = true;
                }
                else
                {
                    bluetooth.SendCommand("LEDlf0");
                    bluetooth.SendCommand("LEDrf0");
                    VehicalPose.Headlights = false;
                }
            }
        }
        public void SetTaiLight(int Select, int[] color)
        {
            if(color.Length >= 3)
            {
                if (Select == 0)
                {
                    bluetooth.SendCommand("LEDlr" + color[0].ToString("D8") + color[1].ToString("D8") + color[2].ToString("D8"));
                    VehicalPose.RearLeft[0] = color[0];
                    VehicalPose.RearLeft[1] = color[1];
                    VehicalPose.RearLeft[2] = color[2];
                }
                else if (Select == 1)
                {
                    bluetooth.SendCommand("LEDrr" + color[0].ToString("D8") + color[1].ToString("D8") + color[2].ToString("D8"));
                    VehicalPose.RearRight[0] = color[0];
                    VehicalPose.RearRight[2] = color[1];
                    VehicalPose.RearRight[2] = color[2];
                }
                else
                {
                    bluetooth.SendCommand("LEDlr" + color[0].ToString("D8") + color[1].ToString("D8") + color[2].ToString("D8"));
                    bluetooth.SendCommand("LEDrr" + color[0].ToString("D8") + color[1].ToString("D8") + color[2].ToString("D8"));
                    VehicalPose.RearLeft[0] = color[0];
                    VehicalPose.RearLeft[2] = color[1];
                    VehicalPose.RearLeft[2] = color[2];
                    VehicalPose.RearRight[0] = color[0];
                    VehicalPose.RearRight[2] = color[1];
                    VehicalPose.RearRight[2] = color[2];
                }
            }
        }
        public void SetTilt(int value)
        {
            if (State == States.Connected)
            {
                int limited = Math.Min(100, Math.Max(0, value));
                bluetooth.SendCommand("ti" + limited.ToString());
                VehicalPose.Tilt = limited;
            }
        }
        public void UpdateSonars(int index)
        {
            if (State == States.Connected && SonarIndex == -1)
            {
                int limited = Math.Min(2, Math.Max(0, index));
                bluetooth.SendCommand("SONAR" + limited.ToString());
                SonarIndex = limited;
            }
        }
        public Pose GetPose()
        {
            return VehicalPose;
        }
    }
}
