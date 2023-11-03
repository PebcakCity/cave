using Cave.Interfaces;

namespace Cave.DeviceControllers
{
    public abstract class Projector : Device, IDisplayInputSelectable, IDisplayMutable, IAudio
    {
        /* IDisplay */
        public virtual Task DisplayPowerOn() { throw new NotImplementedException(); }
        public virtual Task DisplayPowerOff() { throw new NotImplementedException(); }

        /* IInputSelectable */
        public virtual Task SelectInput(object input) { throw new NotImplementedException(); }

        /* IDisplayInputSelectable */
        public virtual Task PowerOnSelectInput(object obj) { throw new NotImplementedException(); }

        /* IDisplayMutable */
        public virtual Task DisplayMuteToggle() { throw new NotImplementedException(); }
        public virtual Task<bool> IsDisplayMuted() { throw new NotImplementedException(); }

        /* IAudio */
        public virtual Task AudioVolumeUp() { throw new NotImplementedException(); }
        public virtual Task AudioVolumeDown() { throw new NotImplementedException(); }
        public virtual Task AudioMuteToggle() { throw new NotImplementedException(); }
        public virtual Task<bool> IsAudioMuted() { throw new NotImplementedException(); }

        /* Projector */
        public virtual Task<object?> GetPowerState() { throw new NotImplementedException(); }
        public virtual Task<object?> GetInputState() { throw new NotImplementedException(); }

        protected Projector(string deviceName) : base(deviceName) { }

        public List<string>? InputsAvailable { get; protected set; }
    }
}
