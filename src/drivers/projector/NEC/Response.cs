using System;
using System.Collections.Generic;
using System.Linq;

using cave.utils;


namespace cave.drivers.projector.NEC {

    public class ResponseEventArgs: EventArgs {
        public Response Response { get; set; }
        public Command Command { get; set; } = null;

        public ResponseEventArgs(
            Response r,     // Response from the device
            Command c       // Command that was sent that (likely) generated this response
        ) {
            Response = r;
            Command = c;
        }
    }

    public class Response: ByteSequence {
        private static byte any = (byte)'*';

        /* Success responses */
        public static Response PowerOnSuccess => new( new byte[]{ 0x22, 0x00, any, any, 0x00, any } );
        public static Response PowerOffSuccess => new( new byte[]{ 0x22, 0x01, any, any, 0x00, any } );
        public static Response SelectInputSuccess => new( new byte[]{ 0x22, 0x03, any, any, 0x01, any, any } );
        public static Response GetStatusSuccess => new( new byte[] { 0x20, 0xbf, any, any, 0x10, 0x02, 
        /*  Power       Content displayed       Input selected (tuple)      Video signal type */
            any,        any,                    any, any,                   any,
        /*  Video mute  Sound mute      Onscreen mute   Freeze status   System reserved */
            any,        any,            any,            any,            any, any, any, any, any, any,
        /*  Checksum */    
            any } );

        public static Response LampInfoSuccess => new( new byte[]{ 0x23, 0x96, any, any, 0x06,
        /*  Lamp#       What was requested      32-bit int data         Checksum */
            any,        any,                    any, any, any, any,     any } );

        public static Response GetErrorsSuccess => new( new byte[] { 0x20, 0x88, any, any, 0x0c,
        /*  Data 1 - 4 error flags */
            any, any, any, any, 
        /*  Data 5 - 8 reserved for system */
            any, any, any, any,
        /*  Data 9 extended error status flags */
            any,
        /*  Data 10 - 12 reserved for system */
            any, any, any,
        /*  Checksum */
            any } );

        public static Response ModelInfoSuccess => new( new byte[] { 0x20, 0x85, any, any, 0x20, 
        /*  32 bytes of data */
            any, any, any, any, any, any, any, any, any, any, any, any, any, any, any, any, any, any, any, any,
            any, any, any, any, any, any, any, any, any, any, any, any,
        /*  Checksum */
            any } );

        public static Response SerialInfoSuccess => new( new byte[] { 0x20, 0xbf, any, any, 0x12, 0x01, 0x06,
        /*  16 bytes of data */
            any, any, any, any, any, any, any, any, any, any, any, any, any, any, any, any,
        /*  Checksum */
            any } );


        /* Failure responses */
        public static Response PowerOnFailure => new( new byte[]{ 0xa2, 0x00, any, any, 0x02, any, any, any } );
        public static Response PowerOffFailure => new( new byte[]{ 0xa2, 0x01, any, any, 0x02, any, any, any } );
        public static Response SelectInputFailure => new( new byte[]{ 0xa2, 0x03, any, any, 0x02, any, any, any } );
        public static Response LampInfoFailure => new( new byte[] { 0xa3, 0x96, any, any, 0x02, any, any, any } );
        public static Response GetErrorsFailure => new( new byte[]{ 0xa0, 0x88, any, any, 0x02, any, any, any } );

        public Response( byte[] bytes ) : base(bytes) { Bytes = bytes; }
        public static new Response FromBytes( IEnumerable<byte> bytes ) { return new Response( bytes.ToArray() ); }

        public bool IndicatesSuccess {
            get {
                if( (this.Bytes[0] >> 4) == 0x02 )
                    return true;
                else
                    return false;
            }
        }

        public bool IndicatesFailure {
            get {
                if( (this.Bytes[0] >> 4) == 0x0a )
                    return true;
                else
                    return false;
            }
        }

        /**
        * Checks a Response against another to see if they partially match.
        * Bytes must at least match in length.  Either array may contain '*' (0x2a) for any byte positions,
        * indicating that byte is a wildcard that need not match.  If no non-'*' byte mismatches
        * are found, the two Responses match.
        */
        public bool Matches( Response other ) {
            if( this.Bytes.Length != other.Bytes.Length )
                return false;
            else {
                for( int idx = 0; idx < this.Bytes.Length; idx++ ) {
                    if(
                        this.Bytes[idx] != other.Bytes[idx] &&
                        this.Bytes[idx] != any &&
                        other.Bytes[idx] != any
                    ) {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
