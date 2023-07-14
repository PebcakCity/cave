namespace Cave.DeviceControllers
{
    public abstract class DeviceError : Exception { }

    public abstract class DeviceCommandError : DeviceError { }
}
