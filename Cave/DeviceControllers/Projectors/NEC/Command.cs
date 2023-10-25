using Ardalis.SmartEnum;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// Represents an NEC projector command.  If the command has parameters, they are appended to the end of the command
    /// using the <see cref="Prepare"/> method.  This method also appends a checksum to the command that must be present
    /// for successful communication with the device.
    /// 
    /// For future versions:
    /// If I were to redo this, I'd like to have the parameters as an embedded IEnumerable so that they can be easily
    /// logged.  I might be able to extend it instead... Only 2 commands really have parameters.  I'd like to be able to
    /// say Logger.Debug($"Sending: {command}") and see something like
    /// Sending: [Command.SelectInput(Input.HDMI1)] [0x02 0x03 ...]
    /// </summary>
    public class Command 
        : SmartEnum<Command>
    {
        private readonly List<byte> _data;

        public Command( int value, string name, List<byte> data ) : base(name, value) { _data = data; }

        public List<byte> Data => _data;

        public Command( Command original )
            : base(original.Name, original.Value)
        {
            this._data = new List<byte>(original.Data);
        }

        public Command Append( byte b )
        {
            this.Data.Add(b);
            return this;
        }

        public static Command operator +( Command command, byte b )
        {
            command.Data.Add(b);
            return command;
        }

        public Command Prepare( params byte[] args )
        {
            int argsAppended = 0;
            Command copy = new(this);
            foreach( byte b in args )
            {
                copy.Append(b);
                ++argsAppended;
            }
            if( argsAppended > 0 )
                copy.Append( copy.Checksum() );
            return copy;
        }

        public Command Prepare( string input )
        {
            try
            {
                Input inputMember = Input.FromName(input);
                return Prepare(inputMember);
            }
            catch
            { throw; }
        }

        private byte Checksum()
        {
            byte total = 0x00;
            foreach( byte b in this.Data )
                total += b;
            return (byte)(total & 0xFF);
        }

        public override string ToString()
        {
            string data = "";
            for( int idx = 0; idx < Data.Count; ++idx )
            {
                data += string.Format("0x{0:x2}", Data[idx]);
                if( idx < Data.Count - 1 )
                    data += " ";
            }
            return $"Command.{Name}: [{data}]";
        }

        public static readonly Command PowerOn = new( 1, nameof(PowerOn), new List<byte> { 0x02, 0x00, 0x00, 0x00, 0x00, 0x02 } );
        public static readonly Command PowerOff = new( 2, nameof(PowerOff), new List<byte> { 0x02, 0x01, 0x00, 0x00, 0x00, 0x03 } );
        public static readonly Command SelectInput = new( 3, nameof(SelectInput), new List<byte> { 0x02, 0x03, 0x00, 0x00, 0x02, 0x01 } );
        public static readonly Command VideoMuteOn = new( 4, nameof(VideoMuteOn), new List<byte> { 0x02, 0x10, 0x00, 0x00, 0x00, 0x12 } ) ;
        public static readonly Command VideoMuteOff = new( 5, nameof(VideoMuteOff), new List<byte> { 0x02, 0x11, 0x00, 0x00, 0x00, 0x13 } );
        public static readonly Command AudioMuteOn = new( 6, nameof(AudioMuteOn), new List<byte> { 0x02, 0x12, 0x00, 0x00, 0x00, 0x14 } );
        public static readonly Command AudioMuteOff = new( 7, nameof(AudioMuteOff), new List<byte> { 0x02, 0x13, 0x00, 0x00, 0x00, 0x15 } );
        public static readonly Command VolumeAdjust = new( 8, nameof(VolumeAdjust), new List<byte> { 0x03, 0x10, 0x00, 0x00, 0x05, 0x05, 0x00 } );
        public static readonly Command GetStatus = new( 9, nameof(GetStatus), new List<byte> { 0x00, 0xbf, 0x00, 0x00, 0x01, 0x02, 0xc2 } );
        public static readonly Command GetInfo = new( 10, nameof(GetInfo), new List<byte> { 0x03, 0x8a, 0x00, 0x00, 0x00, 0x8d } );
        public static readonly Command GetLampInfo = new( 11, nameof(GetLampInfo), new List<byte> { 0x03, 0x96, 0x00, 0x00, 0x02 } );
        public static readonly Command GetErrors = new( 12, nameof(GetErrors), new List<byte> { 0x00, 0x88, 0x00, 0x00, 0x00, 0x88 } );
        public static readonly Command GetModelNumber = new( 13, nameof(GetModelNumber), new List<byte> { 0x00, 0x85, 0x00, 0x00, 0x01, 0x04, 0x8a } );
        public static readonly Command GetSerialNumber = new( 14, nameof(GetSerialNumber), new List<byte> { 0x00, 0xbf, 0x00, 0x00, 0x02, 0x01, 0x06, 0xc8 } );

        public static readonly Dictionary<Command, int> SuccessResponseLengths = new()
        {
            { PowerOn, 6 },
            { PowerOff, 6 },
            { SelectInput, 7 },
            { GetStatus, 22 },
            { GetInfo, 104 },
            { GetLampInfo, 12 },
            { GetErrors, 18 },
            { GetModelNumber, 38 },
            { GetSerialNumber, 24 },
            { VideoMuteOn, 6 },
            { VideoMuteOff, 6 },
            { AudioMuteOn, 6 },
            { AudioMuteOff, 6 },
            { VolumeAdjust, 8 },
        };

        // pretty much universally 8 bytes but allowing possibility of future variance
        public static readonly Dictionary<Command, int> FailureResponseLengths = new()
        {
            { PowerOn, 8 },
            { PowerOff, 8 },
            { SelectInput, 8 },
            { GetStatus, 8 },
            { GetInfo, 8 },
            { GetLampInfo, 8 },
            { GetErrors, 8 },
            { GetModelNumber, 8 },
            { GetSerialNumber, 8 },
            { VideoMuteOn, 8 },
            { VideoMuteOff, 8 },
            { AudioMuteOn, 8 },
            { AudioMuteOff, 8 },
            { VolumeAdjust, 8 },
        };
    }
}
