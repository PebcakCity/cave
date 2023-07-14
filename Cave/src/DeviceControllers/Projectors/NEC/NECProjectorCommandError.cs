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

        private readonly string? _message;

        // Make private once we are done testing
        public (int Byte1, int Byte2) ErrorTuple { get; set; }

        public string ErrorCode
        {
            get
            {
                return string.Format("{0:x2}{1:x2}", ErrorTuple.Byte1, ErrorTuple.Byte2);
            }
        }
        public override string Message
        {
            get
            {
                return _message ?? ErrorCodes.GetValueOrDefault(ErrorTuple) ?? "Unknown error";
            }
        }
        public NECProjectorCommandError() { }
        public NECProjectorCommandError((int byte1, int byte2) errorValues, string? customMessage = null)
        {
            ErrorTuple = errorValues;
            _message = customMessage;
        }
        public NECProjectorCommandError(int byte1, int byte2) : this((byte1, byte2)) { }
        public override string ToString()
        {
            return string.Format("NECCommandError {0} - {1}",
                ErrorCode, Message);
        }

        public static implicit operator string(NECProjectorCommandError error) => error.ToString();
    }
}
