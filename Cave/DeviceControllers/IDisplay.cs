namespace Cave.DeviceControllers
{
    public interface IDisplay
    {
        Task DisplayPowerOn();
        Task DisplayPowerOff();
    }
}
