using Gtk;
using System;
using UI = Gtk.Builder.ObjectAttribute;
using NLog;

using Cave.DeviceControllers;
using Cave.DeviceControllers.Projectors.NEC;
using System.Threading.Tasks;
using Cave.DeviceControllers.Projectors;

/*
Where to go from here with the GTK app:

- Redo Glade interface to include a box at the top for entering the IP address,
port, and device type (NEC, PJLink) and a button to connect to/initialize the
device.  These controls should be able to be disabled and/or hidden if we get
our connection information from a predefined config.

- Create a config file just for this app that contains the connection info for
our device.  Create a config reader class in Cave.Utils for reading this file
and returning the info.  If the file exists and we get the info we need, fill in
and/or disable the controls at the top for setting the address/port/device type.

- Create a delegate that checks for the connection info and then instantiates
the device and calls its Initialize method.  This delegate can be connected to
the button click event and maybe the MainWindow's show (?) event.
*/

namespace CaveGtk
{
    internal class MainWindow : Window, IObserver<DeviceInfo>
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

        private IDisposable Unsubscriber;
        private Projector Projector;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
                Projector = new NECProjector( "Test projector", ip );
                Subscribe( Projector );
                Task.Run(Projector.Initialize);
            }
            catch ( Exception ex )
            {
                Logger.Error( ex.Message );
            }
        }

        private void Window_DeleteEvent( object sender, DeleteEventArgs a )
        {
            Application.Quit();
        }

        public void Subscribe( IObservable<DeviceInfo> observable )
        {
            if ( observable != null )
                Unsubscriber = observable.Subscribe( this );
        }
        
        public void Unsubscribe()
        {
            Unsubscriber?.Dispose();
        }

        public void OnNext( DeviceInfo status )
        {
            var message = "";

            message += "Display power: " + status.PowerState?.ToString() ?? "n/a";
            message += Environment.NewLine + "Input selected: " + status.InputSelected?.ToString() ?? "n/a";
            message += Environment.NewLine + "Video mute: " + status.DisplayMuted ?? "n/a";
            message += Environment.NewLine + "Audio mute: " + status.AudioMuted ?? "n/a";
            message += Environment.NewLine;
            message += status.MessageType.ToString() ?? "Info";
            message += ": " + status.Message ?? "";
            DisplayMessage( message );
        }

        public void OnError( Exception exception )
        {
            string errorText = "";
            if ( exception is AggregateException ae )
            {
                errorText += ae.Message + Environment.NewLine;
                foreach ( Exception e in ae.InnerExceptions )
                {
                    errorText += (e.Message) + Environment.NewLine;
                }
            }
            else if ( exception is Exception e )
            {
                errorText += e.GetType() + " :: " + e.Message;
            }
            DisplayMessage(errorText);
        }

        public void OnCompleted( )
        {
            this.Unsubscribe();
        }

        private void Btn1Clicked(object sender, EventArgs a) { Projector?.PowerOnSelectInput("RGB1"); }
        private void Btn2Clicked(object sender, EventArgs a) { Projector?.PowerOnSelectInput("RGB2"); }
        private void Btn3Clicked(object sender, EventArgs a) { Projector?.PowerOnSelectInput("HDMI1"); }
        private void Btn4Clicked(object sender, EventArgs a) { Projector?.PowerOnSelectInput("Video"); }
        private void Btn5Clicked(object sender, EventArgs a) { }
        private void Btn6Clicked(object sender, EventArgs a) { }
        private void Btn7Clicked(object sender, EventArgs a) { }
        private void Btn8Clicked( object sender, EventArgs a )
        {
        }
        private void BtnOnClicked(object sender, EventArgs a) { Projector?.DisplayPowerOn(); }
        private void BtnOffClicked(object sender, EventArgs a) { Projector?.DisplayPowerOff(); }

        private void DisplayMessage( string message )
        {
            _textView.Buffer.Text = message;
        }
    }
}
