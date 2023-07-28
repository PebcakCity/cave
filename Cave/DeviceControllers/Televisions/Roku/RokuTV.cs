using System.IO;
using System.Net.Http;
using System.Text;
using System.Xml;

using NLog;

namespace Cave.DeviceControllers.Televisions.Roku
{
    public class RokuTV : Television
    {
        private HttpClient? Client = null;
        private static readonly Logger Logger = LogManager.GetLogger("RokuTV");
        private PowerState? PowerState;
        private Input? InputSelected;
        private bool AudioMuted;
        private string? ModelNumber;
        private string? SerialNumber;
        private List<IObserver<DeviceStatus>> Observers;

        public RokuTV(string deviceName, string address, int port=8060, List<string>? inputs = null)
            : base(deviceName, address, port)
        {
            this.Name = deviceName;
            this.Address = address;
            this.Port = port;
            this.Observers = new List<IObserver<DeviceStatus>>();
            this.InputsAvailable = inputs ?? new List<string> { nameof(Input.InputTuner), nameof(Input.InputHDMI1) };
        }

        public override async Task Initialize()
        {
            try
            {
                Client = new HttpClient();
                Client.BaseAddress = new Uri($"http://{this.Address}:{this.Port}");
                await GetStatus();
            }
            catch ( Exception ex )
            {
                Logger.Error($"RokuTV.{nameof(Initialize)} :: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// For display testing application
        /// </summary>
        /// <returns>Combined response of /query/device-info and /query/media-player</returns>
        public async Task<string> GetStatus()
        {
            try
            {
                CancellationTokenSource cts = new();
                cts.CancelAfter(5000);
                var deviceInfo = await GetDeviceInfo(cts.Token);
                ParseDeviceInfo(deviceInfo);

                cts.CancelAfter(5000);
                var mediaPlayerInfo = await GetMediaPlayerInfo(cts.Token);

                var status = deviceInfo + mediaPlayerInfo;

                foreach ( var observer in this.Observers )
                {
                    observer.OnNext(new DeviceStatus
                    {
                        ModelNumber = this.ModelNumber,
                        SerialNumber = this.SerialNumber,
                        PowerState = this.PowerState,
                        Message = status,
                        MessageType = MessageType.Info
                    });
                }
                return status;
            }
            catch { throw; }
        }

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
                                this.ModelNumber = reader.ReadElementContentAsString();
                                break;
                            case "serial_number":
                                this.SerialNumber = reader.ReadElementContentAsString();
                                break;
                            case "power-mode":
                                PowerState.TryFromName(reader.ReadElementContentAsString(), out this.PowerState);
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

        private void NotifyObservers(string? message = null, MessageType type = MessageType.Info)
        {
            foreach ( var observer in this.Observers )
            {
                observer.OnNext(new DeviceStatus
                {
                    PowerState = this.PowerState,
                    InputSelected = this.InputSelected,
                    // AudioMuted = this.AudioMuted, /* Won't be easy to track, not seeing this under device-info */
                    Message = message,
                    MessageType = type
                });
            }
        }

        public override IDisposable Subscribe( IObserver<DeviceStatus> observer )
        {
            if ( ! Observers.Contains( observer ) )
                Observers.Add( observer );
            return new Unsubscriber(Observers, observer);
        }

        /**
         * Seems to me, maybe I should just make KeyPress public instead and
         * call it directly from the app?  Everything is a keypress.
         */

        private async Task<HttpResponseMessage> KeyPress(string key, CancellationToken? token = null )
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

        public override async Task DisplayOn()
        {
            try
            {
                await KeyPress("PowerOn");
                this.PowerState = PowerState.PowerOn;
                NotifyObservers("Power on", MessageType.Info);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError( ex );
                throw;
            }
        }

        public override async Task DisplayOff()
        {
            try
            {
                await KeyPress("PowerOff");
                this.PowerState = PowerState.DisplayOff;
                NotifyObservers("Power off", MessageType.Info);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
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

                await KeyPress(input);
                this.InputSelected = input;
                NotifyObservers($"Input '{input}' selected.", MessageType.Success);
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

        public override async Task PowerOnSelectInput( object obj )
        {
            try
            {
                await DisplayOn();
                await Task.Delay(1000);
                await SelectInput(obj);
            }
            catch { throw; }
        }

        public override async Task VolumeUp()
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

        public override async Task VolumeDown()
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
        /// Toggles mute on/off.  {muted} parameter is ignored.
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
                this.AudioMuted = !this.AudioMuted;
                NotifyObservers();
            }
            catch ( Exception ex )
            {
                foreach ( var observer in this.Observers )
                    observer.OnError(ex);
                throw;
            }
        }

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
    }
}
