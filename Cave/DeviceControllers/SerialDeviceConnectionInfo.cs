using Cave.Interfaces;

namespace Cave.DeviceControllers
{
    public class SerialDeviceConnectionInfo : IDeviceConnectionInfo
    {
        public string SerialPort { get; init; }
        public int Baudrate { get; init; }

        public SerialDeviceConnectionInfo( string port, int baudrate )
        {
            SerialPort = port;
            Baudrate = baudrate;
        }

        public string GetConnectionInfo()
        {
            return $"Serial device: {SerialPort} - Baudrate: {Baudrate}";
        }
        public override string ToString() { return GetConnectionInfo(); }
        public static implicit operator string( SerialDeviceConnectionInfo info ) { return info.ToString(); }
    }
}
