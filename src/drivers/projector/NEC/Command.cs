using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using cave.utils;


namespace cave.drivers.projector.NEC {

    public class Command: ByteSequence {
        public string Name { get; init; }
        public Command( string name, byte [] bytes ) : base(bytes) { Name = name; Bytes = bytes; }
        public static Command FromBytes( string name, IEnumerable<byte> bytes ) { return new Command( name, bytes.ToArray() ); }
        public static Command PowerOn => new( "PowerOn", new byte [] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x02 } );
        public static Command PowerOff => new( "PowerOff", new byte[] { 0x02, 0x01, 0x00, 0x00, 0x00, 0x03 } );
        public static Command SelectInput => new( "SelectInput", new byte[] { 0x02, 0x03, 0x00, 0x00, 0x02, 0x01 } );
        public static Command GetStatus => new( "GetStatus", new byte[] { 0x00, 0xbf, 0x00, 0x00, 0x01, 0x02, 0xc2 } );
        public static Command GetInfo => new( "GetInfo", new byte[] { 0x03, 0x8a, 0x00, 0x00, 0x00, 0x8d } );   //unused
        public static Command GetLampInfo => new( "GetLampInfo", new byte[] { 0x03, 0x96, 0x00, 0x00, 0x02 } );
        public static Command GetErrors => new( "GetErrors", new byte[] { 0x00, 0x88, 0x00, 0x00, 0x00, 0x88 } );
        public static Command GetModel => new( "GetModel", new byte[] { 0x00, 0x85, 0x00, 0x00, 0x01, 0x04, 0x8a } );
        public static Command GetSerial => new( "GetSerial", new byte[] { 0x00, 0xbf, 0x00, 0x00, 0x02, 0x01, 0x06, 0xc8 } );
        //public string GetCommandNameFromBytes( IEnumerable<byte> bytes )
        public static IEnumerable<T> GetAll<T>() where T : Command =>
            typeof(T).GetFields( BindingFlags.Public | BindingFlags.Static )
                .Select( f => f.GetValue(null) )
                .Cast<T>();
        
    }

}
