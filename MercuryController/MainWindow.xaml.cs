using System;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using SharpDX.XInput;

namespace MercuryController
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        DispatcherTimer _timer = new DispatcherTimer();
        private string _leftAxis;
        private string _rightAxis;
        private string _zAxis;
        private string _buttons;
        private Controller _controller;

        public MainWindow()
        {
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _timer.Tick += _timer_Tick;
            _timer.Start();
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            DisplayControllerInformation();
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
            Console.WriteLine(buttons);
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
