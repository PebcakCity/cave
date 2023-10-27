using System.Diagnostics;
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

        public async Task<Response> SendCommandAsync(Command command)
        {
            return await SendCommandAsyncNew(command);
            //return await SendCommandAsyncOriginal(command);
        }

        public async Task<Response> SendCommandAsyncNew(Command command)
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using CancellationTokenSource cts = new();

            byte [] responseData;
            byte [] responseFirstByte = new byte[1];
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                // Connect to the device
                cts.CancelAfter(MaxWaitMilliseconds);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);

                // Send command
                Logger.Debug($"Sending command: {command}");
                cts.CancelAfter(MaxWaitMilliseconds);
                int bytesSent = await socket.SendAsync(command.Data.ToArray(), SocketFlags.None, cts.Token);
                Logger.Debug($"Sent {bytesSent} bytes.");

                // Read first byte of response
                cts.CancelAfter(MaxWaitMilliseconds);
                int totalBytesRead = await socket.ReceiveAsync(responseFirstByte, SocketFlags.None, cts.Token);
                if ( totalBytesRead != 1 )
                    throw new SocketException();

                // Get length of expected response (or throw an exception)
                // based on whether the first byte indicates command failure,
                // success, or neither (indicating data corruption)
                int expectedLength = (responseFirstByte[0] >> 4) switch
                {
                    0x0a => Command.FailureResponseLengths[command],
                    0x02 => Command.SuccessResponseLengths[command],
                    _ => throw new InvalidDataException("Bad response from device")
                };

                // Allocate array for storage of whole response and add first
                // retrieved byte to it.
                responseData = new byte[expectedLength];
                responseData[0] = responseFirstByte[0];

                // Allocate another byte array for the next ReceiveAsync
                // operation to use to complete the response.
                byte [] responseRemainingBytes = new byte[expectedLength-1];

                // Finish reading the rest of the response.
                cts.CancelAfter(MaxWaitMilliseconds);
                totalBytesRead += await socket.ReceiveAsync(responseRemainingBytes, SocketFlags.None, cts.Token);
                Logger.Debug($"Read {totalBytesRead} bytes total.");

                // Copy the rest of the response to the array we'll use to
                // create the Response object
                responseRemainingBytes.CopyTo(responseData, 1);

                // Gracefully shutdown the socket, create Response and return it
                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseData[0..totalBytesRead]);
                Logger.Debug($"Received response: {response}");
                timer.Stop();
                Logger.Info($"Time taken: {timer.ElapsedMilliseconds}");
                return response;
            }
            // ConnectAsync/SendAsync/ReceiveAsync timed out
            catch(OperationCanceledException)
            {
                throw new TimeoutException("Socket operation timed out")
                { 
                    Data = { { "Command", command.Name } }
                };
            }
            // First byte indicates garbled response?
            catch(InvalidDataException ide)
            {
                responseData = new byte[MaxReadSize];
                byte[] junk = new byte[MaxReadSize-1];
                int bytesRead = await socket.ReceiveAsync(junk, SocketFlags.None, cts.Token);
                responseData[0] = responseFirstByte[0];
                junk.CopyTo(responseData, 1);
                Response junkResponse = new(responseData[0..(bytesRead+1)]);
                ide.Data.Add("Command", command.Name);
                ide.Data.Add("Response", junkResponse.ToString());
                throw;
            }
        }

        // Keep this here until we've tested exhaustively (easier than going back through old commits to retrieve)

        public async Task<Response> SendCommandAsyncOriginal( Command command )
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using CancellationTokenSource cts = new();

            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                byte[] responseBytes = new byte[MaxReadSize];

                cts.CancelAfter(MaxWaitMilliseconds);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);

                Logger.Debug($"Sending command: {command}");
                cts.CancelAfter(MaxWaitMilliseconds);
                int bytesSent = await socket.SendAsync(command.Data.ToArray(), SocketFlags.None, cts.Token);
                Logger.Debug($"Sent {bytesSent} bytes.");

                cts.CancelAfter(MaxWaitMilliseconds);
                int bytesRead = await socket.ReceiveAsync(responseBytes, SocketFlags.None, cts.Token);
                Logger.Debug($"Read {bytesRead} bytes.");

                if ( bytesRead < 1 )
                    throw new SocketException();

                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseBytes[0..bytesRead]);
                Logger.Debug($"Received response: {response}");
                timer.Stop();
                Logger.Info($"Time taken: {timer.ElapsedMilliseconds}");
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
