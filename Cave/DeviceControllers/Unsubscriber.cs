namespace Cave.DeviceControllers
{
    /// <summary>
    /// Mechanism for allowing an observer to stop receiving notifications
    /// from a provider.
    /// </summary>
    public class Unsubscriber : IDisposable
    {
        private List<IObserver<DeviceInfo>> observers;
        private IObserver<DeviceInfo> observer;
        public Unsubscriber(List<IObserver<DeviceInfo>> observers, IObserver<DeviceInfo> observer)
        {
            this.observers = observers;
            this.observer = observer;
        }
        public void Dispose()
        {
            if (observer != null && observers.Contains(observer))
                observers.Remove(observer);
        }
    }
}
