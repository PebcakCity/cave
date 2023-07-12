using NLog;
using System.Net.Sockets;

namespace Cave.DeviceControllers.Projectors.NEC
{
    public class Client
    {
        private static readonly Logger logger = LogManager.GetLogger("NEC.Client");
        //private Socket? socket = null;
        private string? ipAddress = null;
        private int port;

        private Client(NECProjector device, string ip, int port=7142)
        {
            this.ipAddress = ip;
            this.port = port;

            // Should we try to connect to the device initially to see if it's online?
            // If so, see here: https://stackoverflow.com/questions/8145479/can-constructors-be-async
            // If we decide we want to connect to the device using connectAsync
            // immediately from within the Client constructor, we might use a factory
            // pattern to return a Task<Client> to the caller (NECProjector).
            // So this constructor would be marked private, and there would be an
            // async factory function to call from NECProjector().

            // OF COURSE, the NECProjector constructor would also have to be declared async
            // which is impossible as well.  So there would need to be a factory function
            // for creating that as well and it would need to be called by whatever
            // object creates the NECProjector instance (ProjectorTest.razor's
            // protected async Task OnInitializedAsync() method).
        }


        public static async Task<Client> Create(NECProjector device, string ip, int port=7142)
        {
            try
            {
                logger.Info("Creating new NEC.Client instance.");
                Client instance = new(device, ip, port);
                logger.Info($"Attempting connection to: {ip}:{port}");
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
                using (Socket socket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp))
                {
                    CancellationTokenSource cts = new();
                    cts.CancelAfter(3000);
                    var token = cts.Token;
                    await socket.ConnectAsync(this.ipAddress!, this.port, token);
                    logger.Info($"Connection success.");
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch(OperationCanceledException)
            {
                /* Rethrow with a useful error message */
                string error = "Connection attempt timed out.";
                logger.Error(error);
                throw new OperationCanceledException(error);
            }
            catch(Exception ex)
            {
                logger.Error($"Exception occurred: {ex.Message}");
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
                await socket.ConnectAsync(ipAddress!, port);

                logger.Info($"Sending command: {toSend}");
                int bytesSent = await socket.SendAsync(toSend.Data.ToArray(), SocketFlags.None, token);
                logger.Debug($"Sent {bytesSent} bytes.");

                cts.CancelAfter(2000);
                int bytesRead = await socket.ReceiveAsync(responseBytes, SocketFlags.None, token);
                logger.Debug($"Read {bytesRead} bytes.");

                socket.Shutdown(SocketShutdown.Both);
                Response response = new(responseBytes[0..bytesRead]);
                logger.Info($"Received response: {response}");
                return response;
            }
            catch(OperationCanceledException)
            {
                string error = "SendCommandAsync: Operation timed out.";
                logger.Error(error);
                throw new OperationCanceledException(error);
            }
            catch(Exception ex)
            {
                logger.Error($"Exception occurred: {ex.Message}");
                throw;
            }
        }

        /**
        * I still like the idea of calling an event handler in NECProjector
        * to handle response events and parse the responses, calling out any
        * errors that were reported, etc.  NECProjector and other controller
        * classes will need a reference to the IToastService instance of their
        * associated DeviceController component instance.  They'll need an
        * event handler method, presumably async void?  Client.SendCommandAsync
        * wraps up the Command and Response and calls an event handler that
        * passes them to the controller for processing.  The responses are
        * parsed asynchronously and whatever the result was is passed to the
        * IToastService to show to the user.
        * Also thinking of implementing my own exceptions to encapsulate projector
        * command errors.  Don't exactly know how that will be affected by
        * choice of return type (void or Task) for any event handlers I use.
        *
        * Could possibly implement my own exceptions and add minimal parsing
        * to each NECProjector.<Call this command> method, just to check for
        * the most common errors and throw those custom exceptions back up
        * the stack to the DeviceController component, which then calls the
        * IToastService itself.  Thus avoiding any need for a response event
        * handler.  The downside to this is that it doesn't provide an obvious
        * way to notify the user when a command /succeeds/, just when something
        * fails.  If we want to be able to read a success response, parse it
        * and say "input switch successful", we need a handler for that.
        *
        * I'd like most of the logic to be in NECProjector, but it would be
        * easy to have a method or two in Client that reads the response and
        * returns bools based on whether the responses indicate success or
        * failure.  Or throws an NECError is the command errors out.
        * Easy but messy.
        */

        /* Old */
        /*
        private async Task connectAsync(string ip, int port, bool initialCheck=false)
        {
            try
            {
                if( socket is not null && socket.Connected )
                    socket.Close();
                
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                    ProtocolType.Tcp);

                CancellationTokenSource cts = new();
                cts.CancelAfter(3000);
                var token = cts.Token;
                
                await socket.ConnectAsync(ip, port, token);
            }
            catch(Exception ex)
            {
                logger.Error($"Connection error: {ex}");
                throw;
            }
            finally
            {
                if( initialCheck && socket!.Connected )
                    socket.Close();
            }
        }
        */

    }
}
