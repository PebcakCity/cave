namespace Cave.DeviceControllers
{
    public interface IDisplay
    {
        Task DisplayOn();
        Task DisplayOff();
    }
}
