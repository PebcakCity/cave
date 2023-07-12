namespace Cave.DeviceControllers.Televisions
{
    public abstract class Television : IDisplay, IAudio
    {
        public virtual Task Initialize() { throw new NotImplementedException(); }
        public virtual Task PowerOn() { throw new NotImplementedException(); }
        public virtual Task PowerOff() { throw new NotImplementedException(); }
        public virtual Task SelectInput( object input ) { throw new NotImplementedException(); }
        public virtual Task PowerOnSelectInput( object input ) { throw new NotImplementedException(); }
        public virtual Task VolumeUp() { throw new NotImplementedException(); }
        public virtual Task VolumeDown() { throw new NotImplementedException(); }
        public virtual Task AudioMute( bool muted ) { throw new NotImplementedException(); }
        public virtual Task<bool> IsAudioMuted() { throw new NotImplementedException(); }

        /* IObservable<DeviceStatus> implementation */
        public virtual IDisposable Subscribe(IObserver<DeviceStatus> observer) { throw new NotImplementedException(); }
    }
}
