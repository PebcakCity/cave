namespace Cave.DeviceControllers
{
    public interface IDisplayMutable : IDisplay
    {
        Task DisplayMute( bool muted );
        Task<bool> DisplayIsMuted();
    }
}
