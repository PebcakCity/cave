using Cave.Utils;

namespace Cave.DeviceControllers.Projectors
{
    public abstract class Projector : Device, IDisplayMutable, IInputSelectable
    {
        /* IDisplay */
        public virtual Task DisplayOn() { throw new NotImplementedException(); }
        public virtual Task DisplayOff() { throw new NotImplementedException(); }

        /* IInputSelectable */
        public virtual Task SelectInput( object input ) { throw new NotImplementedException(); }

        /* IDisplayMutable */
        public virtual Task DisplayMute( bool muted ) { throw new NotImplementedException(); }
        public virtual Task<bool> IsDisplayMuted() { throw new NotImplementedException(); }

        /* Projector */
        public virtual Task PowerOnSelectInput( object obj ) { throw new NotImplementedException(); }
        public virtual Task<Enumeration?> GetPowerState() { throw new NotImplementedException(); }
        public virtual Task<Enumeration?> GetInputSelection() { throw new NotImplementedException(); }

        protected Projector( string deviceName, string address, int port )
            : base(deviceName)
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
