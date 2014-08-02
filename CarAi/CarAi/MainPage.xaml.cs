using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using CarAi.Resources;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Devices.Sensors;
using Microsoft.Xna.Framework;
using Microsoft.Phone.Net.NetworkInformation;

namespace CarAi
{
    public partial class MainPage : PhoneApplicationPage
    {
        Socket _socket = null;
        static ManualResetEvent _clientDone = new ManualResetEvent(false);
        const int TIMEOUT_MILLISECONDS = 5000;
        const int MAX_BUFFER_SIZE = 2048;
        Motion motion;
        BeepBehavior behavior;
        // Constructor
        public MainPage()
        {
            InitializeComponent();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            behavior = new BeepBehavior(0);
            //_socket.Bind(new IPEndPoint(new IPAddress(new Byte[] {192,168,1,167}),5555));
            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
        }

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    SocketAsyncEventArgs eventArg = new SocketAsyncEventArgs();
        //    eventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs es)
        //    {
        //        //response = es.SocketError.ToString();
        //        // Unblock the UI thread
        //        //_clientDone.Set();
        //    });
        //    string data = "Test";
        //    byte[] payload = Encoding.UTF8.GetBytes(data);
        //    eventArg.SetBuffer(payload, 0, payload.Length);

        //    _socket.SendAsync(eventArg);
        //}
        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
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
            behavior.OnNavigateTo();
            ipOut.Text = Find();
        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            behavior.OnNavigateFrom();
            
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
                behavior.UpdateMotion(e);
                // Show the numeric values for attitude.
                yawTextBlock.Text =
                    "YAW: " + MathHelper.ToDegrees(e.Attitude.Yaw).ToString("0") + "°";
                pitchTextBlock.Text =
                    "PITCH: " + MathHelper.ToDegrees(e.Attitude.Pitch).ToString("0") + "°";
                rollTextBlock.Text =
                    "ROLL: " + MathHelper.ToDegrees(e.Attitude.Roll).ToString("0") + "°";

                // Set the Angle of the triangle RenderTransforms to the attitude of the device.
                ((System.Windows.Media.RotateTransform)yawtriangle.RenderTransform).Angle =
                    MathHelper.ToDegrees(e.Attitude.Yaw);
                ((System.Windows.Media.RotateTransform)pitchtriangle.RenderTransform).Angle =
                    MathHelper.ToDegrees(e.Attitude.Pitch);
                ((System.Windows.Media.RotateTransform)rolltriangle.RenderTransform).Angle =
                    MathHelper.ToDegrees(e.Attitude.Roll);

                // Show the numeric values for acceleration.
                xTextBlock.Text = "X: " + e.DeviceAcceleration.X.ToString("0.00");
                yTextBlock.Text = "Y: " + e.DeviceAcceleration.Y.ToString("0.00");
                zTextBlock.Text = "Z: " + e.DeviceAcceleration.Z.ToString("0.00");

                // Show the acceleration values graphically.
                xLine.X2 = xLine.X1 + e.DeviceAcceleration.X * 100;
                yLine.Y2 = yLine.Y1 - e.DeviceAcceleration.Y * 100;
                zLine.X2 = zLine.X1 - e.DeviceAcceleration.Z * 50;
                zLine.Y2 = zLine.Y1 + e.DeviceAcceleration.Z * 50;

                //xDis.Text = "X Dis: " + behavior.Xdisplacement.ToString("0.00");
                //yDis.Text = "Y Dis: " + behavior.Ydisplacement.ToString("0.00");


            }
        }
        public static string Find()
        {
            StringBuilder IpAddress = new StringBuilder();
            var Hosts = Windows.Networking.Connectivity.NetworkInformation.GetHostNames().ToList();
            foreach (var Host in Hosts)
            {
                string IP = Host.DisplayName;
                //IpAddess.Add(IP);
                IpAddress.Append(IP + " ");
            }
            //IPAddress address = IPAddress.Parse(IpAddess);
            return IpAddress.ToString();
        }
        private void ipBT_Click(object sender, RoutedEventArgs e)
        {
            behavior.StartIP(ipTB.Text);
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