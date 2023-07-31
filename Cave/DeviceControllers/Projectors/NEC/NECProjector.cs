using System.Text;

using NLog;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public partial class NECProjector : Projector
    {
        private Client? Client = null;
        private static readonly Logger Logger = LogManager.GetLogger("NECProjector");
        private DeviceStatus Status;
        private List<IObserver<DeviceStatus>> Observers;

        public NECProjector(string deviceName, string address, int port=7142, List<string>? inputs = null)
            :base(deviceName, address, port)
        {
            this.Name = deviceName;
            this.Address = address;
            this.Port = port;
            this.Observers = new List<IObserver<DeviceStatus>>();
            this.InputsAvailable = inputs ?? new List<string> { nameof(Input.RGB1), nameof(Input.HDMI1) };
        }

        public override async Task Initialize()
        {
            try
            {
                this.Client = await Client.Create(this, Address, Port);
                
                // Get model, serial #, and total lamp life & report back to observers
                await GetModelNumber();
                await GetSerialNumber();
                await GetLampInfo(LampInfo.GoodForSeconds);
                await GetLampInfo(LampInfo.UsageTimeSeconds);

                NotifyObservers();
            }
            catch
            {
                throw;
            }
        }


        /// <summary>
        /// Fetch current device status and notify observers of that status, 
        /// optionally sending these observers a text string summarizing the
        /// status and any errors currently being reported by the device.
        /// </summary>
        /// <param name="appWantsText">Whether the app is requesting text to
        /// update a text view</param>
        public async Task GetStatus(bool appWantsText = false)
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetStatus);
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                Status.PowerState = PowerState.FromValue(response.Data[6]);
                var inputTuple = (response.Data[8], response.Data[9]);
                Status.InputSelected = InputStates.GetValueOrDefault(inputTuple);
                Status.DisplayMuted = (response.Data[11] == 0x01);
                Status.AudioMuted = (response.Data[12] == 0x01);
                // Get lamp hours if device has a lamp
                await GetLampInfo(LampInfo.UsageTimeSeconds);

                if ( appWantsText )
                    NotifyObservers(await GetStatusText());
                else
                    NotifyObservers();
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetStatus)} :: {ex}");
                throw;
            }
        }

        private async Task<string> GetStatusText()
        {
            string message = string.Empty;
            message += $"Device name: {this.Name}\n"
                + $"Power state: {Status.PowerState}\n"
                + $"Input selected: {Status.InputSelected}\n";
            message += "Video mute: " + ( ( Status.DisplayMuted==true ) ? "on" : "off" ) + "\n";
            message += "Audio mute: " + ( ( Status.AudioMuted==true ) ? "on" : "off" ) + "\n";

            if ( Status.LampHoursUsed > -1 && Status.LampHoursTotal > 0 )
            {
                int percentRemaining = 100 - (int)Math.Floor((double)Status.LampHoursUsed/(double)Status.LampHoursTotal);
                message += $"Lamp hours used: {Status.LampHoursUsed} / {Status.LampHoursTotal} ({percentRemaining}% life remaining)\n";
            }

            var errors = await GetErrors();
            if ( errors.Count > 0 )
            {
                message += "\nDevice is reporting the following error(s):\n";
                foreach ( var error in errors )
                    message += error.Message + "\n";
            }
            return message;
        }

        /// <summary>
        /// Notify subscribers about current device status, passing an optional
        /// message of the given type (Info, Success, Warning, Error).
        /// </summary>
        /// <param name="message">An optional message to display</param>
        /// <param name="type">The type or severity level of the message to display</param>
        private void NotifyObservers(string? message = null, MessageType type = MessageType.Info)
        {
            foreach ( var observer in this.Observers )
            {
                observer.OnNext(Status with {
                    Message = message,
                    MessageType = type
                });
            }
        }

        private async Task<int> GetLampInfo(LampInfo info)
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetLampInfo.Prepare(0x00, (byte)info));
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                int value = BitConverter.ToInt32(response.Data[7..11], 0);
                switch ( info )
                {
                    case LampInfo.GoodForSeconds:
                        Status.LampHoursTotal = (int)Math.Floor((double)value/3600);
                        break;
                    case LampInfo.UsageTimeSeconds:
                        Status.LampHoursUsed = (int)Math.Floor((double)value/3600);
                        break;
                }
                return value;
            }
            catch( NECProjectorCommandError )
            {
                Status.LampHoursTotal = Status.LampHoursUsed = -1;
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
                Status.ModelNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return Status.ModelNumber;
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
                Status.SerialNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return Status.SerialNumber;
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetSerialNumber)} :: {ex}");
                throw;
            }
        }



        public async Task<List<NECProjectorError>> GetErrors( bool logErrors = true )
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetErrors);
                var errors = NECProjectorError.GetErrorsFromResponse(response);
                if ( errors.Count > 0 && logErrors )
                {
                    Logger.Warn("Device is reporting the following internal error(s):");
                    foreach ( var error in errors )
                        Logger.Warn(error);
                }
                return errors;
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjector.{nameof(GetErrors)} :: {ex}");
                throw;
            }
        }


        public override IDisposable Subscribe( IObserver<DeviceStatus> observer )
        {
            if ( !Observers.Contains(observer) )
                Observers.Add(observer);
            return new Unsubscriber(Observers, observer);
        }

        // Cancellable awaitable PowerOn

        /// <summary>
        /// Attempts to power on the projector and awaits until either an operable state is reached, a failure reason
        /// is detected, or the operation times out.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used for canceling this operation.</param>
        /// <returns>True if device reaches ready state before the operation is canceled. False if a non-exception
        /// throwing reason for failure is detected.</returns>
        /// <exception cref="OperationCanceledException">If cancellation is requested before completion.</exception>
        /// <exception cref="InvalidOperationException">If device's power state cannot be determined reliably.
        /// </exception>
        /// <exception cref="AggregateException">A collection of one or more NECProjectorError instances if projector
        /// errors are detected that would prevent PowerOn from succeeding.</exception>
        private async Task<bool> AwaitPowerOn(CancellationToken cancellationToken)
        {
            bool deviceReady = false;
            string? failureReason = null;
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOn);
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                while ( !deviceReady && failureReason is null )
                {
                    if ( cancellationToken.IsCancellationRequested )
                        throw new OperationCanceledException("PowerOn operation timed out.");

                    var state = await GetPowerState() as PowerState;

                    if ( state is null )
                        throw new InvalidOperationException("Failed to read device state.  Please notify IT.");

                    else if ( state == PowerState.On || state == PowerState.Warming )
                        deviceReady = true;

                    else if ( state == PowerState.Cooling )
                        failureReason = "Device is cooling.  Please wait until power cycle is complete.";

                    else if ( state == PowerState.StandbySleep ||
                            state == PowerState.StandbyNetwork ||
                            state == PowerState.StandbyPowerSaving )
                        failureReason = "Device in standby.  Please wait for power on before input selection.";

                    else if ( state == PowerState.StandbyError )
                    {
                        var errors = await GetErrors();
                        failureReason = "Device is reporting one or more errors.";
                        throw new AggregateException(failureReason, errors);
                    }

                    else if ( state == PowerState.Unknown )
                        failureReason = "Device is busy.  Please wait.";

                    // Device is initializing, wait a second and check again
                    else
                        await Task.Delay(1000, cancellationToken);
                }

                if ( failureReason is not null )
                    Logger.Warn(failureReason);

                return deviceReady;
            }
            catch
            {
                throw;
            }
        }

        public override async Task DisplayOn()
        {
            try
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(120000);
                await AwaitPowerOn(cts.Token);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task DisplayOff()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOff);
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task<object?> GetPowerState()
        {
            try
            {
                await GetStatus();
                return Status.PowerState;
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
                    input = Input.FromName(s);
                else
                    throw new ArgumentException($"Invalid argument type {obj.GetType()}");

                var response = await Client!.SendCommandAsync(Command.SelectInput.Prepare(input));

                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                Status.InputSelected = input;
                NotifyObservers($"Input '{input}' selected.", MessageType.Success);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task<object?> GetInputSelection()
        {
            try
            {
                await GetStatus();
                return Status.InputSelected;
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
                CancellationTokenSource cts = new();
                // Cancel if it takes longer than 2 minutes
                cts.CancelAfter(120000);
                if ( await AwaitPowerOn(cts.Token) )
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
                var response = await Client!.SendCommandAsync(muted ? Command.VideoMuteOn : Command.VideoMuteOff);
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);
                
                Status.DisplayMuted = muted;
                NotifyObservers(string.Format("Video mute {0}", (muted?"ON":"OFF")));
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
                await GetStatus();
                return Status.DisplayMuted;
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public async override Task VolumeUp()
        {
            try
            {// relative adjustment, +1 volume unit
                var response = await Client!.SendCommandAsync(Command.VolumeAdjust.Prepare(0x01, 0x01, 0x00));
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                NotifyObservers("Volume +1");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }

            //throw new NotImplementedException("This feature is currently unimplemented.");
        }

        /// <summary>
        /// Not really a high priority to get this one working right now.
        /// I might just leave both volume controls unimplemented.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NECProjectorCommandError"></exception>
        public async override Task VolumeDown()
        {
            try
            {// relative adjustment, -1 volume unit
                // tried both one's and two's complement, not sure how to get this to work,
                // VolumeUp seems to work fine
                // tried reversing 2nd and 3rd bytes
                
                var response = await Client!.SendCommandAsync(Command.VolumeAdjust.Prepare(0x01, unchecked((byte)~0x01), 0x00));
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                NotifyObservers("Volume -1 (maybe)");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }

            //throw new NotImplementedException("This feature is currently unimplemented.");
        }

        public async override Task AudioMute( bool muted )
        {
            try
            {
                var response = await Client!.SendCommandAsync(muted ? Command.AudioMuteOn : Command.AudioMuteOff);
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                Status.AudioMuted = muted;
                NotifyObservers(string.Format("Audio mute {0}", ( muted ? "ON" : "OFF" )));
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public async override Task<bool> IsAudioMuted()
        {
            try
            {
                await GetStatus();
                return Status.AudioMuted;
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }
    }
}
