using System;

using Cave.Utils;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class Command: Enumeration
    {
        public List<byte> Data { get; protected set; }

        public static Command PowerOn = new( 1, nameof(PowerOn), new List<byte> { 0x02, 0x00, 0x00, 0x00, 0x00, 0x02 } );
        public static Command PowerOff = new( 2, nameof(PowerOff), new List<byte> { 0x02, 0x01, 0x00, 0x00, 0x00, 0x03 } );
        public static Command SelectInput = new( 3, nameof(SelectInput), new List<byte> { 0x02, 0x03, 0x00, 0x00, 0x02, 0x01 } );
        public static Command VideoMuteOn = new( 4, nameof(VideoMuteOn), new List<byte> { 0x02, 0x10, 0x00, 0x00, 0x00, 0x12 } ) ;
        public static Command VideoMuteOff = new( 5, nameof(VideoMuteOff), new List<byte> { 0x02, 0x11, 0x00, 0x00, 0x00, 0x13 } );
        public static Command AudioMuteOn = new( 6, nameof(AudioMuteOn), new List<byte> { 0x02, 0x12, 0x00, 0x00, 0x00, 0x14 } );
        public static Command AudioMuteOff = new( 7, nameof(AudioMuteOff), new List<byte> { 0x02, 0x13, 0x00, 0x00, 0x00, 0x15 } );
        public static Command GetStatus = new( 8, nameof(GetStatus), new List<byte> { 0x00, 0xbf, 0x00, 0x00, 0x01, 0x02, 0xc2 } );
        public static Command GetInfo = new( 9, nameof(GetInfo), new List<byte> { 0x03, 0x8a, 0x00, 0x00, 0x00, 0x8d } );
        public static Command GetLampInfo = new( 10, nameof(GetLampInfo), new List<byte> { 0x03, 0x96, 0x00, 0x00, 0x02 } );
        public static Command GetErrors = new( 11, nameof(GetErrors), new List<byte> { 0x00, 0x88, 0x00, 0x00, 0x00, 0x88 } );
        public static Command GetModelNumber = new( 12, nameof(GetModelNumber), new List<byte> { 0x00, 0x85, 0x00, 0x00, 0x01, 0x04, 0x8a } );
        public static Command GetSerialNumber = new( 13, nameof(GetSerialNumber), new List<byte> { 0x00, 0xbf, 0x00, 0x00, 0x02, 0x01, 0x06, 0xc8 } );
        public Command( int id, string name, List<byte> data ) : base(id, name) { Data = data; }

        public Command( Command original ): base(original)
        {
            Data = new List<byte>(original.Data);
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

        /// <summary>
        /// Special case for selecting an input by name
        /// </summary>
        public Command Prepare( string input )
        {
            try
            {
                Input inputMember = Input.FromName<Input>(input);
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
    }
}
