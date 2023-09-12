using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /* Input-related fields */
    public partial class NECProjector : Projector
    {
        /// <summary>
        /// A mapping of input codes reported by "basic information request"
        /// (Appendix p.30) to those used for selecting inputs (Appendix p.18)
        /// </summary>
        private static readonly Dictionary<(int, int), Input> InputStates = new()
        {
            { (0x01, 0x01), Input.RGB1 },
            { (0x02, 0x01), Input.RGB2 },
            { (0x03, 0x01), Input.RGB2 },       /*COMPUTER 3, present on very few models and there are at least 3 different codes for this one */
            { (0x01, 0x06), Input.HDMI1 },
            { (0x01, 0x21), Input.HDMI1 },
            { (0x02, 0x06), Input.HDMI2 },
            { (0x02, 0x21), Input.HDMI2 },
            { (0x01, 0x20), Input.HDMI2 },      /*DVI-D*/
            { (0x01, 0x0a), Input.HDMI2 },      /*Stereo DVI (?)*/
            { (0x01, 0x02), Input.Video },
            { (0x01, 0x03), Input.Video },      /*S-video*/
            { (0x03, 0x04), Input.Video },      /*YPrPb*/
            { (0x01, 0x22), Input.DisplayPort },
            { (0x02, 0x22), Input.DisplayPort },/*DP 2*/
            { (0x01, 0x27), Input.HDBaseT },
            { (0x01, 0x28), Input.SDI },
            { (0x02, 0x28), Input.SDI },        /*SDI 2*/
            { (0x03, 0x28), Input.SDI },        /*SDI 3*/
            { (0x04, 0x28), Input.SDI },        /*SDI 4*/
            { (0x01, 0x07), Input.Other },      /*Viewer*/
            { (0x02, 0x07), Input.Other },      /*LAN*/
            { (0x03, 0x06), Input.Other },      /*SLOT*/
            { (0x04, 0x07), Input.Other },      /*Viewer*/
            { (0x05, 0x07), Input.Other },      /*APPS*/
            { (0x01, 0x23), Input.Other }       /*SLOT*/
        };
    }

    public class Input 
        : SmartEnum<Input>
    {
        public static readonly Input RGB1 = new( 0x01, nameof(RGB1) );
        public static readonly Input RGB2 = new( 0x02, nameof(RGB2) );
        public static readonly Input HDMI1 = new( 0x1a, nameof(HDMI1) );
        public static readonly Input HDMI1Alternate = new( 0xa1, nameof(HDMI1Alternate) );
        public static readonly Input HDMI2 = new( 0x1b, nameof(HDMI2) );
        public static readonly Input HDMI2Alternate = new( 0xa2, nameof(HDMI2Alternate) );
        public static readonly Input Video = new( 0x06, nameof(Video) );
        public static readonly Input DisplayPort = new( 0xa6, nameof(DisplayPort) );
        public static readonly Input HDBaseT = new( 0xbf, nameof(HDBaseT) );
        public static readonly Input HDBaseTAlternate = new( 0x20, nameof(HDBaseTAlternate) );
        public static readonly Input SDI = new( 0xc4, nameof(SDI) );

        /* Have decided to support selecting the LAN/Network input. We have some classrooms where they are using NEC
         * MultiPresenter software to display over the network and I gotta admit it's pretty slick and I'd kinda like
         * to see that in more places.  It can seamlessly switch between PC, Mac, iOS, or Android devices with no
         * cables required except ethernet to the projector.  Plus, with the projectors networked already for control
         * it just makes sense. */
        public static readonly Input Network = new( 0x20, nameof(Network) );

        /* "Other" will still switch to the USB input. */
        public static readonly Input Other = new( 0x1f, nameof(Other) );

        public Input( int value, string name ): base(name, value){}

        public static implicit operator byte(Input input) => (byte)input.Value;
    }
}
