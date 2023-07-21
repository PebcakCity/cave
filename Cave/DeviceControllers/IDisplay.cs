namespace Cave.DeviceControllers
{
    public interface IDisplay
    {
        Task DisplayOn();
        Task DisplayOff();
    }
    
    public interface IDisplayMutable : IDisplay
    {
        Task DisplayMute( bool muted );
        Task<bool> IsDisplayMuted();
    }
}
