using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using NLog;

using cave.Controller.Projector.NEC;

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
        private readonly Logger logger = LogManager.GetLogger("Window");

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;

            ConnectToProjector();
        }

        public void ConnectToProjector() {
            logger.Debug("ConnectToProjector() called");
            try {
                string ip = Environment.GetEnvironmentVariable("NECTESTIP");
                nec = new NEC( ip );
            } catch( Exception ex ) {
                logger.Error("Failed to instantiate NEC controller: {error}", ex.Message);
            }
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void Btn1Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( NEC.Input.RGB1 ); }
        private void Btn2Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( "RGB2" ); }
        private void Btn3Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( "26" ); }
        private void Btn4Clicked(object sender, EventArgs a) { nec.PowerOnAndSelectInput( 6.0 ); }
        private void Btn5Clicked(object sender, EventArgs a) { nec.GetStatus(); }
        private void Btn6Clicked(object sender, EventArgs a) { nec.GetInfo(); }
        private void Btn7Clicked(object sender, EventArgs a) { nec.test(); }
        private void Btn8Clicked(object sender, EventArgs a) { nec.GetErrors(); }
        private void BtnOnClicked(object sender, EventArgs a) { nec.PowerOn(); }
        private void BtnOffClicked(object sender, EventArgs a) { nec.PowerOff(); }
    }
}
