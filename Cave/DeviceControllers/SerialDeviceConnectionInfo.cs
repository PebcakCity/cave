namespace Cave.DeviceControllers
{
    public class SerialDeviceConnectionInfo : DeviceConnectionInfo
    {
        public string SerialPort { get; set; }
        public int? Baudrate { get; set; }

        public SerialDeviceConnectionInfo( string port, int baudrate )
        {
            SerialPort = port;
            Baudrate = baudrate;
        }

        public override string ToString() { return $"Serial port: {SerialPort} - Baudrate: {Baudrate}"; }
        public static implicit operator string( SerialDeviceConnectionInfo info ) { return info.ToString(); }
    }
}
