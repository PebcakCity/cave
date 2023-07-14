namespace Cave.DeviceControllers
{
    public interface IDisplay : IDevice
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
}
