using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Televisions.Roku
{
    public class PowerState 
        : SmartEnum<PowerState>
    {
        public static readonly PowerState DisplayOff = new(0, nameof(DisplayOff));
        public static readonly PowerState PowerOn = new(1, nameof(PowerOn));
        public PowerState( int value, string name ) : base(name, value) { }
    }
}
