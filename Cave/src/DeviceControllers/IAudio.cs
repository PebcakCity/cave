namespace Cave.DeviceControllers
{
    public interface IAudio
    {
        Task VolumeUp();
        Task VolumeDown();
        Task AudioMute( bool muted );
        Task<bool> IsAudioMuted();
    }
}
