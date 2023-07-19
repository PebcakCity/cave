namespace Cave.DeviceControllers.Televisions
{
    public abstract class Television : Device, IDisplay, IAudio
    {
        /* IDisplay */
        public virtual Task PowerOn() { throw new NotImplementedException(); }
        public virtual Task PowerOff() { throw new NotImplementedException(); }
        public virtual Task SelectInput( object input ) { throw new NotImplementedException(); }
        public virtual Task PowerOnSelectInput( object input ) { throw new NotImplementedException(); }

        /* IAudio */
        public virtual Task VolumeUp() { throw new NotImplementedException(); }
        public virtual Task VolumeDown() { throw new NotImplementedException(); }
        public virtual Task AudioMute( bool muted ) { throw new NotImplementedException(); }
        public virtual Task<bool> IsAudioMuted() { throw new NotImplementedException(); }

        /* Television */
        protected Television(string deviceName, string address, int port) : base(deviceName)
        {
            this.Address = address;
            this.Port = port;
        }

        public string Address { get; protected set; }
        public int Port { get; protected set; }
        public List<string>? InputsAvailable { get; protected set; }


        /* Device */
        //public override Task Initialize() { throw new NotImplementedException(); }

        /* IObservable<DeviceStatus> implementation */
        //public override IDisposable Subscribe(IObserver<DeviceStatus> observer) { throw new NotImplementedException(); }
    }
}
