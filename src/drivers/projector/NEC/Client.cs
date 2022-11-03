using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


/*

Socket errors that are fatal and require opening a new one:
    - Broken Pipe / "Shutdown" - "A broken pipe error is seen when the remote end of the connection is closed gracefully.
        Solution: This exception usually arises when the socket operations performed on either end are not synced."
        https://www.java67.com/2019/02/7-common-socket-errors-and-exception-in-java.html
        
        Basically this will happen after sending malformed commands and trying to read responses that never arrive.

    - Connection Reset - "This exception appears when the remote connection is unexpectedly and forcefully closed due
        to various reasons like application crashes, system reboot, the hard close of the remote host. Kernel from
        the remote system sends out packets with the RST bit to the local system."
        https://www.java67.com/2019/02/7-common-socket-errors-and-exception-in-java.html

        "SocketError.ConnectionReset / WSAECONNRESET / 10054 - The remote side has abortively closed the connection.
        This is commonly caused by the remote process exiting or the remote computer being shut down. However, some
        software (especially server software) is written to abortively close connections as a normal practice, since
        this does reclaim server resources more quickly than a graceful close. Therefore, this is not necessarily
        indicative of an actual error condition; if the communication was complete (and the socket was about to be
        closed anyway), then this error should just be ignored."
        https://blog.stephencleary.com/2009/05/error-handling.html
        
        Powering off some NEC projectors (including the one I'm testing with) will cause them to close a socket this way.

*/

namespace cave.drivers.projector.NEC {

    public class ClientConnectionEventArgs: EventArgs {
        public string Message { get; set; }
        public ClientConnectionEventArgs( string message ) { Message = message; }
    }

    public class Client {

#region PrivateFields

        private IPAddress ipAddress;
        private Socket socket;
        private const int READ_BUFFER_SIZE = 512;
        private byte[] readBuffer = new byte[READ_BUFFER_SIZE];
        private int reconnectDelay = 5;

        private NEC device;
        private MainWindow window;
        private ILogger logger;

#endregion

#region PublicFields

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

#region PrivateMethods

        /// <summary>
        /// Synchronously attempts to connect to the device
        /// and notifies subscribers to ClientConnected if successful.
        /// Currently unused.
        /// </summary>
        /// <param name="retriesAllowed">Number of times to retry before giving up.</param>
        /// <param name="timeout">Time (in seconds) before a retry attempt is canceled.</param>
        private void connect( int retriesAllowed=3 ) {
            int attempts = 0;
            if( socket != null ) 
                socket.Close();
            socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            while( !socket.Connected && attempts < retriesAllowed ) {
                try {
                    ++attempts;
                    socket.Connect( ipAddress, Port );
                    logger.LogInformation( "Connected to {addr}:{port}", Address, Port );

                    /* Notify subscriber (NEC) to begin communications */
                    if( ClientConnected != null )
                        ClientConnected( this, new ClientConnectionEventArgs( $"Connected to {Address}:{Port}" ) );

                    socket.SendTimeout = 500;
                    socket.ReceiveTimeout = 1000;

                } catch( Exception ex ) {
                    logger.LogError( "connect() :: (Attempt #{a}) Error connecting to device: {msg}", attempts, ex.Message );
                }
            }
            if( !socket.Connected ) {
                logger.LogError( "connect(retriesAllowed={r}) :: Fatal: Failed to connect to device.", retriesAllowed );
            }
        }

        /// <summary>
        /// Closes an existing socket and notifies subscribers to ClientDisconnected.
        /// Currently unused
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

            if( socket != null ) {
                socket.Close();
            }

            socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

            while( !socket.Connected && attempts < retriesAllowed ) {
                try {
                    ++attempts;
                    CancellationTokenSource cts = new();
                    cts.CancelAfter(timeout * 1000);
                    var token = cts.Token;

                    await socket.ConnectAsync( ipAddress, Port, token );
                    logger.LogInformation( "Connected to {addr}:{port}", Address, Port );

                    /* Notify subscriber (NEC) to begin communications */
                    if( ClientConnected != null )
                        ClientConnected( this, new ClientConnectionEventArgs($"Connected to {Address}:{Port}") );

                    /* These timeouts are for sync operations, no bearing on async ones */
                    socket.SendTimeout = 500;
                    socket.ReceiveTimeout = 1000;
                    
                } catch( Exception ex ) {
                    logger.LogError( "connectAsync() :: (Attempt #{a}) Error connecting to device: {msg}", attempts, ex.Message );
                }
            }
            if( !socket.Connected ) {
                logger.LogError( "connectAsync(retriesAllowed={r}, timeout={t}) :: Fatal: Failed to connect to device.", retriesAllowed, timeout );
            }
        }

        /// <summary>
        /// Used by SendCommandAsync on certain socket errors to notify the device class that
        /// the device has closed the socket and that it should cease sending status update commands 
        /// while we are disconnected.  It also calls connectAsync() to attempt to reconnect with
        /// the default timeout and number of retries.
        /// </summary>
        private void onDisconnected( object sender, ClientConnectionEventArgs args ) {
            /* Notify subscriber (NEC) */
            if( ClientDisconnected != null ){
                ClientDisconnected( this, args );
            }
            Task.Run( async () => {
                logger.LogDebug( "onDisconnected() :: {msg}", args.Message );
                Thread.Sleep( reconnectDelay * 1000 );
                await connectAsync();
            } );
        }

        /// <summary>
        /// Computes and returns the one-byte checksum of a byte sequence.
        /// </summary>
        /// <param name="bytes">An IEnumerable<byte> object</param>
        private byte checksum( IEnumerable<byte> bytes ) {
            byte total = 0x00;
            foreach( byte b in bytes )
                total += b;
            return (byte)(total & 0xFF);
        }

        /// <summary>
        /// Helper for SendCommand/SendCommandAsync.
        /// Appends optional arguments and an optional checksum to a Command and returns its new byte array.
        /// If arguments are appended to the command, checksum is calculated and appended regardless,
        /// as failing to include the checksum will cause the command to error and can have negative
        /// repercussions on maintaining connection integrity (the device can drop the connection if it gets out of sync).
        /// </summary>
        /// <param name="command">The NEC.Command object to append to.</param>
        /// <param name="checksum">Whether a checksum is required by this command
        ///     (should be true if args is non-empty or command will fail).</param>
        /// <param name="args">A list of single-byte arguments to append before the checksum is appended.</param>
        private byte[] prepareCommand( Command command, bool checksum, params object[] args ){
            int argsAppended = 0;
            logger.LogDebug( "Preparing command '{command}'", command.Name );
            var cmdBytes = command.Bytes.ToList();
            foreach( object arg in args ) {
                if( // arg is int || arg is byte || 
                    arg is NEC.Input || arg is NEC.DeviceInfo.Lamp.LampNumber || arg is NEC.DeviceInfo.Lamp.LampInfo ) {
                    ++argsAppended;
                    cmdBytes.Add( Convert.ToByte(arg) );
                } else {
                    logger.LogError("prepareCommand() :: Argument type unrecognized: {arg}", arg);
                }
            }
            if( checksum || argsAppended > 0 )
                cmdBytes.Add( this.checksum(cmdBytes) );
            logger.LogDebug("Sending bytes: {cmd}", Response.FromBytes(cmdBytes));
            return cmdBytes.ToArray();
        }

#endregion

#region PublicMethods


        /// <summary>
        /// Send a Command sychronously and return the Response.
        /// Currently unused.
        /// </summary>
        /// <param name="command">The NEC.Command to send.</param>
        /// <param name="checksumRequired">Whether a checksum is required by this command
        ///     (should be true if args is non-empty or command will fail).</param>
        /// <param name="args">A list of single-byte arguments to append before the checksum is appended.</param>
        public Response SendCommand( Command command, bool checksumRequired=false, params object[] args ) {
            try {
                socket.Send( prepareCommand( command, checksumRequired, args ) );
                int bytesRead = socket.Receive( readBuffer, 0, readBuffer.Length, SocketFlags.None );
                if( bytesRead <= 0 ) {
                    logger.LogError( $"SendCommand({command.Name}) :: Failed to get a response, check device connection." );
                    /* Should contain whatever actual socket error last occurred if one actually did occur */
                    throw new SocketException();
                }
                else {
                    byte[] dataReceived = new byte[bytesRead];
                    Buffer.BlockCopy( readBuffer, 0, dataReceived, 0, bytesRead );
                    return Response.FromBytes( dataReceived );
                } 
            } catch( SocketException ex ) {
                
                /* Only attempt to reconnect if it's determined that the projector hung up on us, either correctly or incorrectly.
                   Otherwise it stays bound to the old socket and we'll keep failing to reconnect on a new socket.

                   Check if the error code matches the local system's ECONNRESET ("connection reset by peer") or EPIPE (Unix's "broken pipe") code.
                   .NET maps ECONNRESET to SocketError.ConnectionReset and EPIPE to SocketError.Shutdown */

                if( ex.SocketErrorCode == SocketError.ConnectionReset ||
                    ex.SocketErrorCode == SocketError.Shutdown )
                    onDisconnected( this, new ClientConnectionEventArgs( $"Connection dumped by device. Attempting to reconnect in {reconnectDelay}s..." ) );
                else
                    logger.LogError( $"SendCommand({command.Name}) :: SocketException ({(int)ex.SocketErrorCode}) occurred!!!: {ex.Message}" );

            } catch( Exception ex ) {
                logger.LogError( $"SendCommand({command.Name}) :: Exception occurred: {ex.Message}" );
            }

            return null;
        }


        /// <summary>
        /// Send a Command and await the Response asynchronously.
        /// </summary>
        /// <param name="command">The NEC.Command to send.</param>
        /// <param name="checksumRequired">Whether a checksum is required by this command
        ///     (should be true if args is non-empty or command will fail).</param>
        /// <param name="args">A list of single-byte arguments to append before the checksum is appended.</param>
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

                    /* For flexibility, we can use an event-driven model based on the ResponseReceived event... */
                    if( ResponseReceived != null ) {
                        ResponseReceived(
                            this,
                            new ResponseEventArgs(
                                response,
                                command
                             )
                        );
                    }
                    /* ...or a direct return of type Task<Response>.  It is up to the caller to decide how to receive the response. */
                    return response;
                }
            } catch( SocketException ex ) {
                
                /* Only attempt to reconnect if it's determined that the projector hung up on us, either correctly or incorrectly.
                   Otherwise it stays bound to the old socket and we'll keep failing to reconnect on a new socket.

                   Check if the error code matches the local system's ECONNRESET ("connection reset by peer") or EPIPE (Unix's "broken pipe") code.
                   .NET maps ECONNRESET to SocketError.ConnectionReset and EPIPE to SocketError.Shutdown */

                if( ex.SocketErrorCode == SocketError.ConnectionReset ||
                    ex.SocketErrorCode == SocketError.Shutdown )
                    /* Handle this a little softer in the log, don't log an actual error */
                    onDisconnected( this, new ClientConnectionEventArgs( $"Connection dumped by device. Attempting to reconnect in {reconnectDelay}s..." ) );
                else
                    /* Anything else should be shouted from the rooftops */ 
                    logger.LogError( $"SendCommandAsync({command.Name}) :: SocketException ({(int)ex.SocketErrorCode}) occurred!!!: {ex.Message}" );

            } catch( Exception ex ) {
                if( ex is OperationCanceledException )
                    /* Soft explanation */
                    logger.LogWarning( $"SendCommandAsync({command.Name}) :: Command timed out." );
                else
                    /* As above, let someone know there was a real problem */
                    logger.LogError( $"SendCommandAsync({command.Name}) :: Exception occurred: {ex.Message}" );
            }
            return null;
        }

#endregion

    }

}
