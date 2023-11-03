using Cave.Interfaces;

namespace Cave.DeviceControllers
{
    public abstract class Television : Device, IDisplayInputSelectable, IAudio
    {
        /* IDisplay */
        public virtual Task DisplayPowerOn() { throw new NotImplementedException(); }
        public virtual Task DisplayPowerOff() { throw new NotImplementedException(); }

        /* IInputSelectable */
        public virtual Task SelectInput(object obj) { throw new NotImplementedException(); }

        /* IDisplayInputSelectable */
        public virtual Task PowerOnSelectInput(object obj) { throw new NotImplementedException(); }

        /* IAudio */
        public virtual Task AudioVolumeUp() { throw new NotImplementedException(); }
        public virtual Task AudioVolumeDown() { throw new NotImplementedException(); }
        public virtual Task AudioMuteToggle() { throw new NotImplementedException(); }
        public virtual Task<bool> IsAudioMuted() { throw new NotImplementedException(); }

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
        public virtual Task GoBack() { throw new NotImplementedException(); }
        public virtual Task Home() { throw new NotImplementedException(); }

        protected Television(string deviceName) : base(deviceName) { }

        public List<string>? InputsAvailable { get; protected set; }
    }
}
