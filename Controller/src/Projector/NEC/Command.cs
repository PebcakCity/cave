using cave.Utils;


namespace cave.Controller.Projector.NEC {

    public class Command: ByteSequence {
        public enum CommandType {
            PowerOn, PowerOff, SelectInput, GetStatus, GetInfo,
            GetLampInfo, GetErrors, GetModel, GetSerial
        }
        
        public CommandType Type { get; init; }

        public Command( CommandType type, byte [] bytes ) : base(bytes) { Type = type; Bytes = bytes; }

        public static Command PowerOn => new( CommandType.PowerOn, new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x02 } );
        public static Command PowerOff => new( CommandType.PowerOff, new byte[] { 0x02, 0x01, 0x00, 0x00, 0x00, 0x03 } );
        public static Command SelectInput => new( CommandType.SelectInput, new byte[] { 0x02, 0x03, 0x00, 0x00, 0x02, 0x01 } );
        public static Command GetStatus => new( CommandType.GetStatus, new byte[] { 0x00, 0xbf, 0x00, 0x00, 0x01, 0x02, 0xc2 } );
        public static Command GetInfo => new( CommandType.GetInfo, new byte[] { 0x03, 0x8a, 0x00, 0x00, 0x00, 0x8d } );   //unused
        public static Command GetLampInfo => new( CommandType.GetLampInfo, new byte[] { 0x03, 0x96, 0x00, 0x00, 0x02 } );
        public static Command GetErrors => new( CommandType.GetErrors, new byte[] { 0x00, 0x88, 0x00, 0x00, 0x00, 0x88 } );
        public static Command GetModel => new( CommandType.GetModel, new byte[] { 0x00, 0x85, 0x00, 0x00, 0x01, 0x04, 0x8a } );
        public static Command GetSerial => new( CommandType.GetSerial, new byte[] { 0x00, 0xbf, 0x00, 0x00, 0x02, 0x01, 0x06, 0xc8 } );

        public override bool Equals( object other ) {
            return (other is Command cmd && cmd.Type == this.Type );
        }
        public override int GetHashCode() {
            return base.GetHashCode();
        }
        public override string ToString() {
            return Type.ToString();
        }
    }

}
