namespace Cave.DeviceControllers
{
    public abstract class Device : IObservable<DeviceStatus>
    {
        public string Name { get; protected set; }
        protected Device() { Name = "Device"; }
        protected Device(string deviceName) : this() { Name = deviceName; }
        public abstract Task Initialize();
        public abstract IDisposable Subscribe(IObserver<DeviceStatus> observer);
    }
}
