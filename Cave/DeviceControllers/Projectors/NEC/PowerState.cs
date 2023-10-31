using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class PowerState 
        : SmartEnum<PowerState>
    {
        // Manual p. 83
        public static readonly PowerState StandbySleep = new( 0x00, nameof(StandbySleep) );
        public static readonly PowerState On = new( 0x04, nameof(On) );
        public static readonly PowerState Cooling = new( 0x05, nameof(Cooling) );
        public static readonly PowerState StandbyError = new( 0x06, nameof(StandbyError) );
        public static readonly PowerState StandbyPowerSaving = new( 0x0f, nameof(StandbyPowerSaving) );
        public static readonly PowerState StandbyNetwork = new( 0x10, nameof(StandbyNetwork) );

        // Undocumented values seen during testing.  Could be more unknowns.  Guessing as to their meanings...

        public static readonly PowerState Initializing = new( 0x01, nameof(Initializing) );
        public static readonly PowerState Starting = new( 0x02, nameof(Starting) );
        public static readonly PowerState Warming = new( 0x03, nameof(Warming) );
        public static readonly PowerState Unknown = new( 0xff, nameof(Unknown) );

        // Trying to power on one model without its lamp, it seems to alternate between Starting and RunningDiagnostic,
        // then briefly hits Panic, then StartupFailure, before finally settling back to StandbySleep.

        public static readonly PowerState RunningDiagnostic = new( 0x09, nameof(RunningDiagnostic) );
        public static readonly PowerState Panic = new( 0x0a, nameof(Panic) );
        public static readonly PowerState StartupFailure = new( 0x0e, nameof(StartupFailure) );

        public PowerState( int value, string name ): base(name, value){}
        public static implicit operator byte(PowerState state) => (byte)state.Value;
    }
}
