using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using cave.drivers.projector;
using cave.utils;

/*
    Overall design:
    - Methods labeled with no suffix call and await async ones (PowerOn runs a Task that awaits PowerOnAsync, etc).
    - Methods labeled with 'Async' suffix tend to return a Task and are expected to be 'await'ed.
    - Methods GetStatus, GetInfo, & GetErrors all run a Task that awaits one or more client.SendCommandAsync calls.
    
    Methods in this class don't use data directly returned by SendCommandAsync.  SendCommandAsync fires an event that
    triggers handleDeviceResponse which interprets certain responses and maintains a store of fetched data in two structures:
    "deviceStatus" for transient information like current input, power on/off state & mute state; or "deviceInfo" for things
    which don't change often (or maybe ever) like model, serial & lamp usage data.

    Two timers call GetStatus and GetInfo with differing frequency and keep these data (more or less) up to date.
    Ideally GetStatus should be called every couple seconds & GetInfo at least every hour or so to get updated lamp data.
*/

namespace cave.drivers.projector.NEC {

    /// <summary>
    /// Driver class for NEC projectors
    /// </summary>
    public partial class NEC: IProjector {

#region Enums & structs / inner classes

        /// <summary>
        /// Power states of the device.
        /// As (partially) documented on p. 83 (BASIC INFORMATION REQUEST)
        /// </summary>
        public enum PowerState {
            StandbySleep = 0x00,
            Initializing = 0x01,
            Prewarmup = 0x02,
            Warming = 0x03,
            PowerOn = 0x04,
            Cooling = 0x05,
            StandbyError = 0x06,
            StandbyPowerSaving = 0x0f,
            StandbyNetwork = 0x10,
            Unknown = 0xff
        }

        /// <summary>
        /// Input values to be passed to SelectInput command.
        /// As documented on pp. 17-18 (INPUT SW CHANGE) and
        /// Appendix pp. 18-22 (Supplementary Information by Command INPUT SW CHANGE)
        /// </summary>
        public enum Input {
            RGB1 = 0x01,
            RGB2 = 0x02,
            HDMI1 = 0x1a,
            HDMI1Alt = 0xa1,
            HDMI2 = 0x1b,
            HDMI2Alt = 0xa2,
            Video = 0x06,
            DisplayPort = 0xa6,
            HDBaseT = 0xbf,
            HDBaseTAlt =  0x20,
            SDI = 0xc4,
            /* For mapping between NEC.InputState dictionary and Input enum type:
                We don't really care about accurately reporting exactly which input we're on as it depends on the specific model.
                We care about being able to accurately select an input, specifically one of the above, and even more specifically RGB and HDMI.
                The code '0x1f' corresponds to the USB A input, which is present on most models and serves as a stand-in for inputs like
                USB, LAN, viewer, "APPS" (whatever that is), and cardslot inputs, which we don't intend to support selecting. */
            Other = 0x1f
        }

        private class DeviceStatus {
            /* Power, input & mute states all are reported by a single command, GetStatus */
            public PowerState? Power { get; set; }
            public Input? InputSelected { get; set; }
            public struct MuteStruct {
                public bool Video { get; set; }
                public bool Audio { get; set; }
            }
            public MuteStruct Muted = new();
        }

        public class DeviceInfo {
            public string Model { get; set; }
            public string SerialNumber { get; set; }
            public struct Lamp {
                /* Projector reports all times in seconds... */
                public int SecondsUsed { get; set; }
                public int SecondsGoodFor { get; set; }
                public int SecondsRemaining { get; set; }
                public int PercentRemaining { get; set; }
                /* ... divide to get hours. */
                public int HoursUsed { get { return (int)Math.Floor((double)SecondsUsed/3600); } }
                public int HoursGoodFor { get { return (int)Math.Floor((double)SecondsGoodFor/3600); } }
                public int HoursRemaining { get { return (int)Math.Floor((double)SecondsRemaining/3600); } }

                public enum LampNumber {
                    Lamp1 = 0x00,
                    Lamp2 = 0x01
                }
                public enum LampInfo {
                    UsageTimeSeconds = 0x01,
                    GoodForSeconds = 0x02,      /* Total factory-allotted lifetime of the lamp, in seconds */
                    RemainingPercent = 0x04,
                    RemainingSeconds = 0x08
                }
            }
            public Lamp Lamp1 = new(), Lamp2 = new();
        }

#endregion

#region Private fields

        private Client client = null;
        private ILogger logger;
        private DeviceStatus deviceStatus = new();
        private DeviceInfo deviceInfo = new();
        private System.Timers.Timer statusUpdateTimer;
        private System.Timers.Timer infoUpdateTimer;

#endregion

#region Operational data

        /* NEC's input switch command uses a set of values that does not at all map to what its get input status command returns.
            On top of this, both values change with generations and vary by specific models within each generation, making it impossible
            to know for sure what input is currently selected without keeping up with the exact models and their supported inputs.
            (What a nightmare...)
            Because of this disjoint, a mapping from the "get current input" values to the "set this input" values is needed.
            We use the dictionary below along with the data provided by the manual's appendix.  It's not perfect (and won't ever be). */

        /* This info comes from the Appendix on pp. 30-35 */

            /* 
            { (0x01, 0x01), "Computer 1" },     { (0x01, 0x02), "Video" },
            { (0x01, 0x03), "S-video" },        { (0x01, 0x06), "HDMI 1" },
            { (0x01, 0x07), "Viewer" },         { (0x01, 0x0a), "Stereo DVI" },
            { (0x01, 0x20), "DVI" },            { (0x01, 0x21), "HDMI 1" },
            { (0x01, 0x22), "DisplayPort" },    { (0x01, 0x23), "SLOT" },
            { (0x01, 0x27), "HDBaseT" },        { (0x01, 0x28), "SDI 1" },
            { (0x02, 0x01), "Computer 2" },     { (0x02, 0x06), "HDMI 2" },
            { (0x02, 0x07), "LAN" },            { (0x02, 0x21), "HDMI 2" },
            { (0x02, 0x22), "DisplayPort 2" },  { (0X02, 0X28), "SDI 2" },
            { (0x03, 0x01), "Computer 3" },     { (0x03, 0x04), "Component" },
            { (0x03, 0x06), "SLOT" },           { (0x03, 0x28), "SDI 3" },
            { (0x04, 0x07), "Viewer" },         { (0x04, 0x28), "SDI 4" },
            { (0x05, 0x07), "APPS" }
            */

        /* Our mapping for matching the tuples reported by NEC's BASIC INFORMATION REQUEST with the Input values needed by our SelectInput. */
        private static Dictionary<(int, int), Input> InputState = new Dictionary<(int is1, int is2), Input>() {
            { (0x01, 0x01), Input.RGB1 },
            { (0x02, 0x01), Input.RGB2 },
            { (0x03, 0x01), Input.RGB2 },       /*COMPUTER 3, present on very few models and there are at least 3 different codes for this one */
            { (0x01, 0x06), Input.HDMI1 },
            { (0x01, 0x21), Input.HDMI1 },
            { (0x02, 0x06), Input.HDMI2 },
            { (0x02, 0x21), Input.HDMI2 },
            { (0x01, 0x20), Input.HDMI2 },      /*DVI-D*/
            { (0x01, 0x0a), Input.HDMI2 },      /*Stereo DVI (?)*/
            { (0x01, 0x02), Input.Video },
            { (0x01, 0x03), Input.Video },      /*S-video*/
            { (0x03, 0x04), Input.Video },      /*YPrPb*/
            { (0x01, 0x22), Input.DisplayPort },
            { (0x02, 0x22), Input.DisplayPort },/*DP 2*/
            { (0x01, 0x27), Input.HDBaseT },
            { (0x01, 0x28), Input.SDI },
            { (0x02, 0x28), Input.SDI },        /*SDI 2*/
            { (0x03, 0x28), Input.SDI },        /*SDI 3*/
            { (0x04, 0x28), Input.SDI },        /*SDI 4*/
            { (0x01, 0x07), Input.Other },      /*Viewer*/
            { (0x02, 0x07), Input.Other },      /*LAN*/
            { (0x03, 0x06), Input.Other },      /*SLOT*/
            { (0x04, 0x07), Input.Other },      /*Viewer*/
            { (0x05, 0x07), Input.Other },      /*APPS*/
            { (0x01, 0x23), Input.Other }       /*SLOT*/
        };

        /* Errors reported by failed commands */
        private Dictionary<(int, int), string> CommandError = new Dictionary<(int ec1, int ec2), string>() {
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

        /* A nested dictionary mapping error messages to individual bits of byte positions in the GetErrors response.
           The key of the outer dictionary is the byte position in the response, and the value is another dictionary mapping
           the bit values of that particular byte to the error states they represent when set. */
        private Dictionary<int, Dictionary<int, string>> ErrorStates = new Dictionary<int, Dictionary<int, string>>() {
            {
                0,
                new Dictionary<int, string> {
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
                new Dictionary<int, string> {
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
                new Dictionary<int, string> {
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
                new Dictionary<int, string> {
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
                new Dictionary<int, string> {
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

#endregion

#region Constructors

        /// <summary>
        /// Create a new NEC object, set up logging for it, create an NEC.Client
        /// to handle connection details, and begin keeping track of the device
        /// state upon connection success.  Any failure at any point results in
        /// an exception being thrown back to the caller.
        /// </summary>
        public NEC( MainWindow window, string ip, int port=7142 ) {
            try {
                this.logger = Program.LogFactory.CreateLogger( "NEC" );
                
                statusUpdateTimer = new System.Timers.Timer(2000);
                statusUpdateTimer.Elapsed += statusTimerElapsed;
                statusUpdateTimer.Enabled = false;

                infoUpdateTimer = new System.Timers.Timer(30000 * 60);
                infoUpdateTimer.Elapsed += infoTimerElapsed;
                infoUpdateTimer.Enabled = false;

                this.client = new Client( this, window, ip, port );

                client.ClientConnected += enableUpdates;
                client.ClientDisconnected += disableUpdates;
                client.ResponseReceived += handleDeviceResponse;

                logger.LogDebug( ":: constructed" );
                GetInfo( firstRun: true );
            } catch( Exception ) {
                throw;
            }
        }

#endregion

#region Private methods

        /// <summary>
        /// Called when statusUpdateTimer elapses, retrieves device status.
        /// </summary>
        private void statusTimerElapsed( object source, ElapsedEventArgs args ) {
            GetStatus();
        }

        /// <summary>
        /// Called when infoUpdateTimer elapses, retrieves lamp info.
        /// </summary>
        private void infoTimerElapsed( object source, ElapsedEventArgs args ) {
            logger.LogInformation("Calling GetInfo()");
            GetInfo();
        }

        /// <summary>
        /// Called by subscription to client.ClientConnected.
        /// Enables statusTimerElapsed and infoTimerElapsed to run
        /// while the socket is connected to the NEC device.
        /// </summary>
        private void enableUpdates( object sender, ClientConnectionEventArgs args ) {
            logger.LogInformation(args.Message);
            statusUpdateTimer.Enabled = true;
            infoUpdateTimer.Enabled = true;
        }
        
        /// <summary>
        /// Called by subscription to client.ClientDisconnected.
        /// Prevents statusTimerElapsed and infoTimerElapsed from running
        /// while the socket is disconnected.
        /// </summary>
        private void disableUpdates( object sender, ClientConnectionEventArgs args ) {
            logger.LogWarning(args.Message);
            statusUpdateTimer.Enabled = false;
            infoUpdateTimer.Enabled = false;
        }

        /// <summary>
        /// Called by subscription to client.ResponseReceived.
        /// Entry point to processing of all Responses.  Checks the Response for bytes
        /// indicating success or failure and calls the appropriate handler.
        /// </summary>
        /// <param name="rea">ResponseEventArgs object containing the Response and the Command that was sent.</param>
        private void handleDeviceResponse( object sender, ResponseEventArgs rea ) {
            // Log the response
            Response response = rea.Response;
            logger.LogDebug( "Response: {response}", response );

            if( response == null )
                return;

            // Interpret the response
            if( response.IndicatesFailure ) {
                handleFailureResponses( rea );
            }
            else if ( response.IndicatesSuccess ) {
                handleSuccessResponses( rea );
            }
        }

        /// <summary>
        /// Entry point for handling all Responses that indicate Command failure.
        /// Gets the error code reported and logs whatever error message is
        /// associated with that code if any.
        /// </summary>
        /// <param name="rea">ResponseEventArgs object containing the Response and the Command that was sent.</param>
        private void handleFailureResponses( ResponseEventArgs rea ) {
            Response response = rea.Response;
            Command command = rea.Command;

            if( command != null ) {
                // get error code at 6th & 7th bytes
                if( response.Bytes.Length >= 8 ) {
                    var errorCode = (response.Bytes[5], response.Bytes[6]);
                    var errorMsg = CommandError.GetValueOrDefault(errorCode);
                    if( errorMsg != null ) {
                        string logString = $"Operation '{command.Name}' failed with NEC error code {errorCode}" +
                            Environment.NewLine + $"{errorMsg}";
                        logger.LogWarning( logString );
                    }
                }
            }
        }

        /// <summary>
        /// Entry point for handling all Responses that indicate Command success.
        /// Checks which Command was run and whether the Response matches what we expect from that command,
        /// and calls the appropriate subhandler if so.
        /// </summary>
        /// <param name="rea">ResponseEventArgs object containing the Response and the Command that was sent.</param>
        private void handleSuccessResponses( ResponseEventArgs rea ) {
            Response response = rea.Response;
            Command command = rea.Command;

            if( command != null ) {
                logger.LogDebug( "Command '{name}' successful.", command.Name );

                if( command.Name.Equals("GetErrors") && response.Matches( Response.GetErrorsSuccess ) ) {
                    handleGetErrors( response );
                } else if( command.Name.Equals("GetStatus") && response.Matches( Response.GetStatusSuccess ) ) {
                    handleGetStatus( response );
                } else if( command.Name.Equals("GetLampInfo") && response.Matches( Response.LampInfoSuccess ) ) {
                    handleGetLampInfo( response );
                } else if( command.Name.Equals("GetModel") && response.Matches( Response.ModelInfoSuccess ) ) {
                    handleGetModelInfo( response );
                } else if( command.Name.Equals("GetSerial") && response.Matches( Response.SerialInfoSuccess ) ) {
                    handleGetSerialInfo( response );
                }
            }
        }

        /// <summary>
        /// Handles the response from a GetErrors command.
        /// Logs whatever projector errors were reported.
        /// </summary>
        /// <param name="response">Response object representing Device's response</param>
        private void handleGetErrors( Response response ) {
            List<string> errors = parseErrors( response );
            if( errors.Count > 0 ) {
                string errorString = String.Join( Environment.NewLine, errors );
                string logString = "Projector errors are reported: " +
                    Environment.NewLine + $"{errorString}";
                logger.LogWarning( logString );
            } else {
                logger.LogInformation( "No projector errors reported at this time." );
            }
        }

        /// <summary>
        /// Helper for handleGetErrors.  Parses the GetErrors response to extract error data
        /// and pair with error message strings.  Returns a list of strings matching the
        /// errors reported.
        /// </summary>
        /// <param name="response">Response object representing Device's response</param>
        private List<string> parseErrors( Response response ) {
            List<string> errorsReported = new List<string>();
            var relevantBytes = response.Bytes[5..14];  // 6th thru 9th bytes of response
                                                        // plus the 14th ("extended status")
            foreach( var outerPair in ErrorStates ) {
                int byteKey = outerPair.Key;
                Dictionary<int, string> errorData = outerPair.Value;
                foreach( var innerPair in errorData ) {
                    int bitKey = innerPair.Key;
                    string errorMsg = innerPair.Value;
                    if( (relevantBytes[byteKey] & bitKey) != 0 && errorMsg != null ) {
                        errorsReported.Add( errorMsg );
                    }
                }
            }
            return errorsReported;
        }

        /// <summary>
        /// Handles the response for a GetStatus command.
        /// Status data we need is extracted from the relevant response bytes
        /// and stored in the appropriate deviceStatus fields.
        /// </summary>
        /// <param name="response">Response object representing Device's response</param>
        private void handleGetStatus( Response response ) {
            try {
                deviceStatus.Power = (PowerState)response.Bytes[6];

                var tuple = (response.Bytes[8], response.Bytes[9]);
                deviceStatus.InputSelected = InputState.GetValueOrDefault(tuple);

                deviceStatus.Muted.Video = (response.Bytes[11] == 0x01);
                deviceStatus.Muted.Audio = (response.Bytes[12] == 0x01);

                logger.LogDebug(
                    "handleGetStatus :: PowerStatus: {stat1}, InputSelected: {stat2}, Muted.Video: {stat3}, Muted.Audio: {stat4}",
                    deviceStatus.Power, deviceStatus.InputSelected,
                    deviceStatus.Muted.Video, deviceStatus.Muted.Audio
                );
            } catch( Exception ex ) {
                logger.LogError( "handleGetStatus :: Error occurred: {error}", ex.Message );
            }
        }

        /// <summary>
        /// Handles the response for a GetLampInfo command.
        /// Gets a byte representing what was queried and the 4 bytes representing a 32-bit answer
        /// to the query from the Response and stores the returned data in the appropriate deviceInfo.Lamp field.
        /// </summary>
        /// <param name="response">Response object representing Device's response</param>
        private void handleGetLampInfo( Response response ) {
            /* Bytes[5] is which lamp we were querying.  I don't have any projectors with more than one lamp.
               Supporting this is probably outside the scope of this project right now, so let's ignore it. */

            /* Bytes[6] is which data we requested: 
                    0x01 = UsageTimeSeconds,
                    0x02 = GoodForSeconds,      
                    0x04 = RemainingPercent,
                    0x08 = RemainingSeconds */
            var request = response.Bytes[6];
            
            /* Bytes[7] - Bytes[10] (2nd slice index is noninclusive) are the requested data in little-endian 32-bit int */
            var data = response.Bytes[7..11];
            int value = BitConverter.ToInt32( data, 0 );
            switch( request ){
                case (byte)DeviceInfo.Lamp.LampInfo.GoodForSeconds :
                    deviceInfo.Lamp1.SecondsGoodFor = value;
                    break;
                case (byte)DeviceInfo.Lamp.LampInfo.RemainingSeconds :
                    deviceInfo.Lamp1.SecondsRemaining = value;
                    break;
                case (byte)DeviceInfo.Lamp.LampInfo.UsageTimeSeconds :
                    deviceInfo.Lamp1.SecondsUsed = value;
                    break;
                case (byte)DeviceInfo.Lamp.LampInfo.RemainingPercent :
                    deviceInfo.Lamp1.PercentRemaining = value;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Handles the response for a GetModel command.
        /// Truncates off superfluous null bytes stored after the model name in the response.
        /// </summary>
        /// <param name="response">Response object representing Device's response</param>
        private void handleGetModelInfo( Response response ) {
            var data = response.Bytes[5..37];
            /* Trim ending nulls */
            deviceInfo.Model = Encoding.UTF8.GetString(data).TrimEnd('\0');            
        }

        /// <summary>
        /// Handles the response for a GetSerial command.
        /// Truncates off superfluous null bytes stored after the serial number in the response.
        /// </summary>
        /// <param name="response">Response object representing Device's response</param>
        private void handleGetSerialInfo( Response response ) {
            var data = response.Bytes[7..23];
            deviceInfo.SerialNumber = Encoding.UTF8.GetString(data).TrimEnd('\0');
        }


#endregion

#region Public methods

        /// <summary>
        /// Implements IProjector.PowerOn()
        /// </summary>
        public void PowerOn() {
            Task.Run( async () => {
                await PowerOnAsync();
            } );
        }

        /// <summary>
        /// Powers on the device.
        /// </summary>
        public async Task PowerOnAsync() {
            logger.LogInformation( "Sending command 'PowerOn'");
            await client.SendCommandAsync( Command.PowerOn );
        }

        /// <summary>
        /// Implements IProjector.PowerOff()
        /// </summary>
        public void PowerOff() {
            Task.Run( async () => {
                await PowerOffAsync();
            } );
        }

        /// <summary>
        /// Powers off the device.
        /// </summary>
        public async Task PowerOffAsync() {
            logger.LogInformation( "Sending command 'PowerOff'");
            await client.SendCommandAsync( Command.PowerOff );
        }

        /// <summary>
        /// Implements IProjector.SelectInput( object input )
        /// </summary>
        public void SelectInput( object input ) {
            Task.Run( async () => {
                await SelectInputAsync( input );
            } );
        }

        /// <summary>
        /// Checks to ensure that input is of a supported type and then selects that input.
        /// </summary>
        /// <param name="input">An object representing the NEC.Input member.  These are one-byte numeric input codes, and as such
        /// the parameter can be an instance of NEC.Input, byte, 8-bit int, float/double < 255.0, or even string as long as that
        /// string can be parsed into an Input enum member by Enum.TryParse(...).  Parsing works by matching either the value or the member name.
        /// So SelectInput(NEC.Input.RGB1) works just as well as SelectInput(1), SelectInput("RGB1") or even SelectInput("1").
        /// It has its limits (doesn't seem to work with string representations of floating point numbers like "1.0").</param>
        public async Task SelectInputAsync( object input ) {
            object necInput = input;
            if( Enum.TryParse( typeof(Input), input.ToString(), true, out necInput ) ) {
                logger.LogInformation( "Sending command 'SelectInput({value})'", (Input)necInput );
                await client.SendCommandAsync( Command.SelectInput, true, (Input)necInput );
            }
        }

        /// <summary>
        /// Implements IProjector.PowerOnAndSelectInput( object input ).
        /// Runs a Task which (1) Powers on the device, (2) waits for 10 seconds, and (3) attempts input selection.
        /// If the last known device status indicates it is already powered on, the Task simply attempts input selection.
        /// </summary>
        public void PowerOnAndSelectInput( object input ) {
            Task.Run( async () => {
                if( PowerStatus != PowerState.PowerOn ) {
                    await PowerOnAsync();
                    /* Leave ample time, it takes several seconds before the device will start to respond after powering on */
                    await Task.Delay( 10000 );
                }
                await SelectInputAsync( input );
            } );
        }

        /// <summary>
        /// Updates device status.  The Response will contain the device's
        /// current power state, last selected input and whether video and/or audio are currently muted.
        /// </summary>
        public void GetStatus() {
            logger.LogDebug( "GetStatus() :: Sending command 'GetStatus'" );
            Task.Run( async () => {
                await client.SendCommandAsync( Command.GetStatus );
            } );
        }

        /// <summary>
        /// Gets all available lamp information, as well as device model and serial number
        /// on the first run.  Pauses for 1/10th of a second between each command.
        /// This is intended to be run once or twice an hour to get updated lamp life data.
        /// If not running for the first time, model and serial number retrieval are skipped.
        /// </summary>
        /// <param name="firstRun">Whether this is running as part of startup or not.</param>
        public void GetInfo( bool firstRun=false ) {
            Task.Run( async () => {
                /* Increase this delay if the first SendCommandAsync tends to time out.
                   With a lower duration, debug logging will sometimes show that the first request below
                   (for LampInfo.GoodForSeconds) times out. */
                await Task.Delay(1000);

                logger.LogDebug( "Fetching lamp info..." );

                logger.LogDebug( "...GoodForSeconds");
                await client.SendCommandAsync( Command.GetLampInfo, true, DeviceInfo.Lamp.LampNumber.Lamp1, DeviceInfo.Lamp.LampInfo.GoodForSeconds );

                await Task.Delay(100);

                logger.LogDebug( "...UsageTimeSeconds");
                await client.SendCommandAsync( Command.GetLampInfo, true, DeviceInfo.Lamp.LampNumber.Lamp1, DeviceInfo.Lamp.LampInfo.UsageTimeSeconds );

                await Task.Delay(100);

                logger.LogDebug( "...RemainingSeconds");
                await client.SendCommandAsync( Command.GetLampInfo, true, DeviceInfo.Lamp.LampNumber.Lamp1, DeviceInfo.Lamp.LampInfo.RemainingSeconds );

                await Task.Delay(100);

                logger.LogDebug( "...RemainingPercent");
                await client.SendCommandAsync( Command.GetLampInfo, true, DeviceInfo.Lamp.LampNumber.Lamp1, DeviceInfo.Lamp.LampInfo.RemainingPercent );

                /* These won't suddenly change, so no need to query them every time */
                if( firstRun ) {
                    await Task.Delay(100);

                    logger.LogDebug( "Fetching model #" );
                    await client.SendCommandAsync( Command.GetModel );

                    await Task.Delay(100);

                    logger.LogDebug( "Fetching serial #" );
                    await client.SendCommandAsync( Command.GetSerial );
                }
            } );
        }

        /// <summary>
        /// Gets what error states the device is currently reporting if any,
        /// such as lamp in need of replacement, temperature error, etc.
        /// </summary>
        public void GetErrors() {
            logger.LogInformation( "Sending command 'GetErrors'" );
            Task.Run( async () => {
                await client.SendCommandAsync( Command.GetErrors );
            } );
        }

#endregion

#region Properties

        /// <summary>
        /// Retrieves the devices's last recorded power state.
        /// </summary>
        public PowerState? PowerStatus {
            get {
                return deviceStatus.Power;
            }
        }

        /// <summary>
        /// Retrieves the device's last recorded input selection.
        /// </summary>
        public Input? InputStatus {
            get {
                return deviceStatus.InputSelected;
            }
        }

        /// <summary>
        /// Retrieves the device's model number.
        /// </summary>
        public string Model {
            get {
                return deviceInfo.Model;
            }
        }

        /// <summary>
        /// Retrieves the device's serial number.
        /// </summary>
        public string Serial {
            get {
                return deviceInfo.SerialNumber;
            }
        }

        /// <summary>
        /// Retrieves the device's last recorded video mute status.
        /// </summary>
        public bool VideoMute {
            get {
                return deviceStatus.Muted.Video;
            }
        }

        /// <summary>
        /// Retrieves the device's last recorded audio mute status.
        /// </summary>
        public bool AudioMute {
            get {
                return deviceStatus.Muted.Audio;
            }
        }

#endregion

#region Test code

        public void test() {
            logger.LogInformation("PowerStatus: {stat1}, InputStatus: {stat2}", PowerStatus, InputStatus);
            logger.LogInformation("Video mute: {mute1}, Audio mute: {mute2}", deviceStatus.Muted.Video, deviceStatus.Muted.Audio);
            logger.LogInformation("Model name: {model}, Serial#: {serial}", deviceInfo.Model, deviceInfo.SerialNumber );
            logger.LogInformation("Lamp hours used: {stat1}, Lamp hours remaining: {stat2}, Lamp hour limit: {stat3}, Lamp life remaining (%): {stat4}",
                deviceInfo.Lamp1.HoursUsed, deviceInfo.Lamp1.HoursRemaining, deviceInfo.Lamp1.HoursGoodFor, deviceInfo.Lamp1.PercentRemaining);
        }

        public void test2() {
            
        }

#endregion

#region Notes

        /* When trying to use an occasional synchronous SendCommand call between timer-executed GetStatus calls
           that use SendCommandAsync, I found that the responses were getting mixed up between calls.
           For ex. if the device is already powered off, and I call SendCommand(Command.PowerOff), it results
           in an expected failure (error code (0x02,0x03)).  However, it reports that the command that was
           executed that resulted in this failure was "GetStatus", not "PowerOff" as it should have been.
           Maybe I'm missing something but I can't figure out why that would be unless it's something weird
           going on with sync/async.  So I've switched to a 100% SendCommandAsync implementation.  I may
           end up removing client.SendCommand altogether.
        */

#endregion

    }

}
