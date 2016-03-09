using System;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using SharpDX.XInput;
using System.Threading;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Windows.Media;

namespace MercuryController
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        DispatcherTimer _timer = new DispatcherTimer();
        DispatcherTimer _timer2 = new DispatcherTimer();
        DispatcherTimer _timer3 = new DispatcherTimer();
        private string _leftAxis;
        private string _rightAxis;
        private string _zAxis;
        private string _buttons;
        private string _pingTime;
        private Controller _controller;
        private StreamWriter swSender;
        private StreamReader srReceiver;
        private TcpClient tcpServer;
        private Thread thrMessaging;
        private byte Tries = 0;
        private bool Connected = false, launch = false, armedLaunch = false, robotConnected = false;
        private int driveMode = 0x0, brakeMode = 0x0;
        private byte[] sendArray = new byte[11];
        public MainWindow()
        {
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _timer.Tick += _timer_Tick;
            _timer.Start();

            connectedIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));

            _timer2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer2.Tick += _timer2_Tick;
            _timer2.Start();

            _timer3 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _timer3.Tick += _timer3_Tick;
            _timer3.Start();

            Connect();
        }

        public void Connect()
        {
            if (!Connected)
            {
                string ServIP = "192.168.1.7";//change this to your server ip
                InitializeConnection(ServIP);
            }
            else
            {
                CloseConnection("Disconnected at user's request.");
            }
        }
        private void InitializeConnection(string ServIp)
        {
            IPAddress ipAddr = IPAddress.Parse(ServIp);
            tcpServer = new TcpClient();
            try
            {
                tcpServer.Connect(ipAddr, 2000);//change that 1986 to your server port
            }
            catch
            {
                if (Connected) CloseConnection("");
                MessageBox.Show("Connecteing to " + ServIp + "\r\nServer is Down ... Try nomber " + Tries); return;
            }
            Connected = true;
            thrMessaging = new Thread(new ThreadStart(ReceiveMessages));
            thrMessaging.Start();
        }
        private void ReceiveMessages()
        {
            srReceiver = new StreamReader(tcpServer.GetStream());
            string ConResponse = srReceiver.ReadLine();
            if (ConResponse[0] == '*')
            {
                robotConnected = true;
            }
            else
            {
                string Reason = "Not Connected: ";
                Reason += ConResponse.Substring(2, ConResponse.Length - 2);
                return;
            }
            while (Connected)
            {
                try
                {
                    string NewMsg = srReceiver.ReadLine();
                    Console.WriteLine(NewMsg);
                    if (NewMsg != "")
                    {
                        char[] message = NewMsg.ToCharArray();
                        byte[] messB = new byte[message.Length >> 1];
                        for (int i = 0; i < messB.Length; ++i)
                        {
                            messB[i] = (byte)((GetHexValue(message[i << 1])) << 4 | (GetHexValue(message[(i << 1) + 1])));
                        }
                        byte[] returnPacket = COBS.decode(messB);
                        if (returnPacket[0] == 2)
                        {
                            long a = returnPacket[1];
                            long b = returnPacket[2];
                            long c = returnPacket[3];
                            long d = returnPacket[4];

                            long millis = DateTime.Now.Ticks;
                            millis &= 0xFFFFFFFF;

                            long tempMillis = a << 24 | b << 16 | c << 8 | d;

                            _pingTime = ""+ (millis - tempMillis) / TimeSpan.TicksPerMillisecond;
                        }
                    }

                }
                catch { Console.WriteLine("ERROR"); }
            }
        }

        public int GetHexValue(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : 55);
        }
        public void CloseConnection(string Reason)
        {
            try
            {
                Connected = false;
                swSender.Close();
                srReceiver.Close();
                tcpServer.Close();
            }
            catch { }
        }
        public void SendMessage(string Msg)
        {
            if (Msg.Length >= 1)
            {
                try
                {
                    Tries++;
                    swSender.WriteLine(Msg);
                    swSender.Flush();
                    Tries = 0;
                }
                catch
                {
                    if (Tries < 5)
                    {
                        try
                        {
                            CloseConnection("No connection made");
                            Connect();
                        }
                        catch { }
                        SendMessage(Msg);
                    }
                    else { MessageBox.Show("Connecting to server faild for 5 tries"); Tries = 0; }
                }
            }
        }
        
        void _timer_Tick(object sender, EventArgs e)
        {
            DisplayControllerInformation();
        }

        void _timer2_Tick(object sender, EventArgs e)
        {
            swSender = new StreamWriter(tcpServer.GetStream());
            swSender.WriteLine(BitConverter.ToString(sendArray).Replace("-", ""));
            swSender.Flush();
        }

        void _timer3_Tick(object sender, EventArgs e)
        {
            swSender = new StreamWriter(tcpServer.GetStream());
            swSender.WriteLine(BitConverter.ToString(createPingPacket()).Replace("-", ""));
            swSender.Flush();
        }

        byte[] createPingPacket( )
        {
            byte[] packet = new byte[6];

            long millis = DateTime.Now.Ticks;

            packet[0] = 0x02; //mode byte
            packet[1] = (byte)((millis & 0xFF000000) >> 24);
            packet[2] = (byte)((millis & 0xFF0000) >> 16);
            packet[3] = (byte)((millis & 0xFF00) >> 8);
            packet[4] = (byte)((millis & 0xFF));

            for (int i = 0; i < 5; i++)
            {
                packet[5] += packet[i];
            }

            return COBS.encode(packet);
        }

        void DisplayControllerInformation()
        {
            var state = _controller.GetState();
           /* LeftAxis = string.Format("X: {0} Y: {1}", state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY);
            RightAxis = string.Format("X: {0} Y: {1}", state.Gamepad.RightThumbX, state.Gamepad.RightThumbY);
            ZAxis = string.Format("R: {0} L: {1}", state.Gamepad.RightTrigger, state.Gamepad.LeftTrigger);
            Buttons = string.Format("{0}", state.Gamepad.Buttons);*/
            int A = (int)state.Gamepad.Buttons;
            int B = (int)state.Gamepad.Buttons;
            int LS = (int)state.Gamepad.Buttons & 0x100;
            int RS = (int)state.Gamepad.Buttons & 0x200;
            int start = (int)state.Gamepad.Buttons & 0x10;
            A &= 0x1000;
            if(A != 0)
            {
                if(driveMode == 0)
                {
                    driveMode = 1;
                }
                else
                {
                    driveMode = (driveMode << 0x1);
                }
                
                if (driveMode == 0x80)
                {
                    driveMode = 0;
                }
            }
           /* xAxis.Content = LeftAxis;
            yAxis.Content = RightAxis;
            triggers.Content = ZAxis;
            buttonsLabel.Content = Buttons;*/

            // LY  -32768,32767
            // LX  -32768,32767
            // RX  -32768,32767
            // RY  -32768,32767

            // L R Trigger 0,255

            //Right
            double RightY = ((double)state.Gamepad.RightThumbY )/ 32768;
            //Left
            double RightX = ((double)state.Gamepad.RightThumbX )/ 32768 ;
            double stickMag = Math.Sqrt(Math.Pow(RightY, 2) + Math.Pow(RightX, 2));
            double angle = Math.Atan2(RightY, RightX);
            double RightTrigger = state.Gamepad.RightTrigger;
            double LeftTrigger = state.Gamepad.LeftTrigger;
            double back = 1;
            double magnitude = 0;
            double LMotor = 0;
            double RMotor = 0;
            if(LeftTrigger > 20)
            {
                back = -back;
                magnitude = LeftTrigger / 255;
            }
            else
            {
                magnitude = RightTrigger / 255;
            }
            if(stickMag > .3)
            {
                if(Math.Abs(angle)> Math.PI / 2)
                {
                    LMotor = back * magnitude * (1 + Math.Cos(angle));
                    RMotor = back * magnitude;
                }
                else
                {
                    LMotor = back * magnitude;
                    RMotor = back * magnitude * (1 - Math.Cos(angle));
                }
            }
            else
            {
                LMotor = back * magnitude;
                RMotor = back * magnitude;
            }            
            
            LMotor = deadband(LMotor, .18);
            RMotor = deadband(RMotor, .18);
            
            LMotor = LMotor > 1.0 ? 1.0 : LMotor;
            RMotor = RMotor > 1.0 ? 1.0 : RMotor;

            LMotor = LMotor < -1.0 ? -1.0 : LMotor;
            RMotor = RMotor < -1.0 ? -1.0 : RMotor;

            

            byte thr = (Byte)(RMotor * 127 + 127);
            byte steer = (Byte)(LMotor * 127 + 127);

            //servo
            byte[] servo = new byte[3];
            servo[0] = launch ? (byte)0x1 : (byte)0x0;

            //AUX?
            byte[] aux = new byte[2];
            aux[0] = 0x00;
            aux[1] = (byte)driveMode;
    
            byte[] packet = new byte[10];

            //Generate Noraml Packet
            if( true )
            {

                packet[0] = 0x00; //mode byte
                packet[1] = thr;
                packet[2] = steer;
                packet[3] = servo[0];
                packet[4] = servo[1];
                packet[5] = servo[2];
                packet[6] = aux[0];
                packet[7] = aux[1];

                for(int i = 0; i < 8; i++)
                {
                    packet[8] += packet[i];
                }

                byte[] stuffedPacket = COBS.encode(packet);
                packetLabelCOBS.Content = BitConverter.ToString(stuffedPacket);
                packetLabel.Content = BitConverter.ToString(packet);
                stuffedPacket.CopyTo(sendArray, 0);

                pingTimeLabel.Content = _pingTime;
                LMotorSpeed.Value = ((int)thr) - 127;
                RMotorSpeed.Value = ((int)steer) - 127;

                var tempDriveMode = driveMode;
                
                for(int i = 1; driveMode != 0 && i <= 7; i++)
                {
                    if ((tempDriveMode & 0x01) == 1)
                    {
                        speedLevelSlider.Value = i;
                        break;
                    }
                    tempDriveMode = tempDriveMode >> 1;
                }
                if (tempDriveMode == 0)
                {
                    speedLevelSlider.Value = 0;
                }
                //Launch Logic
                if (armedLaunch && LS != 0 && RS != 0 && start != 0)
                {
                    armedLaunch = false;
                    launch = true;
                    launchColorBox.Fill = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0));
                    launchWordLabel.Content = "LAUNCHED";
                }
                else
                {  
                    if (LS != 0 && RS != 0)
                    {
                        armedLaunch = true;
                        launchColorBox.Fill = new SolidColorBrush(Color.FromArgb(200, 0, 255, 0));
                        launchWordLabel.Content = "ARMED";
                    }
                    else
                    {
                        armedLaunch = false;
                        launch = false;
                        launchColorBox.Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                        launchWordLabel.Content = "disengaged";
                    }                    
                }
                if(robotConnected)
                {
                    connectedIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
                }
            }

        }

        public double deadband(double JoystickValue, double DeadbandCutOff)
        {
            
            double deadbandreturn;
            if (Math.Abs(JoystickValue) < DeadbandCutOff) {
                deadbandreturn = 0;
            }
            else {
                deadbandreturn = (JoystickValue - (Math.Abs(JoystickValue) / JoystickValue * DeadbandCutOff)) / (1 - DeadbandCutOff);
            }
            return deadbandreturn;
        }


        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _controller = null;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _controller = new Controller(UserIndex.One);
            if (_controller.IsConnected) return;
            MessageBox.Show("Game Controller is not connected ... you know ;)");
            App.Current.Shutdown();
        }

        #region Properties

        public string LeftAxis
        {
            get
            {
                return _leftAxis;
            }
            set
            {
                if (value == _leftAxis) return;
                _leftAxis = value;
                OnPropertyChanged();
            }
        }

        public string RightAxis
        {
            get
            {
                return _rightAxis;
            }
            set
            {
                if (value == _rightAxis) return;
                _rightAxis = value;
                OnPropertyChanged();
            }
        }
        public string ZAxis
        {
            get
            {
                return _zAxis;
            }
            set
            {
                if (value == _zAxis) return;
                _zAxis = value;
                OnPropertyChanged();
            }
        }

        public string Buttons
        {
            get
            {
                return _buttons;
            }
            set
            {
                if (value == _buttons) return;
                _buttons = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
