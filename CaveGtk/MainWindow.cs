using Gtk;
using System;
using UI = Gtk.Builder.ObjectAttribute;
using NLog;

using Cave.DeviceControllers;
using Cave.DeviceControllers.Projectors.NEC;

namespace CaveGtk
{
    internal class MainWindow : Window, IObserver<DeviceStatus>
    {
        [UI] private Button _btn1 = null;
        [UI] private Button _btn2 = null;
        [UI] private Button _btn3 = null;
        [UI] private Button _btn4 = null;
        [UI] private Button _btn5 = null;
        [UI] private Button _btn6 = null;
        [UI] private Button _btn7 = null;
        [UI] private Button _btn8 = null;
        [UI] private Button _btnOn = null;
        [UI] private Button _btnOff = null;
        [UI] private TextView _textView = null;

        private IDisposable unsubscriber;

        private IDisplay display;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public MainWindow() : this( new Builder( "MainWindow.glade" ) ) { }

        private MainWindow( Builder builder ) : base( builder.GetRawOwnedObject( "MainWindow" ) )
        {
            builder.Autoconnect( this );

            DeleteEvent += Window_DeleteEvent;

            ConnectDisplay();
        }

        private void ConnectDisplay()
        {
            try
            {
                string ip = Environment.GetEnvironmentVariable("NECTESTIP");
                display = new NECProjector( ip );
                Subscribe( display );
            }
            catch ( Exception ex )
            {
                logger.Error( ex.Message );
            }
        }

        private void Window_DeleteEvent( object sender, DeleteEventArgs a )
        {
            Application.Quit();
        }

        public void Subscribe( IObservable<DeviceStatus> observable )
        {
            if ( observable != null )
                unsubscriber = observable.Subscribe( this );
        }
        
        public void Unsubscribe()
        {
            unsubscriber?.Dispose();
        }

        public void OnNext( DeviceStatus status )
        {
            var message = "";

            //message += status.PowerState ?? "";
            //message += string.Format($"Display power: {status.PowerState?.ToString()}");
            message += "Display power: " + status.PowerState?.ToString() ?? "n/a";
            message += Environment.NewLine + "Input selected: " + status.InputSelected?.ToString() ?? "n/a";
            message += Environment.NewLine + "Video mute: " + status.VideoMuted ?? "n/a";
            message += Environment.NewLine + "Audio mute: " + status.AudioMuted ?? "n/a";
            message += status.MessageType.ToString() ?? "Info" + status.Message ?? "";
            DisplayMessage( message );
        }

        public void OnError( Exception exception )
        {
            DisplayMessage( exception.ToString() );
        }

        public void OnCompleted( )
        {
            this.Unsubscribe();
        }

        private void Btn1Clicked(object sender, EventArgs a) { }
        private void Btn2Clicked(object sender, EventArgs a) { }
        private void Btn3Clicked(object sender, EventArgs a) { }
        private void Btn4Clicked(object sender, EventArgs a) { }
        private void Btn5Clicked(object sender, EventArgs a) { }
        private void Btn6Clicked(object sender, EventArgs a) { }
        private void Btn7Clicked(object sender, EventArgs a) { }
        private void Btn8Clicked(object sender, EventArgs a) { }
        private void BtnOnClicked(object sender, EventArgs a) { display?.PowerOn(); }
        private void BtnOffClicked(object sender, EventArgs a) { display?.PowerOff(); }

        private void DisplayMessage( string message )
        {
            _textView.Buffer.Text = message;
        }
    }
}
