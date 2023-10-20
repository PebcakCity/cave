namespace Cave.Interfaces
{
    public interface IDisplayInputSelectable : IDisplay, IInputSelectable
    {
        Task PowerOnSelectInput(object input);
    }
}
