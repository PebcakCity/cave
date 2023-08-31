namespace Cave.DeviceControllers.Projectors.NEC
{
    public class NECProjectorError : DeviceError
    {
        /// <summary>
        /// Internal error condition bitfields pulled from the manual
        /// </summary>
        private static readonly Dictionary<int, Dictionary<int, string?>> ErrorStates = new()
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

        private readonly string _message;

        public override string Message
        {
            get => _message;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public NECProjectorError() { _message = string.Empty; }

        /// <summary>
        /// Constructor taking a pair of keys and optionally a custom error
        /// message.  The keys are a byte position and bit value used to index
        /// into a dictionary mapped over a bitfield.  The dictionary contains
        /// error messages provided by the NEC documentation.  If the bit value
        /// bitwise ANDed with the byte value is not equal to zero, the
        /// error condition associated with that byte and bit combination is
        /// true. The keys are checked for existing even if a custom message is
        /// provided, partly to ensure our documentation is correct.  If there
        /// is no entry in the dictionary matching these keys, an
        /// <see cref="ArgumentException"/> is thrown.  If a custom message is
        /// provided, it is used in place of the dictionary-provided one for
        /// throwing more meaningful exceptions.
        /// </summary>
        /// <param name="byteKey">Position of the byte to look at in the
        /// bitfield.</param>
        /// <param name="bitKey">Bit value to bitwise AND with the byte value
        /// to determine whether an error is set.</param>
        /// <param name="customMessage">A message to use in place of the
        /// default one provided by the dictionary.</param>
        /// <exception cref="ArgumentException">Thrown if no entry is found in
        /// the dictionary using the provided keys.
        /// </exception>
        public NECProjectorError( int byteKey, int bitKey, string? customMessage = null )
        {
            if ( ! ErrorStates.ContainsKey(byteKey) ||
                 ! ErrorStates[byteKey].ContainsKey(bitKey) )
                throw new ArgumentException($"Bad arguments to {nameof(NECProjectorError)} constructor.");

            _message = (customMessage ?? ErrorStates[byteKey][bitKey]) ?? "Unknown NEC error";
        }

        /// <summary>
        /// Reads the data from the <see cref="Response"/> to a
        /// GetErrors <see cref="Command"/> and parses it for reported errors.
        /// </summary>
        /// <param name="response"></param>
        /// <returns>A list of <see cref="NECProjectorError"/> instances
        /// reported in the response.</returns>
        public static List<NECProjectorError> GetErrorsFromResponse( Response response )
        {
            List<NECProjectorError> errorsReported = new();
            /* Error bytes are contained in Data[5..8] & [13], [14..16] are
             * system reserved, checksum is Data[17] */
            if ( response is not null && response.Data.Length > 18 )
            {
                var relevantBytes = response.Data[5..14];
                foreach ( var outerKeyValuePair in ErrorStates )
                {
                    int byteKey = outerKeyValuePair.Key;
                    Dictionary<int, string?> errorData = outerKeyValuePair.Value;
                    foreach ( var innerKeyValuePair in errorData )
                    {
                        int bitKey = innerKeyValuePair.Key;
                        string? errorMsg = innerKeyValuePair.Value;
                        if ( ( relevantBytes[byteKey] & bitKey ) != 0 && errorMsg != null )
                            errorsReported.Add(new NECProjectorError(byteKey, bitKey));
                    }
                }
            }
            return errorsReported;
        }

        public override string ToString()
        {
            return $"{nameof(NECProjectorError)}: {Message}";
        }

        public static implicit operator string( NECProjectorError error ) => error.ToString();
    }
}
