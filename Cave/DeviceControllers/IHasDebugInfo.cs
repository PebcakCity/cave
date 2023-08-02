namespace Cave.DeviceControllers
{
    public interface IHasDebugInfo
    {
        Task<string> GetDebugInfo();
    }
}
