using System;
using System.Collections.Generic;
using System.Linq;

namespace cave {
    namespace utils {

        public class ByteSequence {
            public byte[] Bytes {get; protected set;}
            protected ByteSequence( byte[] bytes ) => (Bytes) = (bytes);
            public override string ToString() {
                string repr = "";
                bool isPrintable( byte b ) {
                    return !Char.IsControl( (char)b ) && !Char.IsWhiteSpace( (char)b );
                }
                foreach( byte b in Bytes ) {
                    if( isPrintable(b) ) {
                        repr += " '" + (char)b + "' ";
                    } else {
                        repr += string.Format( "0x{0:x2}", b ) + " ";
                    }
                }
                return repr;
            }

            public override bool Equals( object obj ) {
                if(
                    obj is byte[] &&
                    ((byte[])obj).SequenceEqual( this.Bytes )
                ) {
                    return true;
                } else if( obj is ByteSequence other ) {
                    bool typeMatches = this.GetType().Equals( obj.GetType() );
                    bool bytesMatch = this.Bytes.SequenceEqual( other.Bytes );
                    if( typeMatches && bytesMatch ) {
                        return true;
                    }
                }
                return false;
            }

            public override int GetHashCode() {
                return base.GetHashCode();
            }

            public static ByteSequence FromBytes( IEnumerable<byte> bytes ) { return new ByteSequence( bytes.ToArray() ); }
            
        }
        
    }
}
