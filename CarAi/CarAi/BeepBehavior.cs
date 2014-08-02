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
using Windows.Phone.Speech.Recognition;
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
        private float I = 0;
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

        //Auto Drive
        enum AutoDriveStates { Stop, Forward, Backward,Faccel,Baccel,Feql,Beql};
        AutoDriveStates autoDriveState =  AutoDriveStates.Stop;
        int incAmount = 1;

        //Speech Data
        bool UsingSpeech = true;
        SpeechRecognizer RecWithout;
        string[] recGrammer;
        Thread SpeechThread;
        enum DriveStates { DriveForward, DriveBackward, TurnLeft, TurnRight,GoStraight,Stop };
        Queue<DriveStates> SpeechQueue;
       

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
            if (UsingSpeech)
            {
                RecWithout = new SpeechRecognizer();
                recGrammer = new string[6]{"Drive forward","Drive Backward","Turn left","Turn right","Go Straight","Stop"};
                //RecWithout.Grammars.AddGrammarFromList("CarCommands", recGrammer);
                //RecWithout.Settings.InitialSilenceTimeout = TimeSpan.FromSeconds(20);
                Uri cmdGrammar = new Uri("ms-appx:///Grammer.grxml", UriKind.Absolute);
                RecWithout.Grammars.AddGrammarFromUri("CMD", cmdGrammar);
                
                RecWithout.Settings.BabbleTimeout = TimeSpan.FromSeconds(20.0);
                RecWithout.Settings.EndSilenceTimeout = TimeSpan.FromMilliseconds(10);
                SpeechQueue = new Queue<DriveStates>();

            }
            SystemTime.Start();
            //ControlLoopThread = new Thread(new ThreadStart(ControlLoop));
            //ControlLoopThread.Start();
            Connect();
        }
        private async void Connect()
        {
            //bool hardwarefound = await HardwareInterface.Search();
            //while (!hardwarefound)
            //{
            //    hardwarefound = await HardwareInterface.Search();
            //    Thread.Sleep(1000);
            //    //Log.AppendLine("Hardware not found!");
            //    Speech.SpeakTextAsync("Hardware not found");
            //}
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
            if (UsingSpeech)
            {
                SpeechThread = new Thread(new ThreadStart(SpeechLoop));
                await RecWithout.PreloadGrammarsAsync();
            }
            State = States.Connecting;
            ControlLoopThread = new Thread(new ThreadStart(ControlLoop));
            ControlLoopThread.Start();
            autoDriveState = AutoDriveStates.Stop;
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
                            //watcher.Start();
                        }
                        //TiltCalibration();
                        BehaviorState = BehaviorStates.Execution;
                        if (UsingSpeech)
                        {
                            if (SpeechThread.ThreadState == ThreadState.Unstarted)
                            {
                                SpeechThread.Start();
                            }
                        }
                    }
                    else if (BehaviorState == BehaviorStates.Execution)
                    {
                        if (SensorData.Count > 4)
                        {
                            Vector3 average = new Vector3(0, 0, 0);
                            for (int i = 0; i < 5; i++)
                            {
                                average += SensorData[i].DeviceAcceleration;
                            }
                            average = average / 5.0f;

                            //HoldAngle(3f);
                            //calcualtePosition();
                            if (UsingSpeech)
                            {
                                ReadSpeechQueue();
                                SpeedManagement(average);
                            }
                            if (useIP)
                            {
                                SendTelemtry(average);
                                CheckPackets();
                            }
                        }
                        Thread.Sleep(10);
                    }
                }
            }
        }
        private void SpeedManagement(Vector3 average)
        {
            CarHardware.Pose pose = HardwareInterface.GetPose();
            float mag = (float)Math.Sqrt(Math.Pow(average.X, 2) + Math.Pow(average.Y, 2));
            if (Crash(mag))
            {
                autoDriveState = AutoDriveStates.Stop;
                Speech.SpeakTextAsync("Crashed");
            }
            switch (autoDriveState)
            {
                case AutoDriveStates.Stop:
                    HardwareInterface.SetThrottleTo(32);
                    //Speech.SpeakTextAsync("Stopped");
                    break;
                case AutoDriveStates.Forward:
                    HardwareInterface.SetThrottleTo(32 + incAmount);
                    autoDriveState = AutoDriveStates.Faccel;
                    Speech.SpeakTextAsync("Forward");
                    Thread.Sleep(200);
                    break;
                case AutoDriveStates.Backward:
                    HardwareInterface.SetThrottleTo(32 - incAmount);
                    autoDriveState = AutoDriveStates.Baccel;
                    Speech.SpeakTextAsync("Backward");
                    Thread.Sleep(200);
                    break;
                case AutoDriveStates.Faccel:
                    if (mag < 1.5 && pose.Throttle < 40)
                    {
                        HardwareInterface.SetThrottleTo(pose.Throttle + incAmount);
                        //Speech.SpeakTextAsync("Forward Accel " + pose.Throttle);
                        Thread.Sleep(200);
                    }
                    else
                    {
                        Speech.SpeakTextAsync("Done Forward Accel " + pose.Throttle);
                        autoDriveState = AutoDriveStates.Feql;
                    }
                    break;
                case AutoDriveStates.Baccel:
                    if (mag < 1.5 && pose.Throttle > 20)
                    {
                        HardwareInterface.SetThrottleTo(pose.Throttle - incAmount);
                        //Speech.SpeakTextAsync("Backward Accel " + pose.Throttle);
                        Thread.Sleep(200);
                    }
                    else
                    {
                        Speech.SpeakTextAsync("Done Backward Accel " + pose.Throttle);
                        autoDriveState = AutoDriveStates.Beql;
                    }
                    break;
                case AutoDriveStates.Feql:
                    
                    break;
                case AutoDriveStates.Beql:
                    
                    break;
                default:
                    HardwareInterface.SetThrottleTo(32);
                    break;
            }
        }
        private bool Crash(float mag)
        {
            if (mag > 10)
                return true;
            else
                return false;

        }
        private void ReadSpeechQueue()
        {
            if (SpeechQueue.Count > 0)
            {
                DriveStates cmd = SpeechQueue.Dequeue();
                switch (cmd)
                {
                    case DriveStates.DriveBackward:
                        autoDriveState = AutoDriveStates.Backward;
                        break;
                    case DriveStates.DriveForward:
                        autoDriveState = AutoDriveStates.Forward;
                        break;
                    case DriveStates.TurnLeft:
                        HardwareInterface.SteerTo(0);
                        break;
                    case DriveStates.TurnRight:
                        HardwareInterface.SteerTo(100);
                        break;
                    case DriveStates.GoStraight:
                        HardwareInterface.SteerTo(50);
                        break;
                    case DriveStates.Stop:
                        HardwareInterface.SetThrottleTo(32);
                        autoDriveState = AutoDriveStates.Stop;
                        Speech.SpeakTextAsync("Stop Command");
                        break;
                }
            }
        }
        private async void SpeechLoop()
        {
            
            await Speech.SpeakTextAsync("Ready and awaiting commands Sir");
            while(true)
            {
                SpeechRecognitionResult result = await RecWithout.RecognizeAsync();
                if (result.Text != "")
                {
                    string cmd = (string)result.Semantics["CMD"].Value;
                    if (cmd == "Stop")
                    {
                        SpeechQueue.Enqueue(DriveStates.Stop);
                        
                    }
                    else if (cmd == "Drive")
                    {
                        string dir = (string)result.Semantics["Dir"].Value;
                        if (dir == "Forward")
                        {
                            SpeechQueue.Enqueue(DriveStates.DriveForward);
                        }
                        else
                        {
                            SpeechQueue.Enqueue(DriveStates.DriveBackward);
                        }
                    }
                    else if (cmd == "Steer")
                    {
                        string dir = (string)result.Semantics["Dir"].Value;
                        if (dir == "Left")
                        {
                            SpeechQueue.Enqueue(DriveStates.TurnLeft);
                        }
                        else if (dir == "Right")
                        {
                            SpeechQueue.Enqueue(DriveStates.TurnRight);
                        }
                        else if (dir == "Straight")
                        {
                            SpeechQueue.Enqueue(DriveStates.GoStraight);
                        }
                    }
                }
            }
        }
        private void TiltCalibration()
        {
            if (TiltMinPitch == -1)
            {
                HardwareInterface.SetTilt(0);
                List<float> past = new List<float>();
                float Last = 0;
                float average = 9000;
                while (Math.Abs(average - Last) > 0.01)
                {
                    if (SensorData.Count >= 3)
                    {
                        Last = average;
                        average = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            average += MathHelper.ToDegrees(SensorData[i].Attitude.Pitch);
                        }
                        average = average / 3.0f;
                    }
                    Thread.Sleep(250);
                }
                TiltMinPitch = average;
                //Speech.SpeakTextAsync("Done finding Minimum. Value = " + average.ToString());
            }
            if (TiltMaxPitch == -1)
            {
                HardwareInterface.SetTilt(100);
                List<float> past = new List<float>();
                float Last = 0;
                float average = 9000;
                while (Math.Abs(average - Last) > 0.01)
                {
                    if (SensorData.Count >= 3)
                    {
                        Last = average;
                        average = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            average += MathHelper.ToDegrees(SensorData[i].Attitude.Pitch);
                        }
                        average = average / 3.0f;
                    }
                    Thread.Sleep(250);
                }
                TiltMaxPitch = average;
                //Speech.SpeakTextAsync("Done finding Maximum. Value = " + average.ToString());
            }
            if (TiltMinPitch > TiltMaxPitch)
            {
                float temp = TiltMinPitch;
                TiltMinPitch = TiltMaxPitch;
                TiltMaxPitch = temp;
                Inverted = true;
            }
        }
        private void SendTelemtry(Vector3 average)
        {
            CarHardware.Pose pose = HardwareInterface.GetPose();
            compCon.SendTelemetry(pose.Throttle, pose.Steering, SystemTime.ElapsedMilliseconds, MathHelper.ToDegrees(average.X), MathHelper.ToDegrees(average.Y));
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
                        case ComputerConnection.MessageTypes.SetThrottle:
                            HardwareInterface.SetThrottleTo(int.Parse(mes.Data));
                            break;
                        case ComputerConnection.MessageTypes.SetSteering:
                            HardwareInterface.SteerTo(int.Parse(mes.Data));
                            break;
                        case ComputerConnection.MessageTypes.SetLight:
                            string[] vars = mes.Data.Split(',');
                            if (vars[0] == "H")
                            {
                                HardwareInterface.SetHeadLights(bool.Parse(vars[1]));
                            }
                            else
                            {
                                int[] col = new int[3]{int.Parse(vars[1]),int.Parse(vars[2]),int.Parse(vars[3])};
                                HardwareInterface.SetTaiLight(0,col);
                                HardwareInterface.SetTaiLight(1,col);
                            }
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
            float p = 0.2f;//0.5
            float pi = 0.04f;
            OutPerDegree = (100 / (TiltMaxPitch - TiltMinPitch));
            
            float pose = HardwareInterface.GetPose().Tilt;
            if (Math.Abs(error) > 0.01)
            {
                if(Inverted)
                {
                    I += error * OutPerDegree;
                    HardwareInterface.SetTilt((int)(p*error * OutPerDegree + pi*I));
                }
                else
                {
                    I -= error * OutPerDegree;
                    HardwareInterface.SetTilt((int)(p*-error * OutPerDegree + pi*I));
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
            compCon.Disconnect();
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
            compCon.Connect(ipaddress);
            useIP = true;
            IPtimer.Start();

        }
    }
}
