namespace Cave.DeviceControllers.Projectors.NEC
{
    public interface INECClient
    {
        Task<Response> SendCommandAsync( Command toSend );
    }
}
