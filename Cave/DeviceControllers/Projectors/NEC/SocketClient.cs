using System.Net.Sockets;

using NLog;

namespace Cave.DeviceControllers.Projectors.NEC
{
    /* 
     * Would like to redo this one similar to the SerialClient now that it's starting to come along.  Try to get the
     * shape of the response by the first byte and then get the rest.
     */

    public class SocketClient : INECClient
    {
        private static readonly Logger Logger = LogManager.GetLogger("NEC.SocketClient");
        private readonly string IPAddress;
        private readonly int Port;
        private const int MaxReadSize = 256;
        private const int MaxWaitMilliseconds = 1000;

        private SocketClient(string address, int port)
        {
            (IPAddress, Port) = (address, port);
        }

        public static async Task<SocketClient> Create(string address, int port)
        {
            Logger.Info("Creating new NEC.SocketClient instance.");
            SocketClient instance = new(address, port);
            Logger.Info($"Attempting connection to: {address}:{port}");
            await instance.TestConnection();
            return instance;
        }

        private async Task TestConnection()
        {
            using Socket socket = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using CancellationTokenSource cts = new();

            try
            {
                cts.CancelAfter(MaxWaitMilliseconds);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);
                Logger.Info($"Connection success.");
                socket.Shutdown(SocketShutdown.Both);
            }
            catch(OperationCanceledException)
            {
                // Rethrow with a slightly more useful error message
                throw new TimeoutException($"Timeout connecting to {IPAddress}:{Port}");
            }
            catch(Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
        }

        //TODO: Job for rest of the week: test & debug this quite thoroughly.
        // Particularly:
        // 1) set a breakpoint after connecting to the projector and after reading the first byte,
        // then unplug the projector from the network after these breakpoints and see what paths the code takes.
        // 2) Try intentionally setting the first byte to something wrong and see what path the code takes (through
        // InvalidDataException) and see if the read operation completes.  See what happens both with and without the
        // projector connected to the network.
        // 3) Test with every command

        public async Task<Response> SendCommandAsync(Command command)
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using CancellationTokenSource cts = new();

            byte [] responseData;
            byte [] firstByte = new byte[1];
            try
            {
                cts.CancelAfter(MaxWaitMilliseconds);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);

                // Send command
                Logger.Debug($"Sending command: {command}");
                cts.CancelAfter(MaxWaitMilliseconds);
                int bytesSent = await socket.SendAsync(command.Data.ToArray(), SocketFlags.None, cts.Token);
                Logger.Debug($"Sent {bytesSent} bytes.");

                // Read first byte of response
                cts.CancelAfter(MaxWaitMilliseconds);
                int totalBytesRead = await socket.ReceiveAsync(firstByte, SocketFlags.None, cts.Token);
                if ( totalBytesRead < 1 )
                    throw new SocketException();

                // Get length of expected response (or throw an exception)
                // based on whether the first byte indicates command failure,
                // success, or neither (indicating data corruption)
                int expectedLength = (firstByte[0] >> 4) switch
                {
                    0x0a => Command.FailureResponseLengths[command],
                    0x02 => Command.SuccessResponseLengths[command],
                    _ => throw new InvalidDataException("Bad response from device")
                };

                // Add first retrieved byte to array that we will use to craft
                // and return our Response object
                responseData = new byte[expectedLength];
                responseData[0] = (byte)firstByte[0];

                // Allocate another byte array for the next ReceiveAsync
                // operation to use to complete the response
                byte [] responseRemainingBytes = new byte[responseData.Length-1];

                // Finish reading the rest of the response
                cts.CancelAfter(MaxWaitMilliseconds);
                totalBytesRead += await socket.ReceiveAsync(responseRemainingBytes, SocketFlags.None, cts.Token);
                Logger.Debug($"Read {totalBytesRead} bytes total.");

                // Copy the rest of the response to the array we'll use to
                // create the Response object
                responseRemainingBytes.CopyTo(responseData, 1);

                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseData[0..totalBytesRead]);
                Logger.Debug($"Received response: {response}");
                return response;
            }
            // ConnectAsync/SendAsync/ReceiveAsync
            catch(OperationCanceledException)
            {
                throw new TimeoutException("Socket operation timed out")
                { 
                    Data = { { "Command", command.Name } }
                };
            }
            // First byte indicates bad response
            catch(InvalidDataException ide)
            {
                responseData = new byte[MaxReadSize];
                byte[] junk = new byte[MaxReadSize-1];
                int bytesRead = await socket.ReceiveAsync(junk, SocketFlags.None, cts.Token);
                responseData[0] = firstByte[0];
                junk.CopyTo(responseData, 1);
                Response junkResponse = new(responseData[0..(bytesRead+1)]);
                ide.Data.Add("Command", command.Name);
                ide.Data.Add("Response", junkResponse.ToString());
                throw;
            }
            // ArgumentNullException (address), ArgumentOutOfRangeException (port),
            // SocketException (invalid socket options, can't read), SecurityException
            catch ( Exception ex)
            {
                Logger.Error($"Exception occurred: {ex.Message}");
                throw;
            }
        }

        // Keep this here until we've tested exhaustively (easier than going back through old commits to retrieve)

        public async Task<Response> SendCommandAsyncOriginal( Command toSend )
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using CancellationTokenSource cts = new();

            try
            {
                byte[] responseBytes = new byte[512];

                cts.CancelAfter(2000);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);

                Logger.Debug($"Sending command: {toSend}");
                cts.CancelAfter(2000);
                int bytesSent = await socket.SendAsync(toSend.Data.ToArray(), SocketFlags.None, cts.Token);
                Logger.Debug($"Sent {bytesSent} bytes.");

                cts.CancelAfter(2000);
                int bytesRead = await socket.ReceiveAsync(responseBytes, SocketFlags.None, cts.Token);
                Logger.Debug($"Read {bytesRead} bytes.");

                if ( bytesRead < 1 )
                    throw new SocketException();

                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseBytes[0..bytesRead]);
                Logger.Debug($"Received response: {response}");
                return response;
            }
            catch ( OperationCanceledException )
            {
                string error = "SendCommandAsync: Operation timed out.";
                Logger.Error(error);
                throw new OperationCanceledException(error, cts.Token);
            }
            catch ( Exception ex )
            {
                Logger.Error($"Exception occurred: {ex.Message}");
                throw;
            }
        }
    }
}
