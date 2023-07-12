
namespace Cave.DeviceControllers.Projectors.NEC
{
    public class CommandError : Exception
    {
        private Dictionary<(int, int), string> ErrorCodes = new Dictionary<(int ec1, int ec2), string>()
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

        public string ErrorCode { get; }
        public override string Message { get; }
        public CommandError((int byte1, int byte2) code, string? message=null) 
        {
            this.ErrorCode = string.Format("{x2}{x2}", code.byte1, code.byte2);
            this.Message = message
                ?? ErrorCodes.GetValueOrDefault(code)
                ?? "Unknown error code.";
        }

        public CommandError(int byte1, int byte2) : this((byte1, byte2)) { }

        public override string ToString()
        {
            return $"NECError {ErrorCode} - {Message}";
        }

        public static implicit operator string(CommandError error) => error.ToString();
    }
}
