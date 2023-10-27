using System.Collections.Generic;
using System.Reflection;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class Response
    {
        /// <summary>
        /// <see cref="byte"/> [] holding response data.
        /// </summary>
        public byte[] Data { get; protected set; }

        /// <summary>
        /// Name of the Response
        /// </summary>
        public string? Name { get; init; }

        /* Wildcard byte */
        private static readonly byte wild = (byte)'*';


        /* ------------------------------------------Success responses----------------------------------------------- */


        internal static readonly Response PowerOnSuccess = new( new byte[] { 0x22, 0x00, wild, wild, 0x00, wild }, nameof(PowerOnSuccess) );
        internal static readonly Response PowerOffSuccess = new( new byte[] { 0x22, 0x01, wild, wild, 0x00, wild }, nameof(PowerOffSuccess) );
        internal static readonly Response SelectInputSuccess = new( new byte[] { 0x22, 0x03, wild, wild, 0x01, wild, wild }, nameof(SelectInputSuccess) );
        internal static readonly Response GetStatusSuccess = new( new byte[] { 0x20, 0xbf, wild, wild, 0x10, 0x02, 
        /*  Power       Content displayed       Input selected (tuple)      Video signal type */
            wild,       wild,                   wild, wild,                 wild,
        /*  Video mute  Sound mute      Onscreen mute   Freeze status   System reserved */
            wild,       wild,           wild,           wild,           wild, wild, wild, wild, wild, wild,
        /*  Checksum */    
            wild }, nameof(GetStatusSuccess) );

        internal static readonly Response GetInfoSuccess = new( new byte[] { 0x23, 0x8a, wild, wild, 0x62, 
        /* 98 bytes of data ... */
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
        /* Checksum */
            wild,
        }, nameof(GetInfoSuccess) );

        internal static readonly Response GetLampInfoSuccess = new( new byte[] { 0x23, 0x96, wild, wild, 0x06,
        /*  Lamp#       What was requested      32-bit int data             Checksum */
            wild,       wild,                   wild, wild, wild, wild,     wild }, nameof(GetLampInfoSuccess) );

        internal static readonly Response GetErrorsSuccess = new( new byte[] { 0x20, 0x88, wild, wild, 0x0c,
        /*  Data 1 - 4 error flags */
            wild, wild, wild, wild, 
        /*  Data 5 - 8 reserved for system */
            wild, wild, wild, wild,
        /*  Data 9 extended error status flags */
            wild,
        /*  Data 10 - 12 reserved for system */
            wild, wild, wild,
        /*  Checksum */
            wild }, nameof(GetErrorsSuccess) );

        internal static readonly Response GetModelNumberSuccess = new( new byte[] { 0x20, 0x85, wild, wild, 0x20, 
        /*  32 bytes of data */
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
        /*  Checksum */
            wild }, nameof(GetModelNumberSuccess) );

        internal static readonly Response GetSerialNumberSuccess = new( new byte[] { 0x20, 0xbf, wild, wild, 0x12, 0x01, 0x06,
        /*  16 bytes of data */
            wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild, wild,
        /*  Checksum */
            wild }, nameof(GetSerialNumberSuccess) );

        internal static readonly Response VideoMuteOnSuccess = new( new byte[] { 0x22, 0x10, wild, wild, 0x00, wild }, nameof(VideoMuteOnSuccess) );
        internal static readonly Response VideoMuteOffSuccess = new( new byte[] { 0x22, 0x11, wild, wild, 0x00, wild }, nameof(VideoMuteOffSuccess) );
        internal static readonly Response AudioMuteOnSuccess = new( new byte[] { 0x22, 0x12, wild, wild, 0x00, wild }, nameof(AudioMuteOnSuccess) );
        internal static readonly Response AudioMuteOffSuccess = new( new byte[] { 0x22, 0x13, wild, wild, 0x00, wild }, nameof(AudioMuteOffSuccess) );
        internal static readonly Response VolumeAdjustSuccess = new( new byte[] { 0x23, 0x10, wild, wild, 0x02, wild, wild, wild }, nameof(VolumeAdjustSuccess) );


        /* -----------------------------------------Failure responses------------------------------------------------ */

        
        internal static readonly Response PowerOnFailure = new( new byte[] { 0xa2, 0x00, wild, wild, 0x02, wild, wild, wild }, nameof(PowerOnFailure) );
        internal static readonly Response PowerOffFailure = new( new byte[] { 0xa2, 0x01, wild, wild, 0x02, wild, wild, wild }, nameof(PowerOffFailure) );
        internal static readonly Response SelectInputFailure = new( new byte[] { 0xa2, 0x03, wild, wild, 0x02, wild, wild, wild }, nameof(SelectInputFailure) );
        internal static readonly Response GetStatusFailure = new( new byte[] { 0xa0, 0xbf, wild, wild, 0x02, wild, wild, wild }, nameof(GetStatusFailure) );
        internal static readonly Response GetInfoFailure = new( new byte[] { 0xa3, 0x8a, wild, wild, 0x02, wild, wild, wild }, nameof(GetInfoFailure) );
        internal static readonly Response GetLampInfoFailure = new( new byte[] { 0xa3, 0x96, wild, wild, 0x02, wild, wild, wild }, nameof(GetLampInfoFailure) );
        internal static readonly Response GetErrorsFailure = new( new byte[] { 0xa0, 0x88, wild, wild, 0x02, wild, wild, wild }, nameof(GetErrorsFailure) );
        internal static readonly Response GetModelNumberFailure = new( new byte[] { 0xa0, 0x85, wild, wild, 0x02, wild, wild, wild }, nameof(GetModelNumberFailure) );

        /* 
         * The failure response returned by the GetSerialNumber command is basically identical to that of GetStatus, so
         * it is always logged as 'GetStatusFailure' as that one appears first.  (Maybe I should rearrange them, because
         * I have never seen an actual failure to get current status, but GetSerialNumber fails every time the device
         * has not been power cycled since being plugged in?)
         */
        internal static readonly Response GetSerialNumberFailure = new( new byte[] { 0xa0, 0xbf, wild, wild, 0x02, wild, wild, wild }, nameof(GetSerialNumberFailure) );
        internal static readonly Response VideoMuteOnFailure = new( new byte[] { 0xa2, 0x10, wild, wild, 0x02, wild, wild, wild }, nameof(VideoMuteOnFailure) );
        internal static readonly Response VideoMuteOffFailure = new( new byte[] { 0xa2, 0x11, wild, wild, 0x02, wild, wild, wild }, nameof(VideoMuteOffFailure) );
        internal static readonly Response AudioMuteOnFailure = new( new byte[] { 0xa2, 0x12, wild, wild, 0x02, wild, wild, wild }, nameof(AudioMuteOnFailure) );
        internal static readonly Response AudioMuteOffFailure = new( new byte[] { 0xa2, 0x13, wild, wild, 0x02, wild, wild, wild }, nameof(AudioMuteOffFailure) );
        internal static readonly Response VolumeAdjustFailure = new( new byte[] { 0xa3, 0x10, wild, wild, 0x02, wild, wild, wild }, nameof(VolumeAdjustFailure) );

        /// <summary>
        /// Constructs a new <see cref="Response"/> with the given <see cref="Data">Data</see> array and
        /// <see cref="Name">Name</see>, if not null.
        /// </summary>
        /// <param name="data">Byte array for data</param>
        /// <param name="name">String value for response name</param>
        public Response( byte[] data, string? name=null )
        {
            Data = data;
            Name = name;
        }

        /// <summary>
        /// Checks if the first byte of <c><see cref="Data">Data</see></c> indicates the previous command succeeded.  If
        /// the command succeeded, the first byte of the response always begins with the four most significant bits set
        /// to <c>0010</c>.
        /// </summary>
        internal bool IndicatesCommandSuccess
        {
            get => ( this.Data[0] >> 4 == 0x02 );
        }

        /// <summary>
        /// Checks if the first byte of <c><see cref="Data">Data</see></c> indicates the previous command failed.  If
        /// the command failed, the first byte of the response always begins with the four most significant bits set to
        /// <c>1010</c>.
        /// </summary>
        internal bool IndicatesCommandFailure
        {
            get => ( this.Data[0] >> 4 == 0x0a );
        }

        /// <summary>
        /// Checks one <see cref="Response"/>'s <c>Data</c> against another's to see if they partially match.
        /// </summary>
        /// <param name="other">The other <see cref="Response"/> to test against.</param>
        /// <returns>True if the two Responses partially match, that is, the <c><see cref="Data">Data</see></c> members
        /// are equal in length and contain the same required bytes.  False otherwise.</returns>
        internal bool Matches( Response other )
        {
            return this.Matches(other.Data);
        }

        /// <summary>
        /// Checks this <see cref="Response"/>'s <c><see cref="Data">Data</see></c> array against another
        /// <see cref="byte"/>[] to see if they partially match.   First, the arrays are checked to see if they are
        /// equal in length.  Then, their contents are compared byte by byte.  Either array may contain
        /// <see cref="wild">wildcards</see> for any number of byte positions, indicating those bytes need not match
        /// against the same byte in the other array.  If after comparing the two arrays, no non-wildcard byte
        /// mismatches are found, the two arrays match.
        /// </summary>
        /// <param name="other">The other <see cref="byte"/> array to test against.</param>
        /// <returns>True if the two arrays match for all non-wildcard bytes.  False otherwise.</returns>
        internal bool Matches( byte[] other )
        {
            if( this.Data.Length != other.Length )
                return false;
            for( int idx = 0; idx < this.Data.Length; ++idx )
            {
                if(
                    this.Data[idx] != other[idx] &&
                    this.Data[idx] != wild &&
                    other[idx] != wild
                )
                {
                    return false;
                }
            }
            return true;
        }

        private static IEnumerable<Response> GetAll() =>
            typeof(Response).GetFields( BindingFlags.Public |
                                        BindingFlags.Static )
                            .Select(field => field.GetValue(null))
                            .Cast<Response>();

        /// <summary>
        /// Gets the name of the <see cref="Response"/> whose <c><see cref="Data">Data</see></c> array matches the
        /// given byte array, or null if none match.
        /// </summary>
        /// <param name="data">The <see cref="byte"/> array to match.</param>
        /// <returns></returns>
        internal static string? GetMatchingResponseName( byte[] data )
        {
            var match = GetAll().FirstOrDefault( member => member.Matches(data) );
            return match?.Name;
        }

        public override string ToString()
        {
            string data = "";
            for( int idx = 0; idx < Data.Length; ++idx )
            {
                data += string.Format("0x{0:x2}", Data[idx]);
                if( idx < Data.Length - 1 )
                    data += " ";
            }
            return $"Response.{(Name ?? GetMatchingResponseName(this.Data) ?? "[Unknown response]")} [{data}]";
        }
    }
}
