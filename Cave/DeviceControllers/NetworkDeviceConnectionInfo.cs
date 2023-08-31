using Cave.Interfaces;

namespace Cave.DeviceControllers
{
    public class NetworkDeviceConnectionInfo : IDeviceConnectionInfo
    {
        public string IPAddress { get; init; }
        public int Port { get; init; }

        public NetworkDeviceConnectionInfo(string address, int port)
        {
            IPAddress = address;
            Port = port;
        }

        public string GetConnectionInfo()
        {
            return $"{IPAddress}:{Port}";
        }
        public override string ToString() { return GetConnectionInfo(); }
        public static implicit operator string(NetworkDeviceConnectionInfo info) {  return info.ToString(); }
    }
}
