namespace Cave.DeviceControllers
{
    /// <summary>
    /// Mechanism for allowing an observer to stop receiving notifications
    /// from a provider.
    /// </summary>
    public class Unsubscriber : IDisposable
    {
        private List<IObserver<DeviceStatus>> observers;
        private IObserver<DeviceStatus> observer;
        public Unsubscriber(List<IObserver<DeviceStatus>> observers, IObserver<DeviceStatus> observer)
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
