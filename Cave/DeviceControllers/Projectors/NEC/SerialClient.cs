using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLog;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /// <summary>
    /// Client for controlling NEC projectors over a serial port.  A little buggy.
    /// Timing is a little off, particularly with the NECProjector AwaitPowerOn method.
    /// Randomly experiences issues related to partial responses (attempts to read info
    /// that isn't there) while getting PowerState, etc.
    /// 
    /// Been trying to improve it little by little.
    /// </summary>
    public class SerialClient : INECClient
    {
        private static readonly Logger Logger = LogManager.GetLogger("NEC.SerialClient");

        private readonly string PortName;
        private readonly int Baudrate;

        private readonly SerialPort Port;
        private const int MaxReadSize = 256;

        private readonly Dictionary<Command, int> SuccessResponseLengths = new()
        {
            { Command.PowerOn, 6 },
            { Command.PowerOff, 6 },
            { Command.SelectInput, 7 },
            { Command.GetStatus, 22 },
            { Command.GetInfo, 104 },
            { Command.GetLampInfo, 12 },
            { Command.GetErrors, 18 },
            { Command.GetModelNumber, 38 },
            { Command.GetSerialNumber, 24 },
            { Command.VideoMuteOn, 6 },
            { Command.VideoMuteOff, 6 },
            { Command.AudioMuteOn, 6 },
            { Command.AudioMuteOff, 6 },
            { Command.VolumeAdjust, 8 },
        };

        // pretty much universally 8 bytes but allowing possibility of future variance
        private readonly Dictionary<Command, int> FailureResponseLengths = new()
        {
            { Command.PowerOn, 8 },
            { Command.PowerOff, 8 },
            { Command.SelectInput, 8 },
            { Command.GetStatus, 8 },
            { Command.GetInfo, 8 },
            { Command.GetLampInfo, 8 },
            { Command.GetErrors, 8 },
            { Command.GetModelNumber, 8 },
            { Command.GetSerialNumber, 8 },
            { Command.VideoMuteOn, 8 },
            { Command.VideoMuteOff, 8 },
            { Command.AudioMuteOn, 8 },
            { Command.AudioMuteOff, 8 },
            { Command.VolumeAdjust, 8 },
        };

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
                Logger.Error($"Error opening port: {ex}");
                throw;
            }
        }

        public static async Task<SerialClient> Create(string port, int baudrate)
        {
            try
            {
                Logger.Info("Creating new NEC.SerialClient instance.");
                SerialClient instance = new(port, baudrate);
                Logger.Info($"Attempting to open port '{port}' with default settings at {baudrate} baud.");
                await instance.TestConnection();
                return instance;
            }
            catch
            {
                throw;
            }
        }

        private async Task TestConnection()
        {
            // maybe try to open the port, send a test command and log the result?
            await SendCommandAsync(Command.GetInfo);
        }

        public async Task<Response> SendCommandAsync(Command command)
        {
            //return await SendCommandAsyncOriginal(command);
            //return await SendCommandAsync2(command);
            return await SendCommandAsync3(command);
        }

        public async Task<Response> SendCommandAsyncOriginal(Command toSend)
        {
            //0. open port
            //1. do a synchronous write
            //2. delay for ~ 1/10 to 2/10 of a second to get the whole response
            //3. do a BaseStream.ReadAsync() OR Port.Read()
            //4. take the data retrieved, package it in a Response & return it
            
            try
            {
                var timer = Stopwatch.StartNew();
                byte[] responseBytes = new byte[MaxReadSize];

                Port.Open();
                Logger.Debug($"Sending command: {toSend}");
                byte[] cmdBytes = toSend.Data.ToArray();
                Port.Write(cmdBytes, 0, cmdBytes.Length);

                await Task.Delay(100);

                //int bytesRead = await Port.BaseStream.ReadAsync(responseBytes, 0, responseBytes.Length);
                int bytesRead = Port.Read(responseBytes, 0, responseBytes.Length);
                Logger.Debug($"Read {bytesRead} bytes.");
                Response response = new(responseBytes[0..bytesRead]);
                Logger.Debug($"Received response: {response}");
                timer.Stop();
                Logger.Debug($"Took {timer.ElapsedMilliseconds}ms");
                return response;
            }
            finally
            {
                Port.Close();
            }
        }

        public async Task<Response> SendCommandAsync2(Command toSend)
        {
            // Have read that CancellationToken doesn't work properly with SerialPort.BaseStream... testing
            using ( CancellationTokenSource cts = new() )
            {
                try
                {
                    var timer = Stopwatch.StartNew();
                    Port.Open();

                    Logger.Debug($"Sending command: {toSend}");
                    byte[] cmdBytes = toSend.Data.ToArray();
                    await Port.BaseStream.WriteAsync(cmdBytes);

                    await Task.Delay(100);

                    byte[] responseBytes = new byte[MaxReadSize];
                    cts.CancelAfter(100);
                    int bytesRead = await Port.BaseStream.ReadAsync(
                        responseBytes.AsMemory(0, responseBytes.Length), cts.Token);
                    Logger.Debug($"Read {bytesRead} bytes.");

                    Response response = new(responseBytes[0..bytesRead]);
                    Logger.Debug($"Received response: {response}");
                    timer.Stop();
                    Logger.Debug($"Took {timer.ElapsedMilliseconds}ms");
                    return response;
                }
                catch(OperationCanceledException) 
                {
                    throw new TimeoutException("Operation timed out");
                }
                finally
                {
                    Port.Close();
                }
            }
        }

        /**
         * Not much of a discernible difference between the two on Windows.  I did notice something kinda interesting.
         * The pattern of behavior regarding when exceptions are thrown was pretty much identical but this time I
         * noticed that one of the exceptions said that the serial port was already open when trying to open it.
         * It's during that whole AwaitPowerOn business so it's likely a timing issue between waiting 100 ms in the read
         * op and calling multiple read ops in a row.  Should investigate and see if this is what's happening in the
         * synchronous read version that just uses Task.Delay as the only async component...
         * 
         * Other things to try for improving stability/timing on this:
         * 1) Maybe try to use locks somehow to prevent concurrent accesses to the serial port?
         * 
         * 2) The solution below -- kinda ugly but it seems to work.  Since we know from the NEC documentation how long
         *  each response is supposed to be, whether it's an error or not, and also what an error response starts with
         *  (byte with upper 4 bits 'a', 1010)... we keep two collections of expected response lengths for each command,
         *  one for failure, one for success... SendCommandAsync checks the command being sent, sends it, waits a very
         *  short amount of time, gets the firstByte byte to know whether it succeeded or not (and how many more bytes to
         *  get) then sets up a read operation to get the rest and returns it.
         */
        
        public async Task<Response> SendCommandAsync3( Command command )
        {
            using ( var cts = new CancellationTokenSource() )
            {
                int firstByte = 0;
                byte [] responseBytes;
                try
                {
                    Port.Open();

                    Logger.Debug($"Sending command: {command}");
                    byte[] cmdBytes = command.Data.ToArray();
                    Port.Write(cmdBytes, 0, cmdBytes.Length);

                    // Delay  before  beginning  read.  Lowering this is not
                    // advised, seems to make read errors more frequent.
                    await Task.Delay(100);

                    // Get first byte.  -1 indicates the end of the stream.
                    firstByte = Port.ReadByte();
                    if ( firstByte == -1 )
                        throw new EndOfStreamException("Unexpected end of stream");

                    // Get length of expected response based on whether first
                    // byte indicates command success or failure
                    //int expectedLength = ((firstByte>>4)==0x0a) ?
                    //    FailureResponseLengths[command] :
                    //    SuccessResponseLengths[command];

                    // Check for possible data corruption 
                    int expectedLength = (firstByte >> 4) switch
                    {
                        0x0a => FailureResponseLengths[command],
                        0x02 => SuccessResponseLengths[command],
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
                catch ( OperationCanceledException )
                {
                    throw new TimeoutException("Read operation timed out") { 
                        Data = { { "Command", command.Name } }
                    };
                }
                catch ( TimeoutException te )
                {
                    te.Data.Add("Command", command.Name);
                    throw;
                }
                catch ( InvalidDataException ide )
                {
                    responseBytes = new byte[MaxReadSize];
                    responseBytes[0] = (byte)firstByte;
                    int bytesRead = Port.Read(responseBytes, 1, responseBytes.Length-1);
                    Response junkResponse = new(responseBytes[0..(bytesRead+1)]);
                    ide.Data.Add("Command", command.Name);
                    ide.Data.Add("Response", junkResponse.ToString());
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
