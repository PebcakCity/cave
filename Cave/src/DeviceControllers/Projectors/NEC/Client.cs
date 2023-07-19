using NLog;
using System.Net.Sockets;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class Client
    {
        private static readonly Logger Logger = LogManager.GetLogger("NEC.Client");
        private readonly string? IpAddress = null;
        private readonly int Port=7142;
        private readonly NECProjector Device;

        private Client(NECProjector device, string ip, int port=7142)
        {
            this.IpAddress = ip;
            this.Port = port;
            this.Device = device;
        }

        public static async Task<Client> Create(NECProjector device, string ip, int port=7142)
        {
            try
            {
                Logger.Info("Creating new NEC.Client instance.");
                Client instance = new(device, ip, port);
                Logger.Info($"Attempting connection to: {ip}:{port}");
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
                using Socket socket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);
                CancellationTokenSource cts = new();
                cts.CancelAfter(3000);
                var token = cts.Token;
                await socket.ConnectAsync(this.IpAddress!, this.Port, token);
                Logger.Info($"Connection success.");
                socket.Shutdown(SocketShutdown.Both);
            }
            catch(OperationCanceledException)
            {
                /* Rethrow with a useful error message */
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

                CancellationTokenSource cts = new();
                CancellationToken token = cts.Token;
                cts.CancelAfter(2000);

                using Socket socket = new(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(IpAddress!, Port);

                Logger.Info($"Sending command: {toSend}");
                int bytesSent = await socket.SendAsync(toSend.Data.ToArray(), SocketFlags.None, token);
                Logger.Debug($"Sent {bytesSent} bytes.");

                cts.CancelAfter(2000);
                int bytesRead = await socket.ReceiveAsync(responseBytes, SocketFlags.None, token);
                Logger.Debug($"Read {bytesRead} bytes.");

                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseBytes[0..bytesRead]);
                Logger.Info($"Received response: {response}");
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
