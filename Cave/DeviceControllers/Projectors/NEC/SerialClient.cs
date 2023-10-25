using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;

using NLog;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// Client for controlling NEC projectors over a serial port.  Mostly stable
    /// as long as timing between sending the command and attempting to read the
    /// response is not altered.
    /// </summary>
    public class SerialClient : INECClient
    {
        private static readonly Logger Logger = LogManager.GetLogger("NEC.SerialClient");

        private readonly string PortName;
        private readonly int Baudrate;

        private readonly SerialPort Port;
        private const int MaxReadSize = 256;

        private SerialClient(string port, int baudrate)
        {
            try
            {
                (PortName, Baudrate) = (port, baudrate);
                Port = new(PortName, Baudrate);
                Port.ReadTimeout = Port.WriteTimeout = 100;
            }
            catch (IOException ex)
            {
                Logger.Error(ex, $"Error opening port: {PortName}");
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="SerialClient"/> instance with the specified port name and baudrate and tests it to
        /// ensure the port is readable and writable.
        /// </summary>
        /// <param name="port">System port name</param>
        /// <param name="baudrate">Baudrate</param>
        /// <returns>A <see cref="Task{T}"/> representing a working <see cref="SerialClient"/> instance</returns>
        public static async Task<SerialClient> Create(string port, int baudrate)
        {
            Logger.Info("Creating new NEC.SerialClient instance.");
            SerialClient instance = new(port, baudrate);
            Logger.Info($"Attempting to open port '{port}' with default settings at {baudrate} baud.");
            await instance.TestConnection();
            return instance;
        }

        // Sends a test command
        private async Task TestConnection()
        {
            await SendCommandAsync(Command.GetInfo);
        }

        /// <summary>
        /// Sends the <see cref="Command"/> <paramref name="command"/> and returns the <see cref="Response"/> from the
        /// device.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{T}"/> representing the <see cref="Response"/> from the device.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        /// If no data is read because the end of the stream has been reached
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// If the response from the device cannot be interpreted because of data corruption
        /// </exception>
        /// <exception cref="TimeoutException">
        /// If a read timeout occurs waiting for the response
        /// </exception>
        public async Task<Response> SendCommandAsync( Command command )
        {
            using ( var cts = new CancellationTokenSource() )
            {
                int firstByte = 0;
                byte [] responseBytes;
                try
                {
                    Port.Open();

                    Logger.Debug($"Sending command: {command}");

                    // Send command.
                    byte[] cmdBytes = command.Data.ToArray();
                    Port.Write(cmdBytes, 0, cmdBytes.Length);

                    // Delay  before  beginning  read.  Lowering this is not
                    // advised, seems to make read errors more frequent.
                    await Task.Delay(100);

                    // Get first byte.  -1 indicates the end of the stream.
                    firstByte = Port.ReadByte();
                    if ( firstByte == -1 )
                        throw new EndOfStreamException("Unexpected end of stream");

                    // Get length of expected response (or throw an exception)
                    // based on whether the first byte indicates command failure,
                    // success, or neither (indicating data corruption)
                    int expectedLength = (firstByte >> 4) switch
                    {
                        0x0a => Command.FailureResponseLengths[command],
                        0x02 => Command.SuccessResponseLengths[command],
                        _ => throw new InvalidDataException("Bad response from device")
                    };

                    // Add first retrieved byte to response
                    responseBytes = new byte[expectedLength];
                    responseBytes[0] = (byte)firstByte;

                    // Get the rest
                    cts.CancelAfter(100);
                    int bytesRead = await Port.BaseStream.ReadAsync(
                        responseBytes.AsMemory(1, expectedLength-1), cts.Token);

                    // bytesRead+1 because we already read first byte
                    Response response = new(responseBytes[0..(bytesRead+1)]);
                    Logger.Debug($"Response: {response}");
                    return response;
                }
                // ReadAsync timeout (CancellationTokenSource expired)
                catch ( OperationCanceledException )
                {
                    throw new TimeoutException("Serial operation timed out")
                    { 
                        Data = { { "Command", command.Name } }
                    };
                }
                // (firstByte >> 4) != 0x0a or 0x02
                catch ( InvalidDataException ide )
                {
                    responseBytes = new byte[MaxReadSize];
                    responseBytes[0] = (byte)firstByte;
                    // May throw TimeoutException and be caught below?
                    int bytesRead = Port.Read(responseBytes, 1, responseBytes.Length-1);
                    Response junkResponse = new(responseBytes[0..(bytesRead+1)]);
                    ide.Data.Add("Command", command.Name);
                    ide.Data.Add("Response", junkResponse.ToString());
                    throw;
                }
                // Synchronous Write/Read timeout
                catch ( TimeoutException te )
                {
                    te.Data.Add("Command", command.Name);
                    throw;
                }
                finally
                {
                    Port.Close();
                }
            }
        }
    }
}
