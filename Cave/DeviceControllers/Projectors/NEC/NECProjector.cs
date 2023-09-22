using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using NLog;

using Cave.Interfaces;
using System.Runtime.InteropServices;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// A controller for projectors manufactured by NEC Display Corporation
    /// (Sharp NEC Display Solutions).
    /// </summary>
    public partial class NECProjector : Projector, IDebuggable
    {
        private INECClient? Client = null;
        private static readonly Logger Logger = LogManager.GetLogger("NECProjector");
        private DeviceInfo Info;
        private List<IObserver<DeviceInfo>> Observers;

        /// <summary>
        /// Creates a new <see cref="NECProjector"/> object with the specified
        /// name, <see cref="DeviceConnectionInfo">connection information
        /// </see>, and an optional list of strings representing the selectable
        /// <see cref="Input"/> terminals available on this device.
        /// After creation, an application must call <see cref="Initialize"/>
        /// on the device to connect to it.  Preferably this should happen
        /// after also subscribing to notifications from this device, so that
        /// the application begins receiving device status updates immediately
        /// after connecting.
        /// </summary>
        /// <param name="deviceName">A name for the device.</param>
        /// <param name="connectionInfo">Connection information for the device.
        /// </param>
        /// <param name="inputs">List of strings corresponding to input names
        /// available.  If null, sensible defaults available on most newer
        /// models are chosen.</param>
        public NECProjector(string deviceName, DeviceConnectionInfo connectionInfo, List<string>? inputs = null)
            :base(deviceName)
        {
            base.Name = deviceName;
            base.ConnectionInfo = connectionInfo;
            base.InputsAvailable = inputs ?? new List<string> { nameof(Input.RGB1), nameof(Input.HDMI1) };
            this.Observers = new List<IObserver<DeviceInfo>>();
        }

        #region Device methods

        /// <summary>
        /// Tries to create a <see cref="INECClient"/> instance and use it to
        /// connect to the device with the <see cref="DeviceConnectionInfo"/>
        /// instance given in the constructor.  If it succeeds, it attempts to
        /// retrieve the model number, serial number, and lamp life information
        /// and then calls <see cref="NotifyObservers"/> to pass that gathered
        /// data to any observers who may already be listening (such as the
        /// application instantiating this device controller.)
        /// </summary>
        public override async Task Initialize()
        {
            try
            {
                if ( ConnectionInfo is NetworkDeviceConnectionInfo networkInfo )
                    this.Client = await SocketClient.Create(networkInfo.IPAddress, networkInfo.Port ?? 7142);
                else if ( ConnectionInfo is SerialDeviceConnectionInfo serialInfo )
                    //throw new NotImplementedException("Clients only currently available for network devices.");
                    this.Client = await SerialClient.Create(serialInfo.SerialPort, serialInfo.Baudrate ?? 38400);

                await GetModelNumber();
                await GetSerialNumber();
                await GetLampInfo(LampInfo.GoodForSeconds);
                await GetLampInfo(LampInfo.UsageTimeSeconds);
                Logger.Info("NECProjector Initialized");

                NotifyObservers();
            }
            catch (Exception ex)
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Subscribes an <see cref="IObserver{T}"/> to this <see cref="IObservable{T}"/>
        /// where <typeparamref name="T"/> is a <see cref="DeviceInfo"/> struct.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns>An <see cref="IDisposable"/> instance allowing the observer to
        /// unsubscribe from this provider.</returns>
        public override IDisposable Subscribe( IObserver<DeviceInfo> observer )
        {
            if ( !Observers.Contains(observer) )
                Observers.Add(observer);
            return new Unsubscriber(Observers, observer);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Handles an exception by logging what method it occurred in and
        /// notifying observers.
        /// </summary>
        /// <param name="ex">The Exception</param>
        /// <param name="methodExOccurredIn">Name of the method
        /// <paramref name="ex"/> occurred in.  Provided automatically by
        /// <see cref="CallerMemberNameAttribute"/></param>
        private void HandleException(Exception ex, [CallerMemberName] string? methodExOccurredIn = null)
        {
            Logger.Error($"{methodExOccurredIn} :: {ex}");
            foreach ( var observer in Observers )
                observer.OnError(ex);
        }

        /// <summary>
        /// Fetches current device state, stores it in our
        /// <see cref="DeviceInfo"/> struct, and calls
        /// <see cref="NotifyObservers"/> to pass a copy of that struct to
        /// observers.
        /// </summary>
        private async Task GetDeviceInfo()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetStatus);
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                Info.PowerState = PowerState.FromValue(response.Data[6]);

                // Temporary PowerState logging... I keep discovering new undocumented values, so I'm trying to get an
                // idea of exactly what the pattern of state changes looks like by logging them one after another.
                Logger.Debug($"{Info.PowerState}");

                var inputTuple = (response.Data[8], response.Data[9]);
                Info.InputSelected = InputStates.GetValueOrDefault(inputTuple);
                Info.IsDisplayMuted = (response.Data[11] == 0x01);
                Info.IsAudioMuted = (response.Data[12] == 0x01);
                // Get lamp hours if device has a lamp
                await GetLampInfo(LampInfo.UsageTimeSeconds);

                NotifyObservers();
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }


        /// <summary>
        /// Passes all gathered device state/info to observers, optionally
        /// passing a message of the given <see cref="MessageType"/>
        /// (Info, Success, Warning, Error) as well.
        /// </summary>
        /// <param name="message">An optional message to display</param>
        /// <param name="type">The type or severity level of the message to display</param>
        private void NotifyObservers(string? message = null, MessageType type = MessageType.Info)
        {
            foreach ( var observer in this.Observers )
            {
                observer.OnNext(Info with {
                    Message = message,
                    MessageType = type
                });
            }
        }

        /// <summary>
        /// Gets the requested lamp information and stores it in our
        /// <see cref="DeviceInfo"/> struct so that <see cref="NotifyObservers"/>
        /// will push lamp info/state to observers. If the command triggers a
        /// <see cref="NECProjectorCommandException"/> (most likely due to the
        /// device being of a lampless design), all lamp information values are
        /// set to -1.
        /// </summary>
        /// <param name="lampInfo"><see cref="LampInfo"/> member corresponding
        /// to the requested data</param>
        /// <returns>The exact value reported by the device as an
        /// <see cref="int"/></returns>
        private async Task<int> GetLampInfo(LampInfo lampInfo)
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetLampInfo.Prepare(0x00, (byte)lampInfo));
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                int value = BitConverter.ToInt32(response.Data[7..11], 0);
                switch ( lampInfo )
                {
                    case LampInfo.GoodForSeconds:
                        Info.LampHoursTotal = (int)Math.Floor((double)value/3600);
                        break;
                    case LampInfo.UsageTimeSeconds:
                        Info.LampHoursUsed = (int)Math.Floor((double)value/3600);
                        break;
                }
                return value;
            }
            catch( NECProjectorCommandException )
            {
                Info.LampHoursTotal = Info.LampHoursUsed = -1;
                return -1;
            }
            catch( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the projector's model number and stores it in our
        /// <see cref="DeviceInfo"/> struct so that <see cref="NotifyObservers"/>
        /// will push model information to observers.
        /// </summary>
        /// <returns>The model number as a string.</returns>
        private async Task<string> GetModelNumber()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetModelNumber);

                /* For some strange reason on certain models, the GetModelNumber command will fail to execute over
                a serial port the first few times it's tried after plugging the projector in and before powering it on.
                You get a "The command cannot be recognized" error.  When you see this failure, try unpluging the power
                cord from the back of the projector, plugging it back in and then turning it on.  Then the command will
                execute normally, whether the projector is powered on or not.  Weird....  Only model I have that it does
                that on is the M322X, others don't seem to have this issue.  Might just be some weird standby issue.

                Other commands affected by this are GetSerialNumber and GetErrors.  Power the projector on and off
                again and all the commands are available.

                Instead of throwing a NECProjectorCommandException here we just return null if the response was not the
                one we were expecting.  This is so that GetDeviceInfo can keep on trucking and run the commands that
                seem to be unaffected by this weird issue (GetStatus, LampInfo, etc).

                So depending on whether any other models are affected by this, seeing a projector in your UI whose model
                and serial info are reported null could indicate that you need to power cycle that machine.
                */
                if ( response.Matches(Response.GetModelInfoSuccess) )
                {
                    var data = response.Data[5..37];
                    Info.ModelNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
                    return Info.ModelNumber;
                }
                return null!;
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the projector's serial number and stores it in our
        /// <see cref="DeviceInfo"/> struct so that <see cref="NotifyObservers"/>
        /// will push it to observers.
        /// </summary>
        /// <returns>The serial number as a string.</returns>
        private async Task<string> GetSerialNumber()
        {
            try
            {
                // Will error out depending on order of plugging everything in.  See GetModelNumber above
                var response = await Client!.SendCommandAsync(Command.GetSerialNumber);
                if ( response.Matches(Response.GetSerialInfoSuccess) )
                {
                    var data = response.Data[7..23];
                    Info.SerialNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
                    return Info.SerialNumber;
                }
                return null!;
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a list of <see cref="NECProjectorException"/> instances representing
        /// the errors this projector is currently reporting.  Optionally logs
        /// those errors as warnings with the logging platform.
        /// </summary>
        /// <param name="logErrors">Whether to log the errors.</param>
        /// <returns>The list of <see cref="NECProjectorException"/>instances reported.</returns>
        private async Task<List<NECProjectorException>> GetErrors( bool logErrors = true )
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.GetErrors);
                var errors = NECProjectorException.GetErrorsFromResponse(response);
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
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Attempts to power on the projector and awaits until either an
        /// operable state is reached, a failure reason is detected, or the
        /// operation times out.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used for
        /// canceling this operation.</param>
        /// <returns>True if device reaches ready state before the operation
        /// is canceled. False if a non-exception throwing reason for failure
        /// is detected, such as the device being in an uninterruptable state
        /// of cooldown when this command is issued.</returns>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation
        /// is requested before completion.</exception>
        /// <exception cref="InvalidOperationException">Thrown if device's
        /// <see cref="PowerState"/> cannot be determined reliably.  This
        /// indicates either a disruption in the connection or a value returned
        /// for the device's power state that lies outside of the documented
        /// range.</exception>
        /// <exception cref="AggregateException">A collection of one or more
        /// <see cref="NECProjectorException"/> instances if projector errors are
        /// detected that would prevent the power on operation from succeeding.
        /// </exception>
        private async Task<bool> AwaitPowerOn( CancellationToken cancellationToken )
        {
            bool deviceReady = false;
            string? failureReason = null;
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOn);
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                while ( !deviceReady && failureReason is null )
                {
                    if ( cancellationToken.IsCancellationRequested )
                        throw new OperationCanceledException("PowerOn operation timed out.");

                    var state = await GetPowerState() as PowerState;
                    NotifyObservers($"Power state: {state}", MessageType.Debug);

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

        #endregion

        #region Projector methods

        /// <summary>
        /// Gets the current <see cref="PowerState"/> of the device.
        /// </summary>
        /// <returns>The <see cref="PowerState"/>.</returns>
        public override async Task<object?> GetPowerState()
        {
            try
            {
                await GetDeviceInfo();
                return Info.PowerState;
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current <see cref="Input"/> selected on the device.
        /// </summary>
        /// <returns>The selected <see cref="Input"/>.</returns>
        public override async Task<object?> GetInputSelection()
        {
            try
            {
                await GetDeviceInfo();
                return Info.InputSelected;
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }


        #endregion

        #region interface IDisplay

        /// <summary>
        /// Implements <see cref="IDisplay.DisplayPowerOn"/>.
        /// Tries to power on the display using a cancellable awaitable power
        /// on operation which reports <see cref="PowerState"/> transitions to
        /// observers until the device is either successfully powered on or
        /// fails to power on.
        /// </summary>
        public override async Task DisplayPowerOn()
        {
            CancellationTokenSource cts = new();
            try
            {
                cts.CancelAfter(120000);
                await AwaitPowerOn(cts.Token);
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
            finally
            {
                cts.Dispose();
            }
        }

        /// <summary>
        /// Implements <see cref="IDisplay.DisplayPowerOff"/>.
        /// Tries to powers off the display.
        /// </summary>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
        public override async Task DisplayPowerOff()
        {
            try
            {
                var response = await Client!.SendCommandAsync(Command.PowerOff);
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        #endregion

        #region interface IDisplayMutable

        /// <summary>
        /// Implements <see cref="IDisplayMutable.DisplayMute"/>.
        /// Tries to set the display muting state of the device to on or off
        /// according to the value of <paramref name="muted"/>.
        /// </summary>
        /// <param name="muted">True to mute the display, false to unmute it.</param>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
        public override async Task DisplayMute( bool muted )
        {
            try
            {
                var response = await Client!.SendCommandAsync(muted ? Command.VideoMuteOn : Command.VideoMuteOff);
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                Info.IsDisplayMuted = muted;
                NotifyObservers(string.Format("Video mute {0}", ( muted ? "ON" : "OFF" )));
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements <see cref="IDisplayMutable.IsDisplayMuted"/>.
        /// Gets whether the device's display is currently muted or not.
        /// </summary>
        /// <returns>True if the display is muted, false if not.</returns>
        public override async Task<bool> IsDisplayMuted()
        {
            try
            {
                await GetDeviceInfo();
                return Info.IsDisplayMuted;
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        #endregion

        #region interface IInputSelectable

        /// <summary>
        /// Implements <see cref="IInputSelectable.SelectInput"/>.
        /// Tries to select the <see cref="Input"/> on the device matching the
        /// given object.
        /// </summary>
        /// <param name="obj"><see cref="Input"/> or <see cref="System.String"/>
        /// matching the <see cref="Input"/> name.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="obj"/>
        /// is neither a <see cref="System.String"/> nor <see cref="Input"/>.
        /// </exception>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
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
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                Info.InputSelected = input;
                NotifyObservers($"Input '{input}' selected.", MessageType.Success);
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        #endregion

        #region interface IDisplayInputSelectable

        /// <summary>
        /// Implements <see cref="IDisplayInputSelectable.PowerOnSelectInput"/>.
        /// Tries to power on the device, waiting until it's in an operable
        /// state, then tries to select the given <see cref="Input"/>.
        /// </summary>
        /// <param name="input"><see cref="Input"/> or <see cref="System.String"/>
        /// matching the <see cref="Input"/> name.</param>
        public override async Task PowerOnSelectInput( object input )
        {
            CancellationTokenSource cts = new();
            try
            {
                // Cancel if it takes longer than 2 minutes
                cts.CancelAfter(120000);
                if ( await AwaitPowerOn(cts.Token) )
                    await SelectInput(input);
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
            finally
            {
                cts.Dispose();
            }
        }

        #endregion

        #region interface IAudio

        /// <summary>
        /// Implements <see cref="IAudio.AudioVolumeUp"/>.
        /// Tries to increase the audio volume by 1 unit.
        /// </summary>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
        public async override Task AudioVolumeUp()
        {
            try
            {   
                // relative adjustment, +2
                var response = await Client!.SendCommandAsync(Command.VolumeAdjust.Prepare(0x01, 0x02, 0x00));
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                NotifyObservers("Volume +2");
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements <see cref="IAudio.AudioVolumeDown"/>.
        /// Tries to decrease the audio volume by 1 unit.
        /// </summary>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
        public async override Task AudioVolumeDown()
        {
            try
            {
                // relative adjustment, -2
                var response = await Client!.SendCommandAsync(Command.VolumeAdjust.Prepare(0x01, 0xfe, 0xff));
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                NotifyObservers("Volume -2");
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements <see cref="IAudio.AudioMute"/>.
        /// Tries to set the audio muting state of the device to on or off
        /// according to the value of <paramref name="muted"/>.
        /// </summary>
        /// <param name="muted">True to mute the audio, false to unmute it.</param>
        /// <exception cref="NECProjectorCommandException">Thrown if the device
        /// fails to execute the command, such as when the device is in a
        /// state which prevents execution of that command.</exception>
        public async override Task AudioMute( bool muted )
        {
            try
            {
                var response = await Client!.SendCommandAsync(muted ? Command.AudioMuteOn : Command.AudioMuteOff);
                if ( response.IndicatesFailure )
                    throw NECProjectorCommandException.CreateNewFromValues(response.Data[5], response.Data[6]);

                Info.IsAudioMuted = muted;
                NotifyObservers(string.Format("Audio mute {0}", ( muted ? "ON" : "OFF" )));
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements <see cref="IAudio.IsAudioMuted"/>.
        /// Gets whether the device's audio is currently muted or not.
        /// </summary>
        /// <returns>True if the audio is muted, false if not.</returns>
        public async override Task<bool> IsAudioMuted()
        {
            try
            {
                await GetDeviceInfo();
                return Info.IsAudioMuted;
            }
            catch ( Exception ex )
            {
                HandleException(ex);
                throw;
            }
        }

        #endregion

        #region interface IDebuggable

        /// <summary>
        /// Implements <see cref="IDebuggable.GetDebugInfo"/>.
        /// Gets a string containing information (device name, address, current
        /// device state, errors reported) that we want to be able to display
        /// in an application for device debugging purposes.
        /// </summary>
        /// <returns>A string containing information about the device and its state.</returns>
        public async Task<string> GetDebugInfo()
        {
            await GetDeviceInfo();
            string debugInfo = string.Empty;
            debugInfo += $"Device name: {this.Name}\n"
                + $"Connection info - {ConnectionInfo}\n"
                + $"Model: {Info.ModelNumber}\n"
                + $"Serial #: {Info.SerialNumber}\n"
                + $"Power state: {Info.PowerState}\n"
                + $"Input selected: {Info.InputSelected}\n";
            debugInfo += "Video mute: " + ( ( Info.IsDisplayMuted==true ) ? "on" : "off" ) + "\n";
            debugInfo += "Audio mute: " + ( ( Info.IsAudioMuted==true ) ? "on" : "off" ) + "\n";

            if ( Info.LampHoursUsed > -1 && Info.LampHoursTotal > 0 )
            {
                int percentRemaining = 100 - (int)Math.Floor((double)Info.LampHoursUsed/(double)Info.LampHoursTotal*100.0);
                debugInfo += $"Lamp hours used: {Info.LampHoursUsed} / {Info.LampHoursTotal} ({percentRemaining}% life remaining)\n";
            }

            var errors = await GetErrors();
            if ( errors.Count > 0 )
            {
                debugInfo += "\nDevice is reporting the following error(s):\n";
                foreach ( var error in errors )
                    debugInfo += error.Message + "\n";
            }
            return debugInfo;
        }

        /// <summary>
        /// Implements <see cref="IDebuggable.GetMethods"/>.
        /// Replaces the default implementation.  Remove's the
        /// <see cref="Device"/>'s <see cref="IObservable{T}.Subscribe"/>
        /// method from the list of methods callable by the debugging interface.
        /// </summary>
        /// <returns></returns>
        List<string> IDebuggable.GetMethods()
        {
            Type thisType = GetType();
            MethodInfo[] methods = thisType.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly
            );
            return methods.Select(method => method.Name)
                .Where(name => !name.Equals("Subscribe")).ToList();
        }

        #endregion
    }
}
