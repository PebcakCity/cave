namespace Cave.DeviceControllers
{
    public interface IDisplayInputSelectable : IDisplay, IInputSelectable
    {
        Task PowerOnSelectInput( object input );
    }
}
