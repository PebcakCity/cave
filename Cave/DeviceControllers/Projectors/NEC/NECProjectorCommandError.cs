namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// Represents an error due to a failed command
    /// </summary>
    public class NECProjectorCommandError : DeviceCommandError
    {
        /// <summary>
        /// Error codes corresponding to command failure reasons
        /// </summary>
        private static readonly Dictionary<(int, int), string> ErrorCodes = new()
        {
            { (0x00, 0x00), "The command cannot be recognized." },
            { (0x00, 0x01), "The command is not supported by the model in use." },
            { (0x01, 0x00), "The specified value is invalid." },
            { (0x01, 0x01), "The specified input terminal is invalid." },
            { (0x01, 0x02), "The specified language is invalid." },
            { (0x02, 0x00), "Memory allocation error" },
            { (0x02, 0x02), "Memory in use" },
            { (0x02, 0x03), "The specified value cannot be set. (Has the device finished powering on yet?)" },
            { (0x02, 0x04), "Forced onscreen mute on" },
            { (0x02, 0x06), "Viewer error" },
            { (0x02, 0x07), "No signal" },
            { (0x02, 0x08), "A test pattern is displayed." },
            { (0x02, 0x09), "No PC card is inserted." },
            { (0x02, 0x0a), "Memory operation error" },
            { (0x02, 0x0c), "An entry list is displayed." },
            { (0x02, 0x0d), "The command cannot be accepted because the power is off." },
            { (0x02, 0x0e), "The command execution failed." },
            { (0x02, 0x0f), "There is no authority necessary for the operation." },
            { (0x03, 0x00), "The specified gain number is incorrect." },
            { (0x03, 0x01), "The specified gain is invalid." },
            { (0x03, 0x02), "Adjustment failed." }
        };

        private readonly string _message;

        private (int Byte1, int Byte2) ErrorTuple { get; init; }

        private string ErrorCode
        {
            get => string.Format("{0:x2}{1:x2}", ErrorTuple.Byte1, ErrorTuple.Byte2);
        }

        public override string Message
        {
            get => _message;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public NECProjectorCommandError() { _message = string.Empty; }

        /// <summary>
        /// Constructor taking a tuple of two <see cref="int"/> values and an
        /// optional custom error message.  The value tuple is used as a
        /// dictionary key to retrieve a default associated error message from
        /// a static dictionary.  If the key does not exist in the error
        /// dictionary, an <see cref="ArgumentException"/> is thrown.  If a
        /// custom message is provided, it is used in place of the default
        /// message.
        /// </summary>
        /// <param name="errorValues">A value tuple of two ints.</param>
        /// <param name="customMessage">A message to use in place of the
        /// default one provided by the dictionary.</param>
        /// <exception cref="ArgumentException">Thrown if the argument to
        /// the constructor is determined to be invalid, that is, there is no
        /// known NEC projector error code matching this tuple.</exception>
        public NECProjectorCommandError((int byte1, int byte2) errorValues, string? customMessage = null)
        {
            ErrorTuple = errorValues;
            if ( !ErrorCodes.TryGetValue(ErrorTuple, out string? defaultMessage) )
                throw new ArgumentException($"Bad argument to {nameof(NECProjectorCommandError)} constructor.");
            _message = (customMessage ?? defaultMessage) ?? "Unknown NEC command error";
        }

        /// <summary>
        /// Constructor taking two <see cref="int"/> values, plus an optional
        /// custom error message.  Combines the two ints into a tuple and calls
        /// the overload taking a tuple and string.
        /// </summary>
        /// <param name="byte1"></param>
        /// <param name="byte2"></param>
        /// <param name="customMessage"></param>
        public NECProjectorCommandError(int byte1, int byte2, string? customMessage = null) 
            : this((byte1, byte2), customMessage) { }

        public override string ToString()
        {
            return $"{nameof(NECProjectorCommandError)} {ErrorCode} - {Message}";
        }

        public static implicit operator string(NECProjectorCommandError error) => error.ToString();
    }
}
