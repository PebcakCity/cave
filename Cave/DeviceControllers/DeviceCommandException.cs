namespace Cave.DeviceControllers
{
    public abstract class DeviceCommandException : DeviceException 
    {
        public DeviceCommandException() { }
        public DeviceCommandException(string message) : base(message) { }
        public DeviceCommandException(string message, Exception innerException)
            : base( message, innerException ) { }
    }
}
