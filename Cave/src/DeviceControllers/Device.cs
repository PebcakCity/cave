namespace Cave.DeviceControllers
{
/*
 * Dear future self,
 * 
 * I felt I should outline the reasons why I decided that certain fields
 * (ie. Address, Port, InputsAvailable) should be located in the subclasses
 * Projector, Television, etc. even if they appear to be duplicated.
 * I considered putting them all in Device so as to avoid duplication, but
 * decided shortly before committing to relocate them.
 * 
 * Not all devices will need fields corresponding to a string Address and int
 * Port (ex. devices that are exclusively RS232- or relay-controlled that I
 * might add later) and not all of them will need a list of available inputs
 * (ex. digital amplifiers / DSPs that only have audio controls and no
 * selectable/routable inputs of any kind).  The one thing all devices should
 * have is a name.  I might eventually move fields like ModelNumber or
 * SerialNumber up in the hierarchy if it makes sense to do that.
 * 
 */

    public abstract class Device : IObservable<DeviceStatus>
    {
        public string Name { get; protected set; }
        protected Device() { Name = "Device"; }
        protected Device(string deviceName) : this() { Name = deviceName; }
        public abstract Task Initialize();
        public abstract IDisposable Subscribe(IObserver<DeviceStatus> observer);
    }
}
