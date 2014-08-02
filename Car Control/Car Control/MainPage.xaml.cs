using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Car_Control.Resources;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using System.Diagnostics;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Threading;
using Microsoft.Devices.Sensors;
using Microsoft.Xna.Framework;
using System.IO;
using Windows.Storage;
using System.Text;

namespace Car_Control
{
    public partial class MainPage : PhoneApplicationPage
    {
        private BluetoothManager bluetooth;
        private int State;
        SynchronizationContext _context;
        volatile bool BeatBack = false;
        StorageFolder local;
        
        struct SystemState
        {
            public float AccX;
            public float AccY;
            public int Throttle;
            public int Steering;
            public long Time;
            public override string ToString()
            {
                return Time.ToString() + "," + Throttle.ToString() + "," + Steering.ToString() + "," + AccX.ToString("0.000") + "," + AccY.ToString("0.000") + "/n";
            }
        }
        List<SystemState> Log;

        int Throttle;
        int Steering;
        Stopwatch timing;
        Motion motion;
        // Constructor
        public MainPage()
        {
            InitializeComponent();
            bluetooth = new BluetoothManager();
            bluetooth.MessageReceived += MessageRecieved;
            
            Touch.FrameReported += Touch_FrameReporter;
            steeringText.Text = "50";
            throttleText.Text = "50";
            _context = SynchronizationContext.Current;
            local = Windows.Storage.ApplicationData.Current.LocalFolder;

            Log = new List<SystemState>();
            timing = new Stopwatch();
            timing.Start();
            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            bluetooth.Initalize();
            if (motion == null)
            {
                motion = new Motion();

                motion.TimeBetweenUpdates = TimeSpan.FromMilliseconds(20);
                motion.CurrentValueChanged +=
                    new EventHandler<SensorReadingEventArgs<MotionReading>>(motion_CurrentValueChanged);
            }
            try
            {
                motion.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("unable to start the Motion API.");
            }
            //stateText.Text ="Connecting...";
            //Connect();
        }
        private async void Connect()
        {
            PeerFinder.AlternateIdentities["Bluetooth:Paired"] = "";
            var pairedDevices = await PeerFinder.FindAllPeersAsync();//await PeerFinder.FindAllPeersAsync();

            if (pairedDevices.Count == 0)
            {
                Debug.WriteLine("No paired devices were found.");
            }
            else
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
                State = 1;
                stateText.Text = "Connecting...";
                
            }
        }
        async void Connected()
        {
            stateText.Text = "Handshaking..";
            await bluetooth.SendCommand("Lili Says Hi");
        }
        void HeartBeatLoop()
        {
            
            while (State == 2)
            {
                if (BeatBack)
                {
                    BeatBack = false;
                    bluetooth.SendCommand("Beat");
                    Thread.Sleep(1000);

                }
                else
                {
                    State = 0;
                    _context.Post((SendOrPostCallback)(_ => { 
                        stateText.Text = "Disconnected";
                        connectBT.Content = "Connect";
                    } ),null);
                }
            }
        }
        async void MessageRecieved(string message)
        {
            if (message == "I'm Alive")
            {

            }
            if (message == "Hello Lili")
            {
                _context.Post((SendOrPostCallback)(_ => { 
                    stateText.Text = "Connected";
                    connectBT.Content = "Connected";
                }
                    ),null);
                BeatBack = true;
                State = 2;
                ThreadStart heartbeat = new ThreadStart(HeartBeatLoop);
                Thread beatThread = new Thread(heartbeat);
                beatThread.Start();
                
            }
            if(message.StartsWith("SONAR"))
            {
                string range = message.Remove(0, 5);
                _context.Post((SendOrPostCallback)(_ =>{
                    sonarBT.Content = range + "cm";
                }),null);
            }
            if (message == "BOP")
            {
                BeatBack = true;
            }
        }
        void Touch_FrameReporter(object sender, TouchFrameEventArgs e)
        {
            TouchPointCollection points = e.GetTouchPoints(ContentPanel);
            TouchPoint steeringtouch;
            TouchPoint throttletouch;
            foreach(TouchPoint point in points)
            {
                if (point.Position.Y < steering.Margin.Top + steering.ActualHeight)
                {
                    steeringtouch = point;
                    System.Windows.Point pos = steeringtouch.Position;
                    steering.Value = ((pos.X - steering.Margin.Left )/ steering.ActualWidth) * 100; 
                }
                else if (point.Position.X > throttle.Margin.Left && point.Position.Y > throttle.Margin.Top)
                {
                    throttletouch = point;
                    System.Windows.Point pos = throttletouch.Position;
                    throttle.Value =100 -  ((pos.Y - throttle.Margin.Top) / throttle.ActualHeight) * 100; 
                }
            }
            
            
            
        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            bluetooth.Terminate();
            WriteToFile();
        }

        private async Task WriteToFile()
        {
            StringBuilder output = new StringBuilder();
            foreach(SystemState line in Log)
            {
                output.Append(line.ToString());
            }
            var dataFiolder = await local.CreateFolderAsync("DataFolder",CreationCollisionOption.OpenIfExists);
            var file = await dataFiolder.CreateFileAsync("DataFile.txt", CreationCollisionOption.ReplaceExisting);
            using (var s = await file.OpenStreamForWriteAsync())
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(output.ToString().ToCharArray());
                s.Write(data, 0, data.Length);
            }
        }
        private void steering_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (steeringText != null)
            {
                steeringText.Text = steering.Value.ToString();
                int angle = (int)(steering.Value);
                Steering = angle;
                bluetooth.SendCommand("st" + angle.ToString());
            }
        }

        private void throttle_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (throttleText != null)
            {
                throttleText.Text = throttle.Value.ToString();
                int angle = (int)(throttle.Value);
                Throttle = angle;
                bluetooth.SendCommand("th" + angle.ToString());
            }
        }

        private void connectBT_Click(object sender, RoutedEventArgs e)
        {
            connectBT.Content = "Connecting";
            Connect();

        }
        

        private void SetHead( bool val)
        {
            if(val)
            {
                bluetooth.SendCommand("LEDlf1");
                bluetooth.SendCommand("LEDrf1"); 
            }
            else
            {
                bluetooth.SendCommand("LEDlf0");
                bluetooth.SendCommand("LEDrf0"); 
            }
        }
        private void SetTail(int r, int g, int b)
        {
            bluetooth.SendCommand("LEDlr" + r.ToString("D8") + g.ToString("D8") + b.ToString("D8"));
            bluetooth.SendCommand("LEDrr" + r.ToString("D8") + g.ToString("D8") + b.ToString("D8"));
        }

        private void lightsCB_Checked(object sender, RoutedEventArgs e)
        {
            SetHead(true);
        }

        private void lightsCB_Unchecked(object sender, RoutedEventArgs e)
        {
            SetHead(false);
        }

        private void tilt_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(tilt != null)
            {
                int angle = (int)(tilt.Value);
                bluetooth.SendCommand("ti" + angle.ToString());
            }
        }

        private void sonarBT_Click(object sender, RoutedEventArgs e)
        {
            bluetooth.SendCommand("SONAR0");
        }
        void motion_CurrentValueChanged(object sender, SensorReadingEventArgs<MotionReading> e)
        {
            // This event arrives on a background thread. Use BeginInvoke to call
            // CurrentValueChanged on the UI thread.
            Dispatcher.BeginInvoke(() => CurrentValueChanged(e.SensorReading));
        }
        private void CurrentValueChanged(MotionReading e)
        {
            // Check to see if the Motion data is valid.
            if (motion.IsDataValid)
            {
                SystemState newState = new SystemState();
                newState.Throttle = Throttle;
                newState.Steering = Steering;
                newState.Time = timing.ElapsedMilliseconds;
                newState.AccX = e.DeviceAcceleration.X;
                newState.AccY = e.DeviceAcceleration.Y;
                Log.Add(newState);
                //// Show the numeric values for attitude.
                //yawTextBlock.Text =
                //    "YAW: " + MathHelper.ToDegrees(e.Attitude.Yaw).ToString("0") + "°";
                //pitchTextBlock.Text =
                //    "PITCH: " + MathHelper.ToDegrees(e.Attitude.Pitch).ToString("0") + "°";
                //rollTextBlock.Text =
                //    "ROLL: " + MathHelper.ToDegrees(e.Attitude.Roll).ToString("0") + "°";

                //// Set the Angle of the triangle RenderTransforms to the attitude of the device.
                //((System.Windows.Media.RotateTransform)yawtriangle.RenderTransform).Angle =
                //    MathHelper.ToDegrees(e.Attitude.Yaw);
                //((System.Windows.Media.RotateTransform)pitchtriangle.RenderTransform).Angle =
                //    MathHelper.ToDegrees(e.Attitude.Pitch);
                //((System.Windows.Media.RotateTransform)rolltriangle.RenderTransform).Angle =
                //    MathHelper.ToDegrees(e.Attitude.Roll);

                //// Show the numeric values for acceleration.
                //xTextBlock.Text = "X: " + e.DeviceAcceleration.X.ToString("0.00");
                //yTextBlock.Text = "Y: " + e.DeviceAcceleration.Y.ToString("0.00");
                //zTextBlock.Text = "Z: " + e.DeviceAcceleration.Z.ToString("0.00");

                //// Show the acceleration values graphically.
                //xLine.X2 = xLine.X1 + e.DeviceAcceleration.X * 100;
                //yLine.Y2 = yLine.Y1 - e.DeviceAcceleration.Y * 100;
                //zLine.X2 = zLine.X1 - e.DeviceAcceleration.Z * 50;
                //zLine.Y2 = zLine.Y1 + e.DeviceAcceleration.Z * 50;

                //xDis.Text = "X Dis: " + behavior.Xdisplacement.ToString("0.00");
                //yDis.Text = "Y Dis: " + behavior.Ydisplacement.ToString("0.00");


            }
        }
        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}
    }
}