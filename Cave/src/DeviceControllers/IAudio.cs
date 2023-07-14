namespace Cave.DeviceControllers
{
    public interface IAudio: IDevice
    {
        Task VolumeUp();
        Task VolumeDown();
        Task AudioMute( bool muted );
        Task<bool> IsAudioMuted();
    }
}
