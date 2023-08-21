namespace Cave.Interfaces
{
    public interface IAudio
    {
        Task AudioVolumeUp();
        Task AudioVolumeDown();
        Task AudioMute(bool muted);
        Task<bool> AudioIsMuted();
    }
}
