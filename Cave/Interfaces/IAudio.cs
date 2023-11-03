namespace Cave.Interfaces
{
    public interface IAudio
    {
        Task AudioVolumeUp();
        Task AudioVolumeDown();
        Task AudioMuteToggle();
        Task<bool> IsAudioMuted();
    }
}
