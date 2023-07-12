using NLog;
using System.Text;
using System.Threading.Tasks;

using Cave.Utils;


namespace Cave.DeviceControllers.Projectors.NEC
{
    public class NECProjector: Projector
    {
#region Private fields
        private Client? Client = null;
        private string? IpAddress;
        private int Port;
        private static readonly Logger Logger = LogManager.GetLogger("NECProjector");
        private PowerState? PowerState;
        private Input? InputSelected;
        private bool? AudioMuted;
        private bool? VideoMuted;

    #region IObservable    
        private List<IObserver<DeviceStatus>> Observers;
        private class Unsubscriber: IDisposable
        {
            private List<IObserver<DeviceStatus>> observers;
            private IObserver<DeviceStatus> observer;
            public Unsubscriber(List<IObserver<DeviceStatus>> observers, IObserver<DeviceStatus> observer)
            {
                this.observers = observers;
                this.observer = observer;
            }
            public void Dispose()
            {
                if( observer != null && observers.Contains(observer) )
                    observers.Remove(observer);
            }
        }
    #endregion
#endregion

#region Operational data
        private static Dictionary<(int, int), Input> InputState = new Dictionary<(int is1, int is2), Input>()
        {
            { (0x01, 0x01), Input.RGB1 },
            { (0x02, 0x01), Input.RGB2 },
            { (0x03, 0x01), Input.RGB2 },       /*COMPUTER 3, present on very few models and there are at least 3 different codes for this one */
            { (0x01, 0x06), Input.HDMI1 },
            { (0x01, 0x21), Input.HDMI1 },
            { (0x02, 0x06), Input.HDMI2 },
            { (0x02, 0x21), Input.HDMI2 },
            { (0x01, 0x20), Input.HDMI2 },      /*DVI-D*/
            { (0x01, 0x0a), Input.HDMI2 },      /*Stereo DVI (?)*/
            { (0x01, 0x02), Input.Video },
            { (0x01, 0x03), Input.Video },      /*S-video*/
            { (0x03, 0x04), Input.Video },      /*YPrPb*/
            { (0x01, 0x22), Input.DisplayPort },
            { (0x02, 0x22), Input.DisplayPort },/*DP 2*/
            { (0x01, 0x27), Input.HDBaseT },
            { (0x01, 0x28), Input.SDI },
            { (0x02, 0x28), Input.SDI },        /*SDI 2*/
            { (0x03, 0x28), Input.SDI },        /*SDI 3*/
            { (0x04, 0x28), Input.SDI },        /*SDI 4*/
            { (0x01, 0x07), Input.Other },      /*Viewer*/
            { (0x02, 0x07), Input.Other },      /*LAN*/
            { (0x03, 0x06), Input.Other },      /*SLOT*/
            { (0x04, 0x07), Input.Other },      /*Viewer*/
            { (0x05, 0x07), Input.Other },      /*APPS*/
            { (0x01, 0x23), Input.Other }       /*SLOT*/
        };
        private Dictionary<int, Dictionary<int, string?>> ErrorStates = new Dictionary<int, Dictionary<int, string?>>()
        {
            {
                0,
                new Dictionary<int, string?> {
                    { 0x80, "Lamp 1 must be replaced (exceeded maximum hours)" },
                    { 0x40, "Lamp 1 failed to light" },
                    { 0x20, "Power error" },
                    { 0x10, "Fan error" },
                    { 0x08, "Fan error" },
                    { 0x04, null },
                    { 0x02, "Temperature error (bi-metallic strip)" },
                    { 0x01, "Lamp cover error" }
                }
            },
            {
                1,
                new Dictionary<int, string?> {
                    { 0x80, "Refer to extended error status" },
                    { 0x40, null },
                    { 0x20, null },
                    { 0x10, null },
                    { 0x08, null },
                    { 0x04, "Lamp 2 failed to light" },
                    { 0x02, "Formatter error" },
                    { 0x01, "Lamp 1 needs replacing soon"}
                }
            },
            {
                2,
                new Dictionary<int, string?> {
                    { 0x80, "Lamp 2 needs replacing soon" },
                    { 0x40, "Lamp 2 must be replaced (exceeded maximum hours)" },
                    { 0x20, "Mirror cover error" },
                    { 0x10, "Lamp 1 data error" },
                    { 0x08, "Lamp 1 not present" },
                    { 0x04, "Temperature error (sensor)" },
                    { 0x02, "FPGA error" },
                    { 0x01, null }
                }
            },
            {
                3,
                new Dictionary<int, string?> {
                    { 0x80, "The lens is not installed properly" },
                    { 0x40, "Iris calibration error" },
                    { 0x20, "Ballast communication error" },
                    { 0x10, null },
                    { 0x08, "Foreign matter sensor error" },
                    { 0x04, "Temperature error due to dust" },
                    { 0x02, "Lamp 2 data error" },
                    { 0x01, "Lamp 2 not present" }
                }
            },
            {
                8,
                new Dictionary<int, string?> {
                    { 0x80, null },
                    { 0x40, null },
                    { 0x20, null },
                    { 0x10, null },
                    { 0x08, "System error has occurred (formatter)" },
                    { 0x04, "System error has occurred (slave CPU)" },
                    { 0x02, "The interlock switch is open" },
                    { 0x01, "The portrait cover side is up" }
                }
            }
        };
#endregion

#region Constructor
        public NECProjector(string address, int port=7142)
        {
            this.IpAddress = address;
            this.Port = port;
            this.Observers = new List<IObserver<DeviceStatus>>();
        }

        public override async Task Initialize()
        {
            try
            {
                this.Client = await Client.Create(this, IpAddress!, Port);
                
                // call a separate method to get basic info like model, serial #, lamp life
            }
            catch
            {
                throw;
            }
        }

        /* Unused for now */
        /*
        public static async Task<NECProjector> Create(string ip, int port=7142, bool initialize=true)
        {
            try
            {
                logger.Info("Creating new NECProjector instance");
                NECProjector instance = new(ip, port);
                if( initialize )
                    instance.client = await Client.Create(instance, ip, port);
                return instance;
            }
            catch(Exception)
            {
                throw;
            }
        }
        */
#endregion

#region Private methods

        private async Task<Response> GetStatus()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetStatus);
                this.PowerState = Enumeration.FromValue<PowerState>(response.Data[6]);
                var inputTuple = (response.Data[8], response.Data[9]);
                this.InputSelected = InputState.GetValueOrDefault(inputTuple);
                this.VideoMuted = (response.Data[11] == 0x01);
                this.AudioMuted = (response.Data[12] == 0x01);
                
                // get lamp hours if device has a lamp
                // ...

                foreach ( var observer in this.Observers )
                {
                    observer.OnNext(new DeviceStatus
                    {
                        PowerState = this.PowerState,
                        InputSelected = this.InputSelected,
                        VideoMuted = this.VideoMuted,
                        AudioMuted = this.AudioMuted
                    });
                }

                return response;
            }
            catch ( Exception ex )
            {
                Logger.Error(ex);
                throw;
            }
        }

        private async Task<List<string>> GetErrors( bool log = true )
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetErrors);
                List<string> errors = ParseErrors(response);

                if ( errors.Count > 0 && log )
                {
                    string errorString = String.Join(Environment.NewLine, errors);
                    string logString = "Projector errors were reported: " +
                        Environment.NewLine + $"{errorString}";
                    Logger.Warn(logString);
                }
                return errors;
            }
            catch ( Exception ex )
            { 
                Logger.Error(ex);
                throw;
            }
        }

        private List<string> ParseErrors( Response response )
        {
            List<string> errorsReported = new List<string>();
            var relevantBytes = response.Data[5..14];
            foreach ( var outerPair in ErrorStates )
            {
                int byteKey = outerPair.Key;
                Dictionary<int, string?> errorData = outerPair.Value;
                foreach ( var innerPair in errorData )
                {
                    int bitKey = innerPair.Key;
                    string? errorMsg = innerPair.Value;
                    if ( ( relevantBytes[byteKey] & bitKey ) != 0 && errorMsg != null )
                        errorsReported.Add(errorMsg);
                }
            }
            return errorsReported;
        }

        #endregion

        #region Public methods

        public override async Task PowerOn()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOn);
                if ( response.IndicatesFailure )
                    throw new CommandError(response.Data[5], response.Data[6]);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task PowerOff()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOff);
                if ( response.IndicatesFailure )
                    throw new CommandError(response.Data[5], response.Data[6]);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task<Enumeration?> GetPowerState()
        {
            try
            {
                var response = await GetStatus();
                if ( response.IndicatesFailure )
                    throw new CommandError(response.Data[5], response.Data[6]);

                // On success Data[6] holds what we want
                //var powerState = Enumeration.FromValue<PowerState>(response.Data[6]);
                return this.PowerState;
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task SelectInput( object obj )
        {
            try
            {
                Input? input;

                if ( obj is Input i )
                    input = i;
                else if ( obj is string s )
                    input = Enumeration.FromName<Input>(s);
                else
                    throw new ArgumentException($"Invalid argument type {obj.GetType()}");

                var response = await Client!.SendCommandAsync(Command.SelectInput.Prepare(input));

                if ( response.IndicatesFailure )
                    throw new CommandError(response.Data[5], response.Data[6]);
                else
                {
                    // await GetStatus();
                    foreach ( var observer in Observers )
                    {
                        observer.OnNext(new DeviceStatus
                        {
                            InputSelected=input,
                            Message=$"Input '{input}' selected."
                        });
                    }
                }
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task<Enumeration?> GetInputSelection()
        {
            try
            {
                var response = await GetStatus();
                if ( response.IndicatesFailure )
                    throw new CommandError(response.Data[5], response.Data[6]);

                //var tuple = (response.Data[8], response.Data[9]);
                //var inputSelected = InputState.GetValueOrDefault(tuple);
                return this.InputSelected;
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task PowerOnSelectInput( object input )
        {
            try
            {
                bool deviceReady = false;
                string? failureReason = null;

                await PowerOn();
                while ( !deviceReady && failureReason is null )
                {
                    // this is weird and I'm not sure if it works properly in edge cases
                    var state = await GetPowerState() as PowerState;
                    string? stateName = state?.Name;
                    switch ( stateName )
                    {
                        /* This means our query failed, maybe our documentation
                           is out of date? */
                        case null:
                            failureReason = "Failed to get device power state.  Please notify IT.";
                            throw new Exception(failureReason);
                        /* Device started normally (hopefully) and should be
                           ready to accept further commands soon */
                        case nameof(PowerState.Warming):
                        case nameof(PowerState.On):
                            deviceReady = true;
                            break;
                        /* Device may have already been cooling down, or it may
                           have failed to start and gone straight to cooling. */
                        case nameof(PowerState.Cooling):
                            failureReason = "Device is cooling.  Please wait until power cycle is complete.";
                            break;
                        /* Device may have already been in the process of shutting
                           down and has now finished. */
                        case nameof(PowerState.StandbySleep):
                        case nameof(PowerState.StandbyNetwork):
                        case nameof(PowerState.StandbyPowerSaving):
                            failureReason = "Device in standby.  Please wait for power on before input selection.";
                            break;
                        /* Startup failed, likely a lamp or temperature sensor error */
                        case nameof(PowerState.StandbyError):
                            var errors = await GetErrors(log:false);
                            failureReason = "Device reporting error(s):" +
                                Environment.NewLine + string.Join(Environment.NewLine, errors);
                            throw new Exception(failureReason);
                        /* Seems to report this while busy with other commands */
                        case nameof(PowerState.Unknown):
                            failureReason = "Device is busy.  Please wait.";
                            break;
                        /* Initializing, Starting */
                        default:
                            await Task.Delay(1000);
                            continue;
                    }
/*
                    if( state == PowerState.On || state == PowerState.Warming )
                    {
                        break;
                    }
                    else if( state == PowerState.Cooling )
                    {
                        logger.Warn("Device in cooling state.  Please wait until it powers off completely.");
                        failed = true;
                    }
                    else if( state == PowerState.StandbySleep || 
                        state == PowerState.StandbyNetwork ||
                        state == PowerState.StandbyPowerSaving )
                    {
                        logger.Warn("Device in standby.  Please wait for PowerOn before attempting input selection.");
                        failed = true;
                    }
                    else if( state == PowerState.StandbyError )
                    {
                        logger.Warn("Device is in error state.");
                        failed = true;
                        var errors = await GetErrors();
                        logger.Warn(errors);
                        throw new Exception(errors);
                    }
                    else if( state == PowerState.Unknown )
                    {
                        logger.Warn("Device in unknown state.");
                        failed = true;
                    }
*/
                }

                if ( failureReason is not null )
                {
                    Logger.Warn(failureReason);
                }

                await Task.Delay(500);
                await SelectInput(input);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task DisplayMute( bool muted )
        {
            try
            {
                var response = await Client!.SendCommandAsync(muted ? Command.DisplayMuteOn : Command.DisplayMuteOff);
                if ( response.IndicatesFailure )
                    throw new CommandError(response.Data[5], response.Data[6]);
                // await GetStatus();
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override IDisposable Subscribe( IObserver<DeviceStatus> observer )
        {
            if ( !Observers.Contains(observer) )
                Observers.Add(observer);
            return new Unsubscriber(Observers, observer);
        }



        #endregion



    }
}
