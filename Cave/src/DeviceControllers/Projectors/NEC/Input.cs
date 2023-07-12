using Cave.Utils;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class Input: Enumeration
    {
        public static Input RGB1 = new( 0x01, nameof(RGB1) );
        public static Input RGB2 = new( 0x02, nameof(RGB2) );
        public static Input HDMI1 = new( 0x1a, nameof(HDMI1) );
        public static Input HDMI1Alternate = new( 0xa1, nameof(HDMI1Alternate) );
        public static Input HDMI2 = new( 0x1b, nameof(HDMI2) );
        public static Input HDMI2Alternate = new( 0xa2, nameof(HDMI2Alternate) );
        public static Input Video = new( 0x06, nameof(Video) );
        public static Input DisplayPort = new( 0xa6, nameof(DisplayPort) );
        public static Input HDBaseT = new( 0xbf, nameof(HDBaseT) );
        public static Input HDBaseTAlternate = new( 0x20, nameof(HDBaseTAlternate) );
        public static Input SDI = new( 0xc4, nameof(SDI) );
        /* For mapping between NEC.InputState dictionary and Input enum type:
            We don't really care about accurately reporting exactly which input we're on as it depends on the specific model.
            We care about being able to accurately select an input, specifically one of the above, and even more specifically RGB and HDMI.
            The code '0x1f' corresponds to the USB A input, which is present on most models and serves as a stand-in for inputs like
            USB, LAN, viewer, "APPS" (whatever that is), and cardslot inputs, which we don't intend to support selecting. */
        public static Input Other = new( 0x1f, nameof(Other) );

        public Input( int id, string name ): base(id, name){}

        public static implicit operator byte(Input input) => (byte)input.Id;
    }
}
