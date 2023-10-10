using System.Collections;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// Represents an error due to a failed command
    /// </summary>
    public class NECProjectorCommandException : DeviceCommandException
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
            { (0x02, 0x03), "The specified value cannot be set. (Is the device on?)" },
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

        public override string Message { get => _message; }

        /// <summary>
        /// Default parameterless constructor
        /// </summary>
        public NECProjectorCommandException() 
            : base()
        {
            _message = "Command failed for unknown reason."; 
        }

        /// <summary>
        /// Constructor taking a string message.  If parameter <paramref name="message"/> is null, the default of
        /// "Command failed for unknown reason" is used instead.
        /// </summary>
        /// <param name="message">Message indicating the reason for command failure.</param>
        public NECProjectorCommandException(string? message)
            : this()
        {
            _message = message ?? _message;
        }

        /// <summary>
        /// Static factory method taking a tuple of two <see cref="int"/> values and returning a matching
        /// <see cref="NECProjectorCommandException"/>.  The value tuple is used as a dictionary key to retrieve an
        /// associated error message from a static dictionary.  If the key does not exist in the error dictionary,
        /// an <see cref="ArgumentOutOfRangeException"/> is thrown.
        /// </summary>
        /// <param name="errorTuple">A value tuple of two <see cref="int"/> values.</param>
        /// <param name="command">The NECProjector <see cref="Command"/> that failed.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the tuple argument is determined to be invalid,
        /// that is, there is no known NEC projector error code matching this tuple.</exception>
        /// <returns><see cref="NECProjectorCommandException"/> instance with message retrieved using the provided
        /// tuple.</returns>
        public static NECProjectorCommandException CreateNewFromValues((int byte1, int byte2) errorTuple,
            Command? command = null)
        {
            if ( ! ErrorCodes.TryGetValue(errorTuple, out string? message) )
                throw new ArgumentOutOfRangeException(
                    nameof(errorTuple), errorTuple,
                    $"Bad argument to {nameof(NECProjectorCommandException)}.{nameof(CreateNewFromValues)}()"
                );
            var ex = new NECProjectorCommandException(message);
            ex.Data.Add("ErrorCode", string.Format("{0:x2}{1:x2}", errorTuple.byte1, errorTuple.byte2));
            if ( command is not null )
                ex.Data.Add("Command", command.Name);
            return ex;
        }

        /// <summary>
        /// Static factory method taking two <see cref="int"/> values and returning a matching
        /// <see cref="NECProjectorCommandException"/>.  The values are packaged as a tuple and passed to the
        /// <see cref="CreateNewFromValues"/> overload taking a tuple.
        /// </summary>
        /// <param name="byte1">Value 1</param>
        /// <param name="byte2">Value 2</param>
        /// <param name="command">The NECProjector <see cref="Command"/> that failed.</param>
        /// <returns><see cref="NECProjectorCommandException"/> instance with message retrieved using the provided
        /// values.</returns>
        public static NECProjectorCommandException CreateNewFromValues( int byte1, int byte2, Command? command = null )
        {
            return CreateNewFromValues((byte1, byte2), command);
        }

        public override string ToString()
        {
            string errorString = $"{nameof(NECProjectorCommandException)}: {Message}\n";
            foreach ( DictionaryEntry de in Data )
                errorString += string.Format("     {0,-10}:     {1,-20}\n", de.Key, de.Value);
            //var errorCode = Data.Contains("ErrorCode") ? Data["ErrorCode"] : null;
            //return $"{nameof(NECProjectorCommandException)}: " +
            //    ((errorCode is null) ? $"{Message}" : $"({errorCode}) {Message}");
            return errorString;
        }

        public static implicit operator string(NECProjectorCommandException ex) => ex.ToString();
    }
}
