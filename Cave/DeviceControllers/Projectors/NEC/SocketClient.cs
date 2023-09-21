using System.Net.Sockets;

using NLog;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class SocketClient : INECClient
    {
        private static readonly Logger Logger = LogManager.GetLogger("NEC.SocketClient");
        private readonly string IPAddress;
        private readonly int Port;

        private SocketClient(string address, int port)
        {
            (IPAddress, Port) = (address, port);
        }

        // todo: refactor SocketClient to take an IP address & port again (non-nullable) as part of the larger refactor to
        // make NDCI.Port nullable.  This way if port numbers are not provided in configuration, the controllers and/or
        // controller helpers like *Client can still provide their own default.

        public static async Task<SocketClient> Create(string address, int port)
        {
            try
            {
                Logger.Info("Creating new NEC.SocketClient instance.");
                SocketClient instance = new(address, port);
                Logger.Info($"Attempting connection to: {address}:{port}");
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
            try
            {
                using Socket socket = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                using CancellationTokenSource cts = new();
                cts.CancelAfter(3000);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);
                Logger.Info($"Connection success.");
                socket.Shutdown(SocketShutdown.Both);
            }
            catch(OperationCanceledException)
            {
                /* Rethrow with a slightly more useful error message */
                string error = "Connection attempt timed out.";
                Logger.Error(error);
                throw new OperationCanceledException(error);
            }
            catch(Exception ex)
            {
                Logger.Error($"Exception occurred: {ex.Message}");
                throw;
            }
        }

        public async Task<Response> SendCommandAsync(Command toSend)
        {
            try
            {
                byte[] responseBytes = new byte[512];

                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                using CancellationTokenSource cts = new();

                cts.CancelAfter(2000);
                await socket.ConnectAsync(IPAddress, Port, cts.Token);

                Logger.Debug($"Sending command: {toSend}");
                cts.CancelAfter(2000);
                int bytesSent = await socket.SendAsync(toSend.Data.ToArray(), SocketFlags.None, cts.Token);
                Logger.Debug($"Sent {bytesSent} bytes.");

                cts.CancelAfter(2000);
                int bytesRead = await socket.ReceiveAsync(responseBytes, SocketFlags.None, cts.Token);
                Logger.Debug($"Read {bytesRead} bytes.");

                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseBytes[0..bytesRead]);
                Logger.Debug($"Received response: {response}");
                return response;
            }
            catch(OperationCanceledException)
            {
                string error = "SendCommandAsync: Operation timed out.";
                Logger.Error(error);
                throw new OperationCanceledException(error);
            }
            catch(Exception ex)
            {
                Logger.Error($"Exception occurred: {ex.Message}");
                throw;
            }
        }
    }
}
