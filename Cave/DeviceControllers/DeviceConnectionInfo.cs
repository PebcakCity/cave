using System.Text.Json.Serialization;

namespace Cave.DeviceControllers
{
    [JsonDerivedType(typeof(NetworkDeviceConnectionInfo), typeDiscriminator: "network")]
    [JsonDerivedType(typeof(SerialDeviceConnectionInfo), typeDiscriminator: "serial")]
    public abstract class DeviceConnectionInfo
    {
    }
}
