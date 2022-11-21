using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace cave.drivers.projector.NEC {

    /// <summary>
    /// Argument passed when a connection is made or broken, contains a message to display/log.
    /// </summary>
    public class ClientConnectionEventArgs: EventArgs {
        public string Message { get; set; }
        public ClientConnectionEventArgs( string message ) { Message = message; }
    }

    /// <summary>
    /// NEC projector network connection client.  Handles all connection details
    /// as well as sending/receiving data.
    /// </summary>
    public class Client {

#region Private fields

        private IPAddress ipAddress;
        private Socket socket;
        private const int READ_BUFFER_SIZE = 512;
        private byte[] readBuffer = new byte[READ_BUFFER_SIZE];
        private int reconnectDelay = 5;
        private bool lastCommandTimedOut;

        private NEC device;
        private MainWindow window;
        private ILogger logger;

#endregion

#region Public fields

        public string Address;
        public int Port;

#endregion

#region Events

        /// <summary>
        /// An event triggered when a Response is received from the device and packaged for processing.
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseReceived;

        /// <summary>
        /// An event triggered when the Client successfully connects to the device.
        /// </summary>
        public event EventHandler<ClientConnectionEventArgs> ClientConnected;

        /// <summary>
        /// An event triggered when the Client disconnects from the device.
        /// </summary>
        public event EventHandler<ClientConnectionEventArgs> ClientDisconnected;

#endregion

#region Constructors

        /// <summary>
        /// Create a new client and attempt to connect to the device
        /// </summary>
        /// <param name="device">The NEC instance that owns this Client.</param>
        /// <param name="window">A reference to the MainWindow class for sending event notifications.</param>
        /// <param name="ip">IP address of the device.</param>
        /// <param name="port">Port to connect to.</param>
        public Client( NEC device, MainWindow window, string ip, int port=7142 ) {
            try{
                this.Address = ip;
                this.Port = port;
                this.ipAddress = IPAddress.Parse( ip );
                this.device = device;
                this.window = window;
                this.logger = Program.LogFactory.CreateLogger( "NEC.Client" );
                logger.LogDebug( ":: constructed" );
                Task.Run( async () => {
                    await connectAsync();
                });
            } catch (Exception ex) {
                logger.LogError("Error occurred: {error}", ex.Message);
            }
        }

#endregion

#region Private methods

        /// <summary>
        /// Closes an existing socket and notifies subscribers to ClientDisconnected.
        /// </summary>
        private void disconnect() {
            if( socket != null ) {
                socket.Close();
                if( ClientDisconnected != null ) {
                    ClientDisconnected( this, new ClientConnectionEventArgs("Disconnected.") );
                }
            }
        }

        /// <summary>
        /// Attempts to connect to the device asynchronously with a timeout and notifies subscribers to ClientConnected if successful.
        /// </summary>
        /// <param name="retriesAllowed">Number of times to retry before giving up.</param>
        /// <param name="timeout">Time (in seconds) before a retry attempt is canceled.</param>
        private async Task connectAsync( int retriesAllowed=5, int timeout=3 ) {
            int attempts = 0;
            bool connected = false;

            if( socket != null ) {
                socket.Close();
            }

            while( !connected && attempts < retriesAllowed ) {
                try {
                    ++attempts;

                    socket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp
                    );

                    CancellationTokenSource cts = new();
                    cts.CancelAfter(timeout * 1000);
                    var token = cts.Token;

                    await socket.ConnectAsync( ipAddress, Port, token );
                    connected = true;
                    logger.LogInformation( "Connected to {addr}:{port}", Address, Port );

                    /* Notify subscriber (NEC) to begin communications */
                    if( ClientConnected != null )
                        ClientConnected( this, new ClientConnectionEventArgs($"Connected to {Address}:{Port}") );

                    /* These timeouts are for sync operations, no bearing on async ones */
                    socket.SendTimeout = 500;
                    socket.ReceiveTimeout = 1000;
                } catch( OperationCanceledException ) {
                    logger.LogWarning( "connectAsync() :: (Attempt #{attempt}) Timed out.", attempts );
                } catch( Exception ex ) {
                    logger.LogError( "connectAsync() :: (Attempt #{attempt}) {errorType} : {errorMsg}", attempts, ex.GetType(), ex.Message );
                }
            }
            if( !socket.Connected ) {
                logger.LogError( "connectAsync(retriesAllowed={r}, timeout={t}) :: Fatal: Failed to connect to device.", retriesAllowed, timeout );
                /* After finally failing to connect, leave behind a fresh unopened socket.
                   Future attempts to send/receive on this socket (ie. user clicks button) will fail
                   with SocketError.Shutdown, causing another cycle of connection attempts. */
                socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp
                );
            }
        }

        /// <summary>
        /// Used by SendCommandAsync on certain socket errors to notify the device class that
        /// the device has closed the socket and that it should cease sending status update requests 
        /// while we are disconnected.  It also checks to see if we should attempt to reconnect.
        /// </summary>
        /// <param name="args">ClientConnectionEventArgs object containing a string explaning what's happened.</param>
        /// <param name="reconnect">Whether to attempt to reconnect immediately.</param>
        private void onDisconnected( object sender, ClientConnectionEventArgs args, bool reconnect=true ) {
            /* Notify subscriber (NEC) */
            if( ClientDisconnected != null ){
                ClientDisconnected( this, args );
            }
            if( reconnect ) {
                Task.Run( async () => {
                    logger.LogWarning( $"Attempting to reconnect in {reconnectDelay}s..." );
                    await Task.Delay( reconnectDelay * 1000 );
                    await connectAsync();
                } );
            }
        }

        /// <summary>
        /// Computes and returns the one-byte checksum of a byte enumerable.
        /// </summary>
        /// <param name="bytes">An IEnumerable<byte> object</param>
        private byte checksum( IEnumerable<byte> bytes ) {
            byte total = 0x00;
            foreach( byte b in bytes )
                total += b;
            return (byte)(total & 0xFF);
        }

        /// <summary>
        /// Helper for SendCommandAsync.
        /// Appends optional arguments and an optional checksum to a Command and returns the new byte array.
        /// If arguments are appended to the command, checksum is calculated and appended regardless.
        /// </summary>
        /// <param name="command">The NEC.Command object to prepare.</param>
        /// <param name="checksum">Whether a checksum is required by this command
        ///     (should be true if args is non-empty or command will fail).</param>
        /// <param name="args">Arguments to the command, either passed separately or as an array.</param>
        private byte[] prepareCommand( Command command, bool checksum, params object[] args ){
            int argsAppended = 0;
            logger.LogDebug( "Preparing command '{command}'", command.Name );
            var cmdBytes = command.Bytes.ToList();
            foreach( object arg in args ) {
                if( arg is NEC.Input || arg is NEC.DeviceInfo.Lamp.LampNumber || arg is NEC.DeviceInfo.Lamp.LampInfo ) {
                    ++argsAppended;
                    cmdBytes.Add( Convert.ToByte(arg) );
                } else {
                    logger.LogError("prepareCommand() :: Argument type unsupported: {arg}", arg.GetType().ToString());
                }
            }
            if( checksum || argsAppended > 0 )
                cmdBytes.Add( this.checksum(cmdBytes) );
            logger.LogDebug("Sending bytes: {cmd}", Response.FromBytes(cmdBytes));
            return cmdBytes.ToArray();
        }

#endregion

#region Public methods

        /// <summary>
        /// Sends a Command and awaits the Response asynchronously.
        /// </summary>
        /// <param name="command">The NEC.Command to send.</param>
        /// <param name="checksumRequired">Whether a checksum is required by this command
        ///     (should be true if args is non-empty or command will fail).</param>
        /// <param name="args">Arguments to the command, either passed separately or as an array.</param>
        public async Task<Response> SendCommandAsync( Command command, bool checksumRequired=false, params object[] args ) {
            try{
                CancellationTokenSource cts = new();
                CancellationToken token = cts.Token;
                cts.CancelAfter(3000);

                await socket.SendAsync(
                    prepareCommand( command, checksumRequired, args ),
                    SocketFlags.None,
                    token
                );

                int bytesRead = await socket.ReceiveAsync( readBuffer, SocketFlags.None, token );
                if( bytesRead <= 0 ) {
                    logger.LogError( $"SendCommandAsync({command.Name}) :: Failed to get a response, check device connection." );
                    /* Should contain whatever actual socket error last occurred if one actually did occur */
                    throw new SocketException();
                } else {
                    byte[] dataReceived = new byte[bytesRead];
                    Buffer.BlockCopy( readBuffer, 0, dataReceived, 0, bytesRead );

                    Response response = new Response( dataReceived );

                    if( ResponseReceived != null ) {
                        ResponseReceived(
                            this,
                            new ResponseEventArgs(
                                response,
                                command
                             )
                        );
                    }
                    if( lastCommandTimedOut && ClientConnected != null ) {
                        lastCommandTimedOut = false;
                        ClientConnected( this, new ClientConnectionEventArgs("Connection reestablished?") );
                    }

                    return response;
                }
            } catch( SocketException ex ) {
                
                /* Only attempt to reconnect if it's determined that the projector hung up on us, either correctly or incorrectly.
                   Otherwise it stays bound to the old socket and we'll keep failing to reconnect on a new socket.

                   Check if the error code matches the local system's ECONNRESET ("connection reset by peer") or EPIPE (Unix's "broken pipe") code.
                   .NET maps ECONNRESET to SocketError.ConnectionReset and EPIPE to SocketError.Shutdown */
                switch( ex.SocketErrorCode ) {
                    case SocketError.ConnectionReset:
                        onDisconnected( this, new ClientConnectionEventArgs( $"SendCommandAsync({command.Name}) :: Connection dumped by device." ), true );
                        break;
                    case SocketError.Shutdown:
                        onDisconnected( this, new ClientConnectionEventArgs( $"SendCommandAsync({command.Name}) :: Broken connection." ), true );
                        break;
                    case SocketError.OperationAborted:
                        lastCommandTimedOut = true;
                        onDisconnected( this, new ClientConnectionEventArgs( $"SendCommandAsync({command.Name}) :: Operation timed out." ), false );
                        break;
                    default:
                        logger.LogError( $"SendCommandAsync({command.Name}) :: SocketException ({(int)ex.NativeErrorCode}) occurred!!!: {ex.Message}" );
                        break;
                }

            } catch( Exception ex ) {
                switch( ex ) {
                    case OperationCanceledException _:
                        lastCommandTimedOut = true;
                        onDisconnected( this, new ClientConnectionEventArgs( $"SendCommandAsync({command.Name}) :: Operation timed out." ), false);
                        break;
                    default:
                        logger.LogError( $"SendCommandAsync({command.Name}) :: Exception occurred: {ex.Message}" );
                        break;
                }
            }

            return null;
        }

#endregion

    }

}
