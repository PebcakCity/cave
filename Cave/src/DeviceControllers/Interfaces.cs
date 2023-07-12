namespace Cave.DeviceControllers
{
    public interface IDevice: IObservable<DeviceStatus>
    {
        Task Initialize();
    }

    public interface IDisplay: IDevice
    {
        Task PowerOn();
        Task PowerOff();
        Task SelectInput( object input );
        Task PowerOnSelectInput( object input );
    }

    public interface IDisplayMutable : IDisplay
    {
        Task DisplayMute( bool muted );
        Task<bool> IsDisplayMuted();
    }

    public interface IAudio: IDevice
    {
        Task VolumeUp();
        Task VolumeDown();
        Task AudioMute( bool muted );
        Task<bool> IsAudioMuted();
    }
}
