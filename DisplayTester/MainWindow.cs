using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

using NLog;

using Cave.DeviceControllers;
using Cave.DeviceControllers.Projectors;
using Cave.DeviceControllers.Projectors.NEC;
using Cave.DeviceControllers.Televisions;
using Cave.DeviceControllers.Televisions.Roku;
using Cave.Interfaces;
using Cave.Utils;

namespace Cave.DisplayTester
{
    internal class MainWindow : Window, IObserver<DeviceInfo>
    {
#pragma warning disable CS0414
        /* Top level */
        [UI] private Box BoxMainWindow = null;

        /* Top box controls */
        [UI] private Box BoxTop = null;
        [UI] private ComboBoxText ComboBoxDeviceClass = null;
        [UI] private Box BoxEntries = null;
        [UI] private Entry EntryAddress = null;
        [UI] private Entry EntryPort = null;
        [UI] private Button ButtonConnect = null;
        [UI] private Button ButtonDisconnect = null;
        [UI] private Box BoxBlankMute = null;
        [UI] private Button ButtonBlank = null;
        [UI] private Button ButtonMute = null;
        [UI] private Button ButtonOn = null;
        [UI] private Button ButtonOff = null;

        /* Button grid controls */
        [UI] private Grid GridControls = null;
        [UI] private Button ButtonInput1 = null;
        [UI] private Button ButtonInput2 = null;
        [UI] private Button ButtonInput3 = null;
        [UI] private Button ButtonInput4 = null;
        [UI] private Button ButtonInfo = null;

        /* Channel/volume keys */
        [UI] private Grid GridControlsChannelVolume = null;
        [UI] private Button ButtonChannelUp = null;
        [UI] private Button ButtonChannelDown = null;
        [UI] private Button ButtonVolumeUp = null;
        [UI] private Button ButtonVolumeDown = null;

        /* Media playback/arrow keys */
        [UI] private Grid GridControlsMedia = null;
        [UI] private Button ButtonRewind = null;
        [UI] private Button ButtonFastForward = null;
        [UI] private Button ButtonPlay = null;
        [UI] private Button ButtonLeft = null;
        [UI] private Button ButtonRight = null;
        [UI] private Button ButtonUp = null;
        [UI] private Button ButtonDown = null;

        [UI] private Grid GridControlsBackHome = null;
        [UI] private Button ButtonBack = null;
        [UI] private Button ButtonHome = null;

        [UI] private Button ButtonExecute = null;
        [UI] private Button ButtonGetMethods = null;
        [UI] private Button ButtonClear = null;
        [UI] private Entry EntryDebugCommand = null;

        [UI] private ScrolledWindow ScrollWindowStatus = null;
        [UI] private TextView TextViewStatus = null;
#pragma warning restore CS0414


        private readonly Dictionary<string, int> DisplayTypes = new()
        {
            { "NECProjector", 7142 },
            { "RokuTV", 8060 },
            { "NECProjector (serial)", 38400 }
        };

        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private Cave.DeviceControllers.Device DisplayDevice = null;
        private IDisposable Unsubscriber = null;

        private bool DisplayMuted;
        private bool AudioMuted;


        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow( Builder builder ) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;

            Initialize();
        }

        public void Subscribe( IObservable<DeviceInfo> observable )
        {
            if ( observable != null )
                Unsubscriber = observable.Subscribe(this);
        }

        public void Unsubscribe()
        {
            Unsubscriber?.Dispose();
        }

        public void OnCompleted()
        {
            this.Unsubscribe();
        }

        public void OnNext( DeviceInfo status )
        {
            this.DisplayMuted = status.IsDisplayMuted;
            this.AudioMuted = status.IsAudioMuted;
            if ( status.Message != null )
                DisplayMessage(status.Message);
        }

        public void OnError( Exception ex )
        {
            string errorText = string.Empty;
            switch (ex)
            {
                case AggregateException ae:
                    errorText += ae.Message + Environment.NewLine;
                    foreach ( Exception e in ae.InnerExceptions )
                        errorText += " - " + e.Message + Environment.NewLine;
                    break;
                // Display our own exceptions using ToString() (no stack trace for these)
                case DeviceException de:
                    errorText += de.ToString();
                    break;
                default:
                    errorText += ex.GetType().Name + " :: " + ex.Message;
                    break;
            }
            DisplayMessage(errorText);
        }

        private void DisplayMessage( string message )
        {
            TextViewStatus.Buffer.Text = message;
        }

        private void Initialize()
        {
            // Set ComboBoxDeviceClass model & select first item
            ListStore classStore = new(typeof(string));
            ComboBoxDeviceClass.Model = classStore;
            foreach ( string className in DisplayTypes.Keys )
                classStore.AppendValues(className);

            TreeIter iter;
            ComboBoxDeviceClass.Model.IterNthChild(out iter, 0);
            ComboBoxDeviceClass.SetActiveIter(iter);

            InitializeControls();
        }

        private void InitializeControls()
        {
            // Disable all controls except ButtonConnect, EntryAddress, EntryPort
            GridControlsChannelVolume.Sensitive = false;
            GridControlsMedia.Sensitive = false;
            GridControlsBackHome.Sensitive = false;
            GridControls.Sensitive = false;
            ButtonDisconnect.Sensitive = false;
            ButtonBlank.Sensitive = false;
            ButtonMute.Sensitive = false;
            ButtonOn.Sensitive = false;
            ButtonOff.Sensitive = false;
            ButtonExecute.Sensitive = false;
            ButtonClear.Sensitive = false;
            EntryDebugCommand.Sensitive = false;

            ButtonConnect.Sensitive = true;
            ComboBoxDeviceClass.Sensitive = true;
            EntryAddress.Sensitive = true;
            EntryPort.Sensitive = true;
        }

        private void Window_DeleteEvent( object sender, DeleteEventArgs a )
        {
            Application.Quit();
        }


        private void ComboBoxDeviceClass_Changed( object sender, EventArgs a ) 
        {
            // Fill in the default port #
            ComboBoxText cb = (ComboBoxText)sender;
            EntryPort.Text = DisplayTypes.GetValueOrDefault(cb.ActiveText).ToString();
            // hack for serial support
            if ( cb.ActiveText == "NECProjector (serial)" )
            {
                EntryAddress.PlaceholderText = "Serial port";
                EntryPort.PlaceholderText = "Baudrate";
            }
            else
            {
                EntryAddress.PlaceholderText = "IP Address";
                EntryPort.PlaceholderText = "Port";
            }
        }

        private async void ButtonConnect_Clicked( object sender, EventArgs a )
        {
            try
            {
                var addressOrDevice = EntryAddress.Text;
                var portOrBaudrate = Convert.ToInt32(EntryPort.Text);

                var deviceClass = ComboBoxDeviceClass.ActiveText;

                if ( ! string.IsNullOrWhiteSpace(addressOrDevice) && portOrBaudrate != 0 )
                {
                    switch ( deviceClass )
                    {
                        case nameof(NECProjector):
                            var connectionInfo = new NetworkDeviceConnectionInfo(addressOrDevice, portOrBaudrate);
                            DisplayDevice = new NECProjector("Office projector", connectionInfo);
                            Unsubscriber = DisplayDevice.Subscribe(this);
                            await DisplayDevice.Initialize();
                            EnableControlsForDevice(DisplayDevice);
                            break;
                        case nameof(RokuTV):
                            DisplayDevice = new RokuTV("Home TV", addressOrDevice, portOrBaudrate);
                            Unsubscriber = DisplayDevice.Subscribe(this);
                            await DisplayDevice.Initialize();
                            EnableControlsForDevice(DisplayDevice);
                            break;
                            // hack for serial support
                        case "NECProjector (serial)":
                            var serialInfo = new SerialDeviceConnectionInfo(addressOrDevice, portOrBaudrate);
                            DisplayDevice = new NECProjector("Office projector", serialInfo);
                            Unsubscriber = DisplayDevice.Subscribe(this);
                            await DisplayDevice.Initialize();
                            EnableControlsForDevice(DisplayDevice);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void EnableControlsForDevice( Cave.DeviceControllers.Device device )
        {
            /* Since we apparently connected, disable controls for selecting &
             * connecting to a device ... */
            DisableWidgets(
                ButtonConnect,
                EntryAddress,
                EntryPort,
                ComboBoxDeviceClass
            );

            /* ... and enable those for controlling this one & resetting the UI */
            EnableWidgets(
                ButtonDisconnect,
                ButtonOn,
                ButtonOff,
                ButtonMute,
                GridControls,
                ButtonExecute,
                ButtonClear,
                EntryDebugCommand
            );

            if ( device is Television )
            {
                EnableWidgets(
                    GridControlsChannelVolume,
                    GridControlsMedia,
                    GridControlsBackHome
                );
            }
            else if ( device is Projector )
            {
                EnableWidgets(ButtonBlank);
            }
        }

        private static void EnableWidgets( params Gtk.Widget[] widgets )
        {
            foreach ( var widget in widgets )
                widget.Sensitive = true;
        }

        private static void DisableWidgets( params Gtk.Widget[] widgets )
        {
            foreach ( var widget in widgets )
                widget.Sensitive = false;
        }

        private void ButtonDisconnect_Clicked( object sender, EventArgs a )
        {
            InitializeControls();
        }

        private async void ButtonOn_Clicked( object sender, EventArgs a )
        {
            try
            {
                IDisplay display = DisplayDevice as IDisplay;
                if ( display != null )
                    await display.DisplayPowerOn();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonOff_Clicked( object sender, EventArgs a )
        {
            try
            {
                IDisplay display = DisplayDevice as IDisplay;
                if ( display != null )
                    await display.DisplayPowerOff();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonInput1_Clicked( object sender, EventArgs a )
        {
            try
            {
                switch ( DisplayDevice )
                {
                    case NECProjector nec:
                        await nec.PowerOnSelectInput("RGB1");
                        break;
                    case RokuTV roku:
                        await roku.SelectInput("InputHDMI1");
                        break;
                    default:
                        break;
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonInput2_Clicked( object sender, EventArgs a )
        {
            try
            {
                switch ( DisplayDevice )
                {
                    case NECProjector nec:
                        await nec.PowerOnSelectInput("RGB2");
                        break;
                    case RokuTV roku:
                        await roku.SelectInput("InputHDMI2");
                        break;
                    default:
                        break;
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonInput3_Clicked( object sender, EventArgs a )
        {
            try
            {
                switch ( DisplayDevice )
                {
                    case NECProjector nec:
                        await nec.PowerOnSelectInput("HDMI1");
                        break;
                    case RokuTV roku:
                        await roku.SelectInput("InputHDMI3");
                        break;
                    default:
                        break;
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonInput4_Clicked( object sender, EventArgs a )
        {
            try
            {
                switch ( DisplayDevice )
                {
                    case NECProjector nec:
                        await nec.PowerOnSelectInput("Video");
                        break;
                    case RokuTV roku:
                        await roku.SelectInput("InputTuner");
                        break;
                    default:
                        break;
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonInfo_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is IDebuggable iDebug )
                {
                    var debugInfo = await iDebug.GetDebugInfo();
                    TextViewStatus.Buffer.Text = debugInfo;
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonChannelUp_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.ChannelUp();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonChannelDown_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.ChannelDown();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonVolumeUp_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.AudioVolumeUp();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonVolumeDown_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.AudioVolumeDown();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }
        private async void ButtonRewind_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.Reverse();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonFastForward_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.FastForward();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonPlay_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.Play();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonLeft_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.ArrowLeft();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonRight_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.ArrowRight();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonUp_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.ArrowUp();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonDown_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.ArrowDown();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonBlank_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Projector pj )
                {
                    await pj.DisplayMute(!this.DisplayMuted);
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonMute_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is IAudio ia )
                {
                    await ia.AudioMute(!this.AudioMuted);
                }
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonBack_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.GoBack();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private async void ButtonHome_Clicked( object sender, EventArgs a )
        {
            try
            {
                if ( DisplayDevice is Television tv )
                    await tv.Home();
            }
            catch ( Exception ex )
            {
                OnError(ex);
            }
        }

        private void EntryDebugCommand_Activated( object sender, EventArgs a )
        {
            ButtonExecute.Click();
        }

        private void ButtonExecute_Clicked( object sender, EventArgs a )
        {
            try
            {
                TextViewStatus.Buffer.Text = string.Empty;
                if ( DisplayDevice is IDebuggable debuggable )
                {
                    var result = SimpleMethodCallParser.ParseCallString(
                        EntryDebugCommand.Buffer.Text, debuggable
                    );
                    if ( result is string resultString )
                        DisplayMessage(resultString);
                    else 
                        DisplayExecuteResult(result);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{nameof(ButtonExecute_Clicked)} :: {ex}");
                OnError(ex);
            }
        }

        /* A most likely poor and ill-fated attempt to be able to display a
         * wide variety of result types rather than just strings... it seems to
         * be working for now? Task<T> are not covariant so unfortunately,
         * I don't think we can just blanket await Task<object> (I've tried).*/
        private async void DisplayExecuteResult(object result)
        {
            object tResult = null;
            switch(result)
            {
                case string resultString:
                    DisplayMessage(resultString);
                    break;
                case Task<string> resultTaskString:
                    tResult = await resultTaskString;
                    break;
                case Task<HttpResponseMessage> resultTaskHttpResponseMessage:
                    tResult = await resultTaskHttpResponseMessage;
                    break;
                case Task<bool> resultTaskBool:
                    tResult = await resultTaskBool;
                    break;
                case Task<object> resultTaskObject:
                    tResult = await resultTaskObject;
                    break;
                default:
                    break;
            }
            if ( tResult != null )
                DisplayMessage(tResult.ToString());
        }

        private void ButtonGetMethods_Clicked( object sender, EventArgs a )
        {
            try
            {
                EntryDebugCommand.Buffer.Text = "GetMethods()";
                ButtonExecute.Click();
            }
            catch { throw; }
        }

        private void ButtonClear_Clicked( object sender, EventArgs a )
        {
            EntryDebugCommand.Buffer.Text = string.Empty;
            TextViewStatus.Buffer.Text = string.Empty;
        }
    }
}
