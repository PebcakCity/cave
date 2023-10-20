namespace Cave.Interfaces
{
    public interface IDisplayMutable : IDisplay
    {
        Task DisplayMute(bool muted);
        Task<bool> IsDisplayMuted();
    }
}
