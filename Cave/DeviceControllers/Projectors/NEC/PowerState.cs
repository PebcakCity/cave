using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class PowerState 
        : SmartEnum<PowerState>
    {
        public static readonly PowerState StandbySleep = new( 0x00, nameof(StandbySleep) );
        public static readonly PowerState Initializing = new( 0x01, nameof(Initializing) );
        public static readonly PowerState Starting = new( 0x02, nameof(Starting) );
        public static readonly PowerState Warming = new( 0x03, nameof(Warming) );
        public static readonly PowerState On = new( 0x04, nameof(On) );
        public static readonly PowerState Cooling = new( 0x05, nameof(Cooling) );
        public static readonly PowerState StandbyError = new( 0x06, nameof(StandbyError) );
        public static readonly PowerState StandbyPowerSaving = new( 0x0f, nameof(StandbyPowerSaving) );
        public static readonly PowerState StandbyNetwork = new( 0x10, nameof(StandbyNetwork) );
        public static readonly PowerState Unknown = new( 0xff, nameof(Unknown) );

        // Undocumented values discovered during testing.
        // (specifically by trying to power on a M322X that I forgot I had robbed the bulb out of for a classroom)

        // I'm not quite sure what an appropriate name would be as it seems to be in an extended state of figuring its
        // shit out until it finally gives up and decides to shut off, returning to a state of _StandbySleep_ (not
        // StandbyError as one would expect/hope, love all the minor differences in firmware and undocumented things)
        // Maybe call it a "diagnostic mode"?
        public static readonly PowerState RunningDiagnostic = new( 0x09, nameof(RunningDiagnostic) );

        // Before reverting to StandbySleep, it passes through state value 10
        public static readonly PowerState Panic = new( 0x0a, nameof(Panic) );

        // ... and then 14.  I keep testing and discovering more undocumented states.  This appears to be the last one
        // for now?
        public static readonly PowerState ShitTheBed = new( 0x0e, nameof(ShitTheBed) );

        public PowerState( int value, string name ): base(name, value){}
        public static implicit operator byte(PowerState state) => (byte)state.Value;
    }
}
