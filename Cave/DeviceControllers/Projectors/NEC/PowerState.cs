using Cave.Utils;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class PowerState: Enumeration
    {
        public static PowerState StandbySleep = new( 0x00, nameof(StandbySleep) );
        public static PowerState Initializing = new( 0x01, nameof(Initializing) );
        public static PowerState Starting = new( 0x02, nameof(Starting) );
        public static PowerState Warming = new( 0x03, nameof(Warming) );
        public static PowerState On = new( 0x04, nameof(On) );
        public static PowerState Cooling = new( 0x05, nameof(Cooling) );
        public static PowerState StandbyError = new( 0x06, nameof(StandbyError) );
        public static PowerState StandbyPowerSaving = new( 0x0f, nameof(StandbyPowerSaving) );
        public static PowerState StandbyNetwork = new( 0x10, nameof(StandbyNetwork) );
        public static PowerState Unknown = new( 0xff, nameof(Unknown) );
        public PowerState( int id, string name ): base(id, name){}
        public static implicit operator byte(PowerState state) => (byte)state.Id;
    }
}
