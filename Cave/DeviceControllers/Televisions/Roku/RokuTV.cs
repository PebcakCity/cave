using System.IO;
using System.Net.Http;
using System.Text;
using System.Xml;

using NLog;

using Cave.Interfaces;

namespace Cave.DeviceControllers.Televisions.Roku
{
    /// <summary>
    /// A simple controller for Roku TVs using their REST API published here:
    /// https://developer.roku.com/docs/developer-program/dev-tools/external-control-api.md
    /// </summary>
    public class RokuTV : Television, IDebuggable
    {
        private HttpClient? Client = null;
        private static readonly Logger Logger = LogManager.GetLogger("RokuTV");
        private DeviceStatus Status;
        private List<IObserver<DeviceStatus>> Observers;

        /// <summary>
        /// Creates a new <see cref="RokuTV"/> object with the specified
        /// name, IP address, port, and a list of strings representing the
        /// selectable <see cref="Input"/>s available on this device.
        /// </summary>
        /// <param name="deviceName">A name for the device.</param>
        /// <param name="address">IP address of the device.</param>
        /// <param name="port">Port to connect to.  If unspecified defaults
        /// to 8060, the Roku external control protocol (ECP) port.</param>
        /// <param name="inputs">List of strings corresponding to input names
        /// available.  If null, sensible defaults available on most newer
        /// models are chosen.</param>
        public RokuTV(string deviceName, string address, int port=8060, List<string>? inputs = null)
            : base(deviceName, address, port)
        {
            this.Name = deviceName;
            this.Address = address;
            this.Port = port;
            this.Observers = new List<IObserver<DeviceStatus>>();
            this.InputsAvailable = inputs ?? new List<string> { nameof(Input.InputTuner), nameof(Input.InputHDMI1) };
        }

        #region Device methods

        /// <summary>
        /// Tries to connect to a Roku TV at the address and port specified in
        /// the constructor.  If successful, it calls <see cref="GetStatus"/> to
        /// get some basic information from the device.
        /// </summary>
        public override async Task Initialize()
        {
            try
            {
                Client = new HttpClient();
                Client.BaseAddress = new Uri($"http://{this.Address}:{this.Port}");
                await GetStatus();
                Logger.Info("RokuTV Initialized");
            }
            catch ( Exception ex )
            {
                Logger.Error($"RokuTV.{nameof(Initialize)} :: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Subscribes an <see cref="IObserver{T}"/> to this <see cref="IObservable{T}"/>
        /// where <typeparamref name="T"/> is a <see cref="DeviceStatus"/> struct.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns>An <see cref="IDisposable"/> instance allowing the observer to
        /// unsubscribe from this provider.</returns>
        public override IDisposable Subscribe( IObserver<DeviceStatus> observer )
        {
            if ( !Observers.Contains(observer) )
                Observers.Add(observer);
            return new Unsubscriber(Observers, observer);
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Fetches current device state using ECP commands "/query/device-info"
        /// and "/query/media-player" and then publishes state changes to
        /// observers.
        /// </summary>
        /// <returns>A text string consisting of the XML output of the ECP
        /// commands executed, returned for device debugging purposes.</returns>
        private async Task<string> GetStatus()
        {
            try
            {
                CancellationTokenSource cts = new();
                cts.CancelAfter(5000);
                var deviceInfo = await GetDeviceInfo(cts.Token);
                ParseDeviceInfo(deviceInfo);

                cts.CancelAfter(5000);
                var mediaPlayerInfo = await GetMediaPlayerInfo(cts.Token);
                
                NotifyObservers();
                return deviceInfo + "\n" + mediaPlayerInfo;
            }
            catch ( Exception ex )
            {
                Logger.Error($"RokuTV.{nameof(GetStatus)} :: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes the ECP command "/query/device-info".
        /// </summary>
        /// <returns>A text string containing the XML response of the command.
        /// </returns>
        private async Task<string> GetDeviceInfo(CancellationToken token)
        {
            try
            {
                var response = await Client!.GetAsync("/query/device-info", token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(token);
            }
            catch { throw; }
        }

        /// <summary>
        /// Executes the ECP command "/query/media-player".
        /// </summary>
        /// <returns>A text string containing the XML response of the command.
        /// </returns>
        private async Task<string> GetMediaPlayerInfo(CancellationToken token)
        {
            try
            {
                var response = await Client!.GetAsync("/query/media-player", token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(token);
            }
            catch { throw; }
        }

        /// <summary>
        /// Parses the XML response from <see cref="GetDeviceInfo"/> to update
        /// device state.
        /// </summary>
        private void ParseDeviceInfo(string? deviceInfo)
        {
            try
            {
                if( deviceInfo is not null )
                {
                    using MemoryStream stream = new(Encoding.UTF8.GetBytes(deviceInfo));
                    using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings());

                    while ( reader.Read() )
                    {
                        switch ( reader.Name )
                        {
                            case "model-name":
                                Status.ModelNumber = reader.ReadElementContentAsString();
                                break;
                            case "serial-number":
                                Status.SerialNumber = reader.ReadElementContentAsString();
                                break;
                            case "power-mode":
                                PowerState state;
                                if ( PowerState.TryFromName(reader.ReadElementContentAsString(), out state) )
                                    Status.PowerState = state;
                                break;
                            case "friendly-device-name":
                                this.Name = reader.ReadElementContentAsString();
                                break;
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                Logger.Error($"RokuTV.{nameof(ParseDeviceInfo)} :: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Passes all current device state to observers, optionally passing a
        /// message of the given <see cref="DeviceStatus.MessageType">MessageType</see>
        /// (Info, Success, Warning, Error) as well.
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

        /// <summary>
        /// Simulates pressing a key/button on the TV remote.
        /// </summary>
        /// <param name="key">Key/button to press</param>
        /// <param name="token">Cancellation token for cancelling the action</param>
        /// <returns><see cref="HttpResponseMessage"/> containing the status
        /// code and data.</returns>
        /// <exception cref="HttpRequestException"></exception>
        private async Task<HttpResponseMessage> KeyPress( string key, CancellationToken? token = null )
        {
            try
            {
                if ( token == null )
                {
                    CancellationTokenSource cts = new();
                    cts.CancelAfter(5000);
                    token = cts.Token;
                }
                var response = await Client!.PostAsync($"/keypress/{key}", null, (CancellationToken)token);
                if ( !response.IsSuccessStatusCode )
                    throw new HttpRequestException($"Request failed: /keypress/{key}");
                return response;
            }
            catch ( Exception ex )
            {
                Logger.Error($"RokuTV.{nameof(KeyPress)}({nameof(key)}) :: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Television methods

        /// <summary>
        /// On Roku TV, this toggles media playback between play/pause states
        /// </summary>
        public override async Task Play()
        {
            try
            {
                await KeyPress("Play");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Rewinds/reverses media playback
        /// </summary>
        public override async Task Reverse()
        {
            try
            {
                await KeyPress("Rev");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Fast-forwards media playback
        /// </summary>
        public override async Task FastForward()
        {
            try
            {
                await KeyPress("Fwd");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// On the tuner input, selects the next channel
        /// </summary>
        public override async Task ChannelUp()
        {
            try
            {
                await KeyPress("ChannelUp");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// On the tuner input, selects the previous channel
        /// </summary>
        public override async Task ChannelDown()
        {
            try
            {
                await KeyPress("ChannelDown");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Presses the up arrow on the TV remote
        /// </summary>
        public override async Task ArrowUp()
        {
            try
            {
                await KeyPress("Up");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Pressed the down arrow on the TV remote
        /// </summary>
        public override async Task ArrowDown()
        {
            try
            {
                await KeyPress("Down");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Presses the left arrow on the TV remote
        /// </summary>
        public override async Task ArrowLeft()
        {
            try
            {
                await KeyPress("Left");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Presses the right arrow on the TV remote
        /// </summary>
        public override async Task ArrowRight()
        {
            try
            {
                await KeyPress("Right");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Presses the back/return button on the remote to return to the
        /// previous screen/area.  My Roku TV remote has two similar-looking
        /// buttons, one sending the "Back" keypress bearing a left-facing
        /// arrow, and one I've never used that I'm assuming is "InstantReplay"
        /// bearing the typical counter-clockwise rotating arrow used for the
        /// "Return" action on other TV/media player remotes.
        /// </summary>
        public override async Task GoBack()
        {
            try
            {
                await KeyPress("Back");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Presses the "home" button on the remote to get back to the main
        /// interface screen.  (Truth be told, I don't know how common this is
        /// on TVs in general and this method might belong only on this class
        /// instead of the Television abstract class.)  On Roku TV, this button
        /// is labeled with an icon of a house and sends the "Home" keypress.
        /// </summary>
        public override async Task Home()
        {
            try
            {
                await KeyPress("Home");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        #endregion

        #region Interface IDisplay

        /// <summary>
        /// Implements IDisplay.DisplayPowerOn(). Powers on the display.
        /// </summary>
        public override async Task DisplayPowerOn()
        {
            try
            {
                await KeyPress("PowerOn");
                Status.PowerState = PowerState.PowerOn;
                NotifyObservers("Power on", MessageType.Info);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError( ex );
                throw;
            }
        }

        /// <summary>
        /// Implements IDisplay.DisplayPowerOff().  Powers off the display.
        /// </summary>
        public override async Task DisplayPowerOff()
        {
            try
            {
                await KeyPress("PowerOff");
                Status.PowerState = PowerState.DisplayOff;
                NotifyObservers("Power off", MessageType.Info);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        #endregion

        #region Interface IInputSelectable

        /// <summary>
        /// Implements IInputSelectable.SelectInput().  Tries to select the
        /// <see cref="Input"/> on the device matching the given object.
        /// </summary>
        /// <param name="obj"><see cref="Input"/> or <see cref="System.String"/>
        /// matching the <see cref="Input"/> name.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="obj"/>
        /// is neither a string nor <see cref="Input"/>.</exception>
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

                await KeyPress(input);
                Status.InputSelected = input;
                NotifyObservers($"Input '{input}' selected.", MessageType.Success);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        #endregion

        #region Interface IDisplayInputSelectable

        /// <summary>
        /// Implements IDisplayInputSelectable.PowerOnSelectInput().
        /// Powers on the device, waits one second, then tries to select the
        /// <see cref="Input"/> represented by <paramref name="obj"/>.
        /// </summary>
        /// <param name="obj"><see cref="Input"/> or <see cref="System.String"/>
        /// matching the <see cref="Input"/> name.</param>
        public override async Task PowerOnSelectInput( object obj )
        {
            try
            {
                await DisplayPowerOn();
                await Task.Delay(1000);
                await SelectInput(obj);
            }
            catch { throw; }
        }

        #endregion

        #region Interface IAudio

        /// <summary>
        /// Implements IAudio.AudioVolumeUp().  Increases the volume by one.
        /// </summary>
        public override async Task AudioVolumeUp()
        {
            try
            {
                await KeyPress("VolumeUp");
                NotifyObservers("Volume +1", MessageType.Info);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements IAudio.AudioVolumeDown().  Decreases the volume by one.
        /// </summary>
        public override async Task AudioVolumeDown()
        {
            try
            {
                await KeyPress("VolumeDown");
                NotifyObservers("Volume -1", MessageType.Info);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        /// <summary>
        /// Implements IAudio.AudioMute().
        /// Toggles mute on/off.  <paramref name="muted"/> parameter is ignored
        /// for this class (and probably most TVs) as there is no apparent way
        /// to retrieve mute state.
        /// </summary>
        /// <param name="muted">Ignored</param>
        /// <returns></returns>
        public override async Task AudioMute(bool muted)
        {
            try
            {
                await KeyPress("VolumeMute");
                // Without the device reporting the state of audio muting,
                // the best we can do is toggle the state on/off and hope it's right. 50/50
                Status.AudioMuted = !Status.AudioMuted;
                NotifyObservers();
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        #endregion

        #region Interface IDebuggable

        /// <summary>
        /// Calls <see cref="GetStatus"/> to get the XML response of the ECP
        /// commands "/query/device-info" and "/query/media-player" and returns
        /// it for device troubleshooting/debugging purposes. 
        /// </summary>
        /// <returns>A string containing the XML responses.</returns>
        public async Task<string> GetDebugInfo()
        {
            try
            {
                return await GetStatus();
            }
            catch { throw; }
        }

        #endregion

        #region Extras

        /// <summary>
        /// To be used with IDebuggable interface.  Public version of the
        /// helper method by the same name.  For pressing remote buttons
        /// not already hardcoded into the Television interface.
        /// </summary>
        /// <param name="key">Key/button to press</param>
        /// <returns><see cref="HttpResponseMessage"/> containing the status
        /// code and data.</returns>
        public async Task<HttpResponseMessage> KeyPress( string key )
        {
            try
            {
                return await KeyPress(key, null);
            }
            catch { throw; }
        }

        /// <summary>
        /// To be used with IDebuggable interface.  For correcting problems
        /// with misbehaving apps.  Should clear the device's app cache and
        /// then power cycle it.
        /// </summary>
        public async Task ClearCache()
        {
            try
            {
                var keys = new string[] { "Home", "Home", "Home", "Home", "Home",
                                            "Up", "Rev", "Rev", "Fwd", "Fwd" };
                foreach ( var key in keys )
                {
                    await KeyPress(key);
                    await Task.Delay(100);
                }
                NotifyObservers("Clearing your device's cache.  Please wait for it to restart...");
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                Logger.Error(ex);
                throw;
            }
        }

        #endregion
    }
}
