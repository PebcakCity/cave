
namespace Cave.DeviceControllers.Projectors.NEC
{
    /* Error-related fields */
    public partial class NECProjector : Projector
    {
        /// <summary>
        /// Internal error condition bitfields
        /// </summary>
        private readonly Dictionary<int, Dictionary<int, string?>> ErrorStates = new Dictionary<int, Dictionary<int, string?>>()
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
    }

    /// <summary>
    /// Represents an error due to a failed command
    /// </summary>
    public class NECCommandError : Exception
    {
        /// <summary>
        /// Error codes corresponding to command failure reasons
        /// </summary>
        private readonly Dictionary<(int, int), string> ErrorCodes = new Dictionary<(int ec1, int ec2), string>()
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
        public NECCommandError((int byte1, int byte2) code, string? message=null) 
        {
            this.ErrorCode = string.Format("{x2}{x2}", code.byte1, code.byte2);
            this.Message = message
                ?? ErrorCodes.GetValueOrDefault(code)
                ?? "Unknown error";
        }

        public NECCommandError(int byte1, int byte2) : this((byte1, byte2)) { }

        public override string ToString()
        {
            return $"NECCommandError {ErrorCode} - {Message}";
        }

        public static implicit operator string(NECCommandError error) => error.ToString();
    }
}
