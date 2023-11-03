namespace Cave.Interfaces
{
    public interface IDisplayMutable : IDisplay
    {
        Task DisplayMuteToggle();
        Task<bool> IsDisplayMuted();
    }
}
