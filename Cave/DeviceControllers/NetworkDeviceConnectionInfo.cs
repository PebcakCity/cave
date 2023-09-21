namespace Cave.DeviceControllers
{
    public class NetworkDeviceConnectionInfo : DeviceConnectionInfo
    {
        public string IPAddress { get; set; }
        public int? Port { get; set; }

        public NetworkDeviceConnectionInfo(string address, int port)
        {
            IPAddress = address;
            Port = port;
        }

        public override string ToString() { return $"{IPAddress}:{Port}"; }
        public static implicit operator string(NetworkDeviceConnectionInfo info) {  return info.ToString(); }
    }
}
