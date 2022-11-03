using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

using cave.drivers.projector.NEC;

namespace cave
{
    public class MainWindow : Window
    {
        #pragma warning disable CS0414
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
        #pragma warning restore CS0414

        private NEC nec;
        private ILogger logger;

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;

            this.logger = Program.LogFactory.CreateLogger("MainWindow");

            ConnectToProjector();
        }

        public void ConnectToProjector() {
            logger.LogDebug("ConnectToProjector() called");
            try {
                string ip = Environment.GetEnvironmentVariable("NECTESTIP");
                nec = new NEC( this, ip );
            } catch( Exception ex ) {
                logger.LogError("Failed to instantiate NEC device driver: {error}", ex.Message);
            }
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void Btn1Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( NEC.Input.RGB1 ); }
        private void Btn2Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( NEC.Input.RGB2 ); }
        private void Btn3Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( NEC.Input.HDMI1 ); }
        private void Btn4Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( NEC.Input.Video ); }
        private void Btn5Clicked(object sender, EventArgs a) { nec.GetStatus(); }
        private void Btn6Clicked(object sender, EventArgs a) { nec.GetInfo(); }
        private void Btn7Clicked(object sender, EventArgs a) { nec.test(); }
        private void Btn8Clicked(object sender, EventArgs a) { nec.GetErrors(); }
        private void BtnOnClicked(object sender, EventArgs a) { nec.PowerOn(); }
        private void BtnOffClicked(object sender, EventArgs a) { nec.PowerOff(); }
    }
}
