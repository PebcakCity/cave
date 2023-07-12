using Cave.Utils;

namespace Cave.DeviceControllers.Projectors
{
    public abstract class Projector: IDisplayMutable
    {
        public virtual Task Initialize() { throw new NotImplementedException(); }
        public virtual Task PowerOn() { throw new NotImplementedException(); }
        public virtual Task PowerOff() { throw new NotImplementedException(); }
        public virtual Task<Enumeration?> GetPowerState() { throw new NotImplementedException(); }
        public virtual Task SelectInput( object obj ) { throw new NotImplementedException(); }
        public virtual Task<Enumeration?> GetInputSelection() { throw new NotImplementedException(); }
        public virtual Task PowerOnSelectInput( object obj ) { throw new NotImplementedException(); }
        public virtual Task DisplayMute( bool muted ) { throw new NotImplementedException(); }
        public virtual Task<bool> IsDisplayMuted() { throw new NotImplementedException(); }

        /* IObservable<DeviceStatus> implementation */
        public virtual IDisposable Subscribe(IObserver<DeviceStatus> observer) { throw new NotImplementedException(); }
    }
}
