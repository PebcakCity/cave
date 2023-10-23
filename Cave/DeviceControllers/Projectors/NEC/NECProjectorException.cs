namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// Represents an internal error condition reported by the projector
    /// </summary>
    public class NECProjectorException : DeviceException
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

        public override string Message{ get => _message; }
        public override string? StackTrace { get => null; }

        /// <summary>
        /// Default parameterless constructor.
        /// </summary>
        public NECProjectorException() 
            : base() 
        {
            _message = "Unknown NEC projector error";
        }

        /// <summary>
        /// Constructor taking a string message.  If parameter <paramref name="message"/> is null, the default of
        /// "Unknown NEC projector error" is used instead.
        /// </summary>
        /// <param name="message">Message indicating the error.</param>
        public NECProjectorException(string? message)
            : this()
        {
            _message = message ?? _message;
        }

        /// <summary>
        /// Static method taking a pair of keys and returning a matching <see cref="NECProjectorException"/> instance.  
        /// The keys are used to retrieve an error message from a dictionary of error messages provided in NEC's
        /// documentation.  If either key argument is invalid, an <see cref="ArgumentOutOfRangeException"/> is thrown
        /// for that argument.
        /// </summary>
        /// <param name="byteKey">Dictionary key 1.</param>
        /// <param name="bitKey">Dictionary key 2.</param>
        /// <returns>A new <see cref="NECProjectorException"/> with the message retrieved from the dictionary using the
        /// provided keys.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if a key is invalid.</exception>
        public static NECProjectorException CreateNewFromValues(int byteKey, int bitKey)
        {
            if ( ErrorStates.TryGetValue(byteKey, out var dictionaryForByte) )
            {
                if ( dictionaryForByte.TryGetValue(bitKey, out var message) )
                    return new NECProjectorException(message);
                throw new ArgumentOutOfRangeException(nameof(bitKey), bitKey,
                    $"Bad argument to {nameof(NECProjectorException)}.{nameof(CreateNewFromValues)}()");
            }
            throw new ArgumentOutOfRangeException(nameof(byteKey), byteKey,
                $"Bad argument to {nameof(NECProjectorException)}.{nameof(CreateNewFromValues)}()");
        }

        /// <summary>
        /// Reads the data from the <see cref="Response"/> to a GetErrors <see cref="Command"/> and parses it for
        /// reported errors.  Device error information is contained in a bitfield primarily spanning the 6th through
        /// 9th bytes of the response.
        /// </summary>
        /// <param name="response"><see cref="Response"/> object containing the projector's response to a
        /// <see cref="Command.GetErrors"/> command.</param>
        /// <returns>A list of <see cref="NECProjectorException"/> instances matching error bits set in the response.
        /// </returns>
        public static List<NECProjectorException> GetErrorsFromResponse( Response response )
        {
            List<NECProjectorException> errorsReported = new();
            /* Error bytes are contained in Data[5..8] & [13], [14..16] are system reserved, checksum is Data[17] */
            if ( response is not null && response.Data.Length == 18 )
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
                            errorsReported.Add(CreateNewFromValues(byteKey, bitKey));
                    }
                }
            }
            return errorsReported;
        }

        public override string ToString() => $"{nameof(NECProjectorException)}: {Message}";

        public static implicit operator string( NECProjectorException ex ) => ex.ToString();
    }
}
