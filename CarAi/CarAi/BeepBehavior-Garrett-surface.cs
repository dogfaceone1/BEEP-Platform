using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO.IsolatedStorage;
using Microsoft.Devices.Sensors;
using Microsoft.Xna.Framework;
using Windows.Phone.Speech.Synthesis;
using System.Device.Location;
using System.Diagnostics;

namespace CarAi
{
    class BeepBehavior
    {
        private CarHardware HardwareInterface;
        private enum States {Initializing, Stopped,Connecting, Run};
        private States State;
        private int BehaviorModel;
        private List<MotionReading> SensorData;
        private Thread ControlLoopThread;
        private SpeechSynthesizer Speech;
        private bool Intialized;
        private ComputerConnection compCon;

        //Behavior specific variables
        private enum BehaviorStates { Mapping, Execution };
        private BehaviorStates BehaviorState;
        private float TiltMaxPitch;
        private float TiltMinPitch;
        private bool Inverted;
        GeoCoordinateWatcher watcher;
        private bool useIP;
        private Stopwatch IPtimer;
        private Stopwatch SystemTime;

        //Navigation Data
        public double Xdisplacement;
        public double Ydisplacement;
        double Xvelocity;
        double Yvelocity;
        float ScaleFactor;
        

        public BeepBehavior(int Model)
        {
            State = States.Initializing;
            BehaviorState = BehaviorStates.Mapping;
            TiltMaxPitch = -1;
            TiltMinPitch = -1;
            Inverted = false;
            Speech = new SpeechSynthesizer();
            HardwareInterface = new CarHardware();
            SensorData = new List<MotionReading>();
            Intialized = true;
            compCon = new ComputerConnection();
            useIP = false;
            IPtimer = new Stopwatch();
            SystemTime = new Stopwatch();
            SystemTime.Start();
            //Connect();
        }
        private async void Connect()
        {
            bool hardwarefound = await HardwareInterface.Search();
            while (!hardwarefound)
            {
                hardwarefound = await HardwareInterface.Search();
                Thread.Sleep(1000);
                //Log.AppendLine("Hardware not found!");
                Speech.SpeakTextAsync("Hardware not found");
            }
            Speech.SpeakTextAsync("Hardware found, Connecting");
            HardwareInterface.ConnectionChanged += ConnectionChanged;
            HardwareInterface.SonarUpdated += SonarsUpdated;
            try
            {
                HardwareInterface.Connect();
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                Speech.SpeakTextAsync("Hardware error. Exitting");
                State = States.Stopped;
                Intialized = false;
                return;
            }
            State = States.Connecting;
            ControlLoopThread = new Thread(new ThreadStart(ControlLoop));
            ControlLoopThread.Start();
            Xdisplacement = 0;
            Ydisplacement = 0;
            Xvelocity = 0;
            Yvelocity = 0;
            ScaleFactor = 0.1f;
        }
        private void ConnectionChanged(CarHardware.States state)
        {
            if (state == CarHardware.States.Connected)
            {
                State = States.Run;
                Speech.SpeakTextAsync("Hardware Connected");
            }
            else if (state == CarHardware.States.Disconnected)
            {
                State = States.Stopped;
                Speech.SpeakTextAsync("Hardware Disconnected");
            }
        }
        private void SonarsUpdated(CarHardware.Pose pose)
        {

        }
        public void ControlLoop()
        {
            while (true)
            {
                if (State == States.Run)
                {
                    if (BehaviorState == BehaviorStates.Mapping)
                    {
                        if (watcher == null)
                        {
                            watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High);
                            watcher.MovementThreshold = 10;
                        }
                        //if (TiltMinPitch == -1)
                        //{
                        //    HardwareInterface.SetTilt(0);
                        //    List<float> past = new List<float>();
                        //    float Last = 0;
                        //    float average = 9000;
                        //    while (Math.Abs(average - Last) > 0.01)
                        //    {
                        //        if (SensorData.Count >= 3)
                        //        {
                        //            Last = average;
                        //            average = 0;
                        //            for (int i = 0; i < 3; i++)
                        //            {
                        //                average += MathHelper.ToDegrees(SensorData[i].Attitude.Pitch);
                        //            }
                        //            average = average / 3.0f;
                        //        }
                        //        Thread.Sleep(250);
                        //    }
                        //    TiltMinPitch = average;
                        //    //Speech.SpeakTextAsync("Done finding Minimum. Value = " + average.ToString());
                        //}
                        //if (TiltMaxPitch == -1)
                        //{
                        //    HardwareInterface.SetTilt(100);
                        //    List<float> past = new List<float>();
                        //    float Last = 0;
                        //    float average = 9000;
                        //    while (Math.Abs(average - Last) > 0.01)
                        //    {
                        //        if (SensorData.Count >= 3)
                        //        {
                        //            Last = average;
                        //            average = 0;
                        //            for (int i = 0; i < 3; i++)
                        //            {
                        //                average += MathHelper.ToDegrees(SensorData[i].Attitude.Pitch);
                        //            }
                        //            average = average / 3.0f;
                        //        }
                        //        Thread.Sleep(250);
                        //    }
                        //    TiltMaxPitch = average;
                        //    //Speech.SpeakTextAsync("Done finding Maximum. Value = " + average.ToString());
                        //}
                        //if (TiltMinPitch > TiltMaxPitch)
                        //{
                        //    float temp = TiltMinPitch;
                        //    TiltMinPitch = TiltMaxPitch;
                        //    TiltMaxPitch = temp;
                        //    Inverted = true;
                        //}
                        BehaviorState = BehaviorStates.Execution;
                    }
                    else if (BehaviorState == BehaviorStates.Execution)
                    {
                        
                        //HoldAngle(5f);
                        calcualtePosition();
                        if (useIP)
                        {
                            CheckPackets();
                        }
                        Thread.Sleep(100);
                    }
                }
            }
        }
        private void CheckPackets()
        {
            ComputerConnection.Message[] messages = compCon.GetMessages(10);
            if (messages != null)
            {
                foreach (ComputerConnection.Message mes in messages)
                {
                    switch (mes.MessageType)
                    {
                        case ComputerConnection.MessageTypes.HeartBeat:
                            long time = IPtimer.ElapsedMilliseconds;
                            compCon.SendMessage(ComputerConnection.MessageTypes.HeartBeat, time.ToString());
                            IPtimer.Restart();
                            break;
                    }
                }
            }
        }
        private void calcualtePosition()
        {
            double averageX = 0;
            double averageY = 0;
            for (int i = 0; i < 5; i++)
            {
                averageX += MathHelper.ToDegrees(SensorData[i].DeviceAcceleration.X);
                averageY += MathHelper.ToDegrees(SensorData[i].DeviceAcceleration.Y);
            }
            averageX = averageX / 5.0;
            averageY = averageY / 5.0;
            if (Math.Abs(averageX) < 0.1)
            {
                averageX = 0;
            }
            if (Math.Abs(averageY) < 0.1)
            {
                averageY = 0;
            }
            Xvelocity += averageX * ScaleFactor * 9.8;
            Yvelocity += averageY * ScaleFactor * 9.8;
            if (Math.Abs(Xvelocity) < 0.01)
            {
                Xvelocity = 0;
            }
            if (Math.Abs(Yvelocity) < 0.01)
            {
                Yvelocity = 0;
            }
            Xdisplacement += Xvelocity * ScaleFactor;
            Ydisplacement += Yvelocity * ScaleFactor;
        }
        private void HoldAngle(float Angle)
        {
            float average = 0;
            for (int i = 0; i < 5; i++)
            {
                average += MathHelper.ToDegrees(SensorData[i].Attitude.Pitch);
            }
            average = average / 5.0f;
            float error = average - Angle;
            float OutPerDegree;
            float p = 0.5f;
            OutPerDegree = (100 / (TiltMaxPitch - TiltMinPitch));
            float pose = HardwareInterface.GetPose().Tilt;
            if (Math.Abs(error) > 0.01)
            {
                if(Inverted)
                {
                    HardwareInterface.SetTilt((int)(p*error * OutPerDegree + pose));
                }
                else
                {
                    HardwareInterface.SetTilt((int)(p*-error * OutPerDegree + pose));
                }
            }
        }
        public void OnNavigateTo()
        {
            if (State == States.Stopped && Intialized)
            {
                Connect();
            }
        }
        public void OnNavigateFrom()
        {
            HardwareInterface.OnNavigateFrom();
            State = States.Stopped;
        }
        public void UpdateMotion(MotionReading motiondata)
        {
            SensorData.Add(motiondata);
            if(SensorData.Count > 5)
            {
                SensorData.RemoveAt(0);
            }
        }
        public void StartIP(string ipaddress)
        {
            if (compCon.SetEndPoint(ipaddress, 3456))
            {
                compCon.SendMessage(ComputerConnection.MessageTypes.HeartBeat, "0");
                Thread.Sleep(200);
                compCon.StartListening();
                useIP = true;
                IPtimer.Start();
            }

        }
    }
}
