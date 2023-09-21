using System;
using System.Collections.Generic;
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
    /// I'm a little surprised it works at all, so I'm calling it a win for now.
    /// </summary>
    public class SerialClient : INECClient
    {
        private static readonly Logger Logger = LogManager.GetLogger("NEC.SerialClient");

        private readonly string PortName;
        private readonly int Baudrate;

        private readonly SerialPort Port;
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

        public async Task<Response> SendCommandAsync(Command toSend)
        {
            //0. open port
            //1. do a synchronous write
            //2. delay for ~ 1/10 to 2/10 of a second to get the whole response
            //3. do a BaseStream.ReadAsync() OR Port.Read()
            //4. take the data retrieved, package it in a Response & return it
            try
            {
                byte[] responseBytes = new byte[512];

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
                return response;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception occurred: {ex.Message}");
                throw;
            }
            finally
            {
                Port.Close();
            }
        }

    }
}
