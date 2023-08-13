namespace Cave.DeviceControllers
{
    public interface IDebuggable
    {
        Task<string> GetDebugInfo();
    }
}
