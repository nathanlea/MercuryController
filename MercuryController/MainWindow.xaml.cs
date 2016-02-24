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
        private bool Connected = false;

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

            _timer2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
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
                string ServIP = "192.168.1.2";//change this to your server ip
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
                Console.WriteLine("CONNECTED");
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
            LeftAxis = string.Format("X: {0} Y: {1}", state.Gamepad.LeftThumbX, state.Gamepad.LeftThumbY);
            RightAxis = string.Format("X: {0} Y: {1}", state.Gamepad.RightThumbX, state.Gamepad.RightThumbY);
            ZAxis = string.Format("R: {0} L: {1}", state.Gamepad.RightTrigger, state.Gamepad.LeftTrigger);
            Buttons = string.Format("{0}", state.Gamepad.Buttons);

            xAxis.Content = LeftAxis;
            yAxis.Content = RightAxis;
            triggers.Content = ZAxis;
            buttonsLabel.Content = Buttons;

            // LY  -32768,32767
            // LX  -32768,32767
            // RX  -32768,32767
            // RY  -32768,32767

            // L R Trigger 0,255

            //Right
            double RightY = state.Gamepad.RightThumbY;
            //Throttle
            double throttle = ( ( RightY + 32768 ) * 0.00389099122 );
            //throttle byte
            byte thr = (Byte) throttle;

            //Left
            double LeftY = state.Gamepad.LeftThumbY;
            //Steering
            double steering = ( ( LeftY + 32768 ) * 0.00389099122 );
            //throttle byte
            byte steer = (Byte)steering;

            //servo
            byte[] servo = new byte[3];

            //AUX?
            byte[] aux = new byte[2];
            int buttons = (int) state.Gamepad.Buttons;
            //Console.WriteLine(buttons);
            aux[0] = (byte)( buttons & 0xFF );
            aux[1] = (byte)((buttons & 0xFF00) >> 8);
    
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

            }

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
