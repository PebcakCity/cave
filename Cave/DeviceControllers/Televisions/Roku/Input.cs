using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Televisions.Roku
{
    public class Input
        : SmartEnum<Input>
    {
        public static readonly Input InputTuner = new(0, nameof(InputTuner));
        public static readonly Input InputHDMI1 = new(1, nameof(InputHDMI1));
        public static readonly Input InputHDMI2 = new(2, nameof(InputHDMI2));
        public static readonly Input InputHDMI3 = new(3, nameof(InputHDMI3));
        public static readonly Input InputAV1 = new(4, nameof(InputAV1));
        public Input(int value, string name) : base(name, value) { }
        public static implicit operator string(Input input) => input.Name;
    }
}
