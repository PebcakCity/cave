
using NLog;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class NECProjectorError : DeviceError
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Internal error condition bitfields
        /// </summary>
        public static readonly Dictionary<int, Dictionary<int, string?>> ErrorStates = new()
        {
            {
                0,
                new Dictionary<int, string?> {
                    { 0x80, "Lamp 1 must be replaced (exceeded maximum hours)" },
                    { 0x40, "Lamp 1 failed to light" },
                    { 0x20, "Power error" },
                    { 0x10, "Fan error" },
                    { 0x08, "Fan error" },
                    { 0x04, null },
                    { 0x02, "Temperature error (bi-metallic strip)" },
                    { 0x01, "Lamp cover error" }
                }
            },
            {
                1,
                new Dictionary<int, string?> {
                    { 0x80, "Refer to extended error status" },
                    { 0x40, null },
                    { 0x20, null },
                    { 0x10, null },
                    { 0x08, null },
                    { 0x04, "Lamp 2 failed to light" },
                    { 0x02, "Formatter error" },
                    { 0x01, "Lamp 1 needs replacing soon"}
                }
            },
            {
                2,
                new Dictionary<int, string?> {
                    { 0x80, "Lamp 2 needs replacing soon" },
                    { 0x40, "Lamp 2 must be replaced (exceeded maximum hours)" },
                    { 0x20, "Mirror cover error" },
                    { 0x10, "Lamp 1 data error" },
                    { 0x08, "Lamp 1 not present" },
                    { 0x04, "Temperature error (sensor)" },
                    { 0x02, "FPGA error" },
                    { 0x01, null }
                }
            },
            {
                3,
                new Dictionary<int, string?> {
                    { 0x80, "The lens is not installed properly" },
                    { 0x40, "Iris calibration error" },
                    { 0x20, "Ballast communication error" },
                    { 0x10, null },
                    { 0x08, "Foreign matter sensor error" },
                    { 0x04, "Temperature error due to dust" },
                    { 0x02, "Lamp 2 data error" },
                    { 0x01, "Lamp 2 not present" }
                }
            },
            {
                8,
                new Dictionary<int, string?> {
                    { 0x80, null },
                    { 0x40, null },
                    { 0x20, null },
                    { 0x10, null },
                    { 0x08, "System error has occurred (formatter)" },
                    { 0x04, "System error has occurred (slave CPU)" },
                    { 0x02, "The interlock switch is open" },
                    { 0x01, "The portrait cover side is up" }
                }
            }
        };

        private readonly int BytePosition;
        private readonly int BitValue;

        public override string Message
        {
            get
            {
                return ErrorStates.GetValueOrDefault(BytePosition)?
                    .GetValueOrDefault(BitValue)!;
            }
        }

        public NECProjectorError() { }
        public NECProjectorError( int bytePos, int bitVal )
        {
            (BytePosition, BitValue) = (bytePos, bitVal);
        }

        public static NECProjectorError? TryGetByValues( int bytePos, int bitVal )
        {
            string? message = ErrorStates.GetValueOrDefault(bytePos)?.GetValueOrDefault(bitVal);
            if( message != null )
                return new NECProjectorError(bytePos, bitVal);
            return null;
        }

        public static List<NECProjectorError> GetErrorsFromResponse( Response response )
        {
            try
            {
                List<NECProjectorError> errorsReported = new();
                var relevantBytes = response.Data[5..14];
                foreach ( var outerPair in ErrorStates )
                {
                    int byteKey = outerPair.Key;
                    Dictionary<int, string?> errorData = outerPair.Value;
                    foreach ( var innerPair in errorData )
                    {
                        int bitKey = innerPair.Key;
                        string? errorMsg = innerPair.Value;
                        if ( ( relevantBytes[byteKey] & bitKey ) != 0 && errorMsg != null )
                            errorsReported.Add(new NECProjectorError(byteKey, bitKey));
                    }
                }
                return errorsReported;
            }
            catch ( Exception ex )
            {
                Logger.Error($"NECProjectorError.{nameof(GetErrorsFromResponse)}() :: {ex}");
                return new List<NECProjectorError>();
            }
        }

        public override string ToString()
        {
            return $"NECProjectorError: {Message}";
        }

        public static implicit operator string( NECProjectorError error ) => error.ToString();
    }
}
