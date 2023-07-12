using System.Text;

using NLog;

using Cave.Utils;
using Cave.src.DeviceControllers.Projectors.NEC;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public partial class NECProjector: Projector
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
        private int LampHoursTotal;
        private int LampHoursUsed;
        private string? ModelNumber;
        private string? SerialNumber;

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
                
                // Get model, serial #, and total lamp life & report back to observers
                await this.GetModelNumber();
                await this.GetSerialNumber();
                await this.GetLampInfo(LampInfo.UsageTimeSeconds);
                await this.GetLampInfo(LampInfo.GoodForSeconds);
                foreach ( var observer in Observers )
                {
                    observer.OnNext(new DeviceStatus
                    {
                        ModelNumber = this.ModelNumber,
                        SerialNumber = this.SerialNumber,
                        LampHoursUsed = this.LampHoursUsed,
                        LampHoursTotal = this.LampHoursTotal
                    });
                }
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

        /**
        * There's a decision to make here regarding how methods like
        * SelectInput, etc handle notifying observers about status updates.
        * 
        * One is by calling GetStatus() and letting it scrape up what changed
        * and pass it to the observers.  The other is by calling observer.OnNext
        * directly with a minimal object containing what changed and a
        * notification message to display.
        *         
        * The data contained in a DeviceStatus instance is intended to be used
        * for databinding purposes, eg. changing a mute button class to active
        * depending on the value of AudioMuted, etc.  The Message property is
        * intended for quick useful notifications to display eg. in a popup.
        * 
        * The disadvantages of the first way are that it involves extra steps
        * (like fetching data a second time, including data that wasn't directly
        * requested like lamp data) and that it doesn't pass a useful message
        * directly to the observers.  That can be fixed by including an optional
        * string message parameter to GetStatus() to be packaged up and sent
        * along with the status data, but then the method should really be
        * renamed to something like "UpdateStatus" to better reflect what it does.
        * 
        * This approach still seems messy and I haven't fully committed to
        * either way yet.  Considering a middle ground for now.  I've separated
        * the GetStatus behavior from the NotifyObservers behavior and given
        * NotifyObservers a message parameter.  An ordinary GetStatus call should
        * update the UI without displaying any notifications, but SelectInput
        * and methods like it can simply update their associated fields and then
        * call NotifyObservers with an appropriate notification message.
        */

        
        /// <summary>
        /// Fetch current device status and store in several private fields
        /// that will be referenced by NotifyObservers
        /// </summary>
        private async Task<Response> GetStatus()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetStatus);
                this.PowerState = Enumeration.FromValue<PowerState>(response.Data[6]);
                var inputTuple = (response.Data[8], response.Data[9]);
                this.InputSelected = InputStates.GetValueOrDefault(inputTuple);
                this.VideoMuted = (response.Data[11] == 0x01);
                this.AudioMuted = (response.Data[12] == 0x01);
                // Get lamp hours if device has a lamp
                await this.GetLampInfo(LampInfo.UsageTimeSeconds);

                NotifyObservers();

                return response;
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetStatus)} :: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Notify subscribers about current device status, passing an optional
        /// message of the given type (Info, Success, Warning, Error).
        /// </summary>
        /// <param name="message">An optional message to display</param>
        /// <param name="type">The type or severity level of the message to display</param>
        private void NotifyObservers(string? message = null, MessageType type = MessageType.Info)
        {
            try
            {
                foreach ( var observer in this.Observers )
                {
                    observer.OnNext(new DeviceStatus
                    {
                        PowerState = this.PowerState,
                        InputSelected = this.InputSelected,
                        VideoMuted = this.VideoMuted,
                        AudioMuted = this.AudioMuted,
                        LampHoursUsed = this.LampHoursUsed,
                        Message = message,
                        MessageType = type
                    });
                }
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(NotifyObservers)} :: {ex}");
                throw;
            }
        }

        private async Task<int> GetLampInfo(LampInfo info)
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetLampInfo.Prepare(0x00, (byte)info));
                if ( response.IndicatesFailure )
                    throw new NECCommandError(response.Data[5], response.Data[6]);

                int value = BitConverter.ToInt32(response.Data[7..11], 0);
                switch ( info )
                {
                    case LampInfo.GoodForSeconds:
                        this.LampHoursTotal = (int)Math.Floor((double)value/3600);
                        break;
                    case LampInfo.UsageTimeSeconds:
                        this.LampHoursUsed = (int)Math.Floor((double)value/3600);
                        break;
                }
                return value;
            }
            catch( NECCommandError )
            {
                LampHoursTotal = LampHoursUsed = -1;
                return -1;
            }
            catch( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetLampInfo)} :: {ex}");
                throw;
            }
        }

        private async Task<string> GetModelNumber()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetModelNumber);
                var data = response.Data[5..37];
                this.ModelNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return this.ModelNumber;
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetModelNumber)} :: {ex}");
                throw;
            }
        }

        private async Task<string> GetSerialNumber()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetSerialNumber);
                var data = response.Data[7..23];
                this.SerialNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return this.SerialNumber;
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetSerialNumber)} :: {ex}");
                throw;
            }
        }

        private async Task<List<string>> GetErrors( bool logErrors = true )
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetErrors);
                List<string> errors = ParseErrors(response);

                if ( errors.Count > 0 && logErrors )
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
                Logger.Error($"NECProjector.{nameof(GetErrors)} :: {ex}");
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

        public override IDisposable Subscribe( IObserver<DeviceStatus> observer )
        {
            if ( !Observers.Contains(observer) )
                Observers.Add(observer);
            return new Unsubscriber(Observers, observer);
        }

        public override async Task PowerOn()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOn);
                if ( response.IndicatesFailure )
                    throw new NECCommandError(response.Data[5], response.Data[6]);
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
                    throw new NECCommandError(response.Data[5], response.Data[6]);
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
                    throw new NECCommandError(response.Data[5], response.Data[6]);

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
                    throw new NECCommandError(response.Data[5], response.Data[6]);

                this.InputSelected = input;
                NotifyObservers($"Input '{input}' selected.", MessageType.Success);
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
                    throw new NECCommandError(response.Data[5], response.Data[6]);

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
                            var errors = await GetErrors(logErrors:false);
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

                await Task.Delay(100);
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
                    throw new NECCommandError(response.Data[5], response.Data[6]);
                
                // await GetStatus();
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task<bool> IsDisplayMuted()
        {
            try
            {
                var response = await GetStatus();
                if ( response.IndicatesFailure )
                    throw new NECCommandError(response.Data[5], response.Data[6]);

                return (this.VideoMuted == true);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

#endregion

    }
}
