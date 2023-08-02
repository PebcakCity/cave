namespace Cave.DeviceControllers.Televisions
{
    public abstract class Television : Device, IDisplayInputSelectable, IAudio
    {
        /* IDisplay */
        public virtual Task DisplayPowerOn() { throw new NotImplementedException(); }
        public virtual Task DisplayPowerOff() { throw new NotImplementedException(); }

        /* IInputSelectable */
        public virtual Task SelectInput( object obj ) { throw new NotImplementedException(); }

        /* IDisplayInputSelectable */
        public virtual Task PowerOnSelectInput( object obj ) { throw new NotImplementedException(); }

        /* IAudio */
        public virtual Task AudioVolumeUp() { throw new NotImplementedException(); }
        public virtual Task AudioVolumeDown() { throw new NotImplementedException(); }
        public virtual Task AudioMute( bool muted ) { throw new NotImplementedException(); }
        public virtual Task<bool> AudioIsMuted() { throw new NotImplementedException(); }

        /* Television */
        public virtual Task Play() { throw new NotImplementedException(); }
        public virtual Task Reverse() { throw new NotImplementedException(); }
        public virtual Task FastForward() { throw new NotImplementedException(); }
        public virtual Task ChannelUp() { throw new NotImplementedException(); }
        public virtual Task ChannelDown() { throw new NotImplementedException(); }
        public virtual Task ArrowUp() { throw new NotImplementedException(); }
        public virtual Task ArrowDown() { throw new NotImplementedException(); }
        public virtual Task ArrowLeft() { throw new NotImplementedException(); }
        public virtual Task ArrowRight() { throw new NotImplementedException(); }
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
