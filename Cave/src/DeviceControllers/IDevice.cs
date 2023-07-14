namespace Cave.DeviceControllers
{
    public interface IDevice : IObservable<DeviceStatus>
    {
        Task Initialize();
    }
}
