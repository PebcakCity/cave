namespace Cave.DeviceControllers
{
    public interface IDisplay
    {
        Task DisplayOn();
        Task DisplayOff();

        // Temporarily here until I figure out a better place for it
        // or a better way of doing things?
        Task GetStatus( bool appWantsText );
    }
}
