using System.Text;

using NLog;

using Cave.Utils;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public partial class NECProjector : Projector, IAudio
    {
#region Private fields
        private Client? Client = null;
        private static readonly Logger Logger = LogManager.GetLogger("NECProjector");
        private PowerState? PowerState;
        private Input? InputSelected;
        private bool AudioMuted;
        private bool VideoMuted;
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
        public NECProjector(string deviceName, string address, int port=7142, List<string>? inputs = null)
            :base(deviceName, address, port)
        {
            this.Name = deviceName;
            this.Address = address;
            this.Port = port;
            this.Observers = new List<IObserver<DeviceStatus>>();
            this.InputsAvailable = inputs ?? new List<string> { "RGB1", "HDMI1" };
        }

        public override async Task Initialize()
        {
            try
            {
                this.Client = await Client.Create(this, Address, Port);
                
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

#endregion

#region Private methods

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
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

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
            catch( NECProjectorCommandError )
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

        private async Task<List<NECProjectorError>> GetErrors( bool logErrors = true )
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

#endregion

#region Public methods

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

                    var state = await GetPowerState();

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

        public override async Task PowerOn()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOn);
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


        public override async Task PowerOff()
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

        public override async Task<Enumeration?> GetPowerState()
        {
            try
            {
                var response = await GetStatus();
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

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
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

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
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                return this.InputSelected;
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        
        //public override async Task PowerOnSelectInput( object input )
        //{
        //    bool deviceReady = false;
        //    string? failureReason = null;

        //    try
        //    {
        //        await PowerOn();
        //        while ( !deviceReady && failureReason is null )
        //        {
        //            var state = await GetPowerState();

        //            if ( state is null )
        //                throw new InvalidOperationException("Failed to read device state.  Please notify IT.");

        //            else if ( state == PowerState.On || state == PowerState.Warming )
        //                deviceReady = true;

        //            else if ( state == PowerState.Cooling )
        //                failureReason = "Device is cooling.  Please wait until power cycle is complete.";

        //            else if ( state == PowerState.StandbySleep ||
        //                    state == PowerState.StandbyNetwork ||
        //                    state == PowerState.StandbyPowerSaving )
        //                failureReason = "Device in standby.  Please wait for power on before input selection.";

        //            else if ( state == PowerState.StandbyError )
        //            {
        //                var errors = await GetErrors();
        //                failureReason = "Device is reporting one or more errors.";
        //                throw new AggregateException(failureReason, errors);
        //            }

        //            else if ( state == PowerState.Unknown )
        //                failureReason = "Device is busy.  Please wait.";

        //            // Device is initializing, wait a second and try again
        //            else                      
        //                await Task.Delay(1000);
        //        }
        //    }
        //    catch ( Exception ex )
        //    {
        //        foreach ( var observer in Observers )
        //            observer.OnError(ex);
        //        throw;
        //    }
        //    finally
        //    {
        //        if ( failureReason is not null )
        //            Logger.Warn(failureReason);

        //        if ( deviceReady )
        //        {
        //            await Task.Delay(100);
        //            await SelectInput(input);
        //        }
        //    }
        //}

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
                
                this.VideoMuted = muted;
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
                var response = await GetStatus();
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                return this.VideoMuted;
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public async Task VolumeUp()
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

        public async Task VolumeDown()
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

        public async Task AudioMute( bool muted )
        {
            try
            {
                var response = await Client!.SendCommandAsync(muted ? Command.AudioMuteOn : Command.AudioMuteOff);
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                this.AudioMuted = muted;
                NotifyObservers(string.Format("Audio mute {0}", ( muted ? "ON" : "OFF" )));
            }
            catch ( Exception ex )
            {
                foreach ( var observer in Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public async Task<bool> IsAudioMuted()
        {
            try
            {
                var response = await GetStatus();
                if ( response.IndicatesFailure )
                    throw new NECProjectorCommandError(response.Data[5], response.Data[6]);

                return this.AudioMuted;
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
