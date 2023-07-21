using Cave.Utils;

namespace Cave.DeviceControllers.Projectors
{
    public abstract class Projector : Device, IDisplayMutable
    {
        /* IDisplay */
        public virtual Task PowerOn() { throw new NotImplementedException(); }
        public virtual Task PowerOff() { throw new NotImplementedException(); }
        public virtual Task SelectInput( object obj ) { throw new NotImplementedException(); }
        public virtual Task PowerOnSelectInput( object obj ) { throw new NotImplementedException(); }

        /* IDisplayMutable */
        public virtual Task DisplayMute( bool muted ) { throw new NotImplementedException(); }
        public virtual Task<bool> IsDisplayMuted() { throw new NotImplementedException(); }

        /* Projector */
        protected Projector( string deviceName, string address, int port ) 
            : base(deviceName)
        {
            this.Address = address;
            this.Port = port;
        }

        public virtual Task<Enumeration?> GetPowerState() { throw new NotImplementedException(); }
        public virtual Task<Enumeration?> GetInputSelection() { throw new NotImplementedException(); }

        public string Address { get; protected set; }
        public int Port { get; protected set; }
        public List<string>? InputsAvailable { get; protected set; }


        /* Device */
        //public override Task Initialize() { throw new NotImplementedException(); }

        /* IObservable<DeviceStatus> implementation */
        //public override IDisposable Subscribe(IObserver<DeviceStatus> observer) { throw new NotImplementedException(); }
    }
}
