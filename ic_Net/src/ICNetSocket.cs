/*
 *  ICNetSocket     single socket for ICNet 
 * 
 * 
 *  Usage:
 *      ICNetSocket(gateway_addr)
 *      initSocket(host, port)          start a socket connection towards a given host/port
 *      writeSocket(data)               write a message to the socket
 *      readSocket(data)                read something from the socket
 *      closeSocket()                   close this socket
 *      isConnected()
 *      getDetectedIP()
 */


using System.Collections;

using System;
using System.IO;
using System.Net.Sockets;

using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;


// socket with SSL
public class ICNetSocket
{
    // right now G-entry's IP & port are hard-written
    private string _gateway_IP = null;
    private int _gateway_port = 0;

    // static variables shared across all 
    protected static string _S_entry = "";

    protected TcpClient _socket = null;

    // secure stream
    //protected NetworkStream _stream = null;
    protected SslStream _stream = null;

    // flags
    protected bool _init = false;
    protected bool _available = false;

    protected string _host = "";
    protected int _port = 0;

    // detected IP of this node
    protected string _detectedIP = "";

    // log file for debug messages
    protected icLog _log = null;

    // The following method is invoked by the RemoteCertificateValidationDelegate.
    public static bool validateServerCertificate(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
    {
        // NOTE: for now we always accept
        return true;

        /*
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

        // Do not allow this client to communicate with unauthenticated servers.
        return false;
         */
    }

    public ICNetSocket(string gateway_IPport)
    {
        // see if we can parse well 
        ICNetLayer.parseIP_Port(gateway_IPport, ref _gateway_IP, ref _gateway_port);
      
        // get log instance
        _log = icLog.getInstance();
    }

    public bool initSocket(string strHost, int nPort)
    {
        if (_init == true)
            return false;

        bool bRet = false;

        try
        {
            byte[] messsage;

            // if _S_entry is unknown, query Gentry once for it
            if (_S_entry == "" || _S_entry == "NO_PROXY")
            {
                _log.debug("_S_entry is null, try to query G-entry for it");
                TcpClient socket = new TcpClient(_gateway_IP, _gateway_port);
                
                // Create an SSL stream that will close the client's stream.
                SslStream sslStream = new SslStream(
                    socket.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(validateServerCertificate),
                    null
                    );

                if (authenciateServer("abc", sslStream, socket) == false)
                    return false;

                messsage = Encoding.UTF8.GetBytes("GETPROXY\n");
                // Send hello message to the server. 
                sslStream.Write(messsage);
                sslStream.Flush();

                // Read message from the server.
                string serverMessage = readMessage(sslStream);
                _log.debug("_S_entry: " + serverMessage);
                _S_entry = serverMessage;

                socket.Close();
            }

            if (_S_entry == "" || _S_entry == "NO_PROXY")
            {
                _log.debug("_S_entry is still unknown, cannot connect");
                return false;
            }

            // store remote host & port to connect           
            _host = strHost;
            _port = nPort;

            // extract S-entry's host & port
            string IP = "";
            int port = 0;

            // extract S-entry's host & port
            if (ICNetLayer.parseIP_Port(_S_entry, ref IP, ref port) == false)
                return false;

            _log.debug("connecting to _S_entry: " + IP + " port: " + port);

            _socket = new TcpClient(IP, port);

            // set a larger buffer size (to prevent overload)
            //_socket.ReceiveBufferSize = 8192 * 3;
            //_stream = _socket.GetStream();

            // Create an SSL stream that will close the client's stream.
            _stream = new SslStream(
                _socket.GetStream(),
                false,
                new RemoteCertificateValidationCallback(validateServerCertificate),
                null
                );

            if (authenciateServer(strHost, _stream, _socket) == false)
                return false;

            // get local IP & MAC
            string local_IP = ICNetLayer.getIP();
            string local_MAC = ICNetLayer.getMAC();

            // Encode initial query into a byte array.
            // Signal the end of the message using the "<EOF>".
            //messsage = Encoding.UTF8.GetBytes("CONNECT " + _host + ":" + _port + "\n");
            string msg = "CONNECT " + _host + ":" + _port + " " + local_IP + " " + local_MAC + "\n";
            _log.debug("request: " + msg);
            messsage = Encoding.UTF8.GetBytes(msg);
            
            // Send hello message to the server. 
            _stream.Write(messsage);
            _stream.Flush();

            // get the entry server's view on my IP/port
            _detectedIP = readMessage(_stream);

            Console.WriteLine("detectedIP: " + _detectedIP);

            // remove "\n" at end
            if (_detectedIP.Length > 0)
                _detectedIP = _detectedIP.Substring(0, _detectedIP.Length - 1);

            Console.WriteLine("IP local: " + local_IP + " detected: " + _detectedIP + " MAC: " + local_MAC);

            _init = true;
            _available = true;

            bRet = true;
        }
        catch (Exception e)
        {
            bRet = false;
            _log.debug("ICNetSocket.initSocket() : exception -" + e.ToString());
        }

        return bRet;
    }

    public bool writeSocket(string theLine)
    {
        if (checkSocket() == false)
            return false;

        try 
        {
            if (_stream.CanWrite == true)
            {                              
                Byte[] sndBytes = System.Text.Encoding.UTF8.GetBytes(theLine);
                _stream.Write(sndBytes, 0, sndBytes.Length);
            }
            else
            {
                _log.debug("ICNetSocket.writeSocket() : exception - _stream.CanWrite==false");
                _available = false;
                return false;
            }
        }
        catch (Exception e)
        {
            _log.debug("ICNetSocket.writeSocket() : exception -" + e.ToString());
            _available = false;
            return false;
        }

        return true;
    }

    public bool readSocket(out string data)
    {
        data = "";

        if (checkSocket() == false)
            return false;
        
        try
        {
            if (_stream.CanRead == true)
            {
                if (_socket.GetStream().DataAvailable)
                {
                    data = readMessage(_stream);
                    _log.debug("nRet=" + data.Length);
                }
            }
            else
            {
                _log.debug("_stream.CanRead ===false");
            }
        }
        catch(Exception e)
        {
            _log.debug("ICNetSocket.readSocket() : exception -" + e.ToString());
            _available = false;
            return false;
        }

        return true;
    }

    public void closeSocket()
    {
        if (_init)
        {
            // the following would throw exceptions when called at program termination
            //_socket.Client.Shutdown(SocketShutdown.Both);
            //_socket.Client.Disconnect(true);

            _socket.Close();
            _stream.Close();

            _socket = null;
            _stream = null;
            _init = false;
        }

        _available = false;

        // clear out info on _S_entry
        _S_entry = "";

    }

    // check if socket is connected
    public bool isConnected()
    {
        if (_init && _socket != null)
        {
            return _socket.Connected;
        }

        return false;
    }

    // get detected IP, if any
    public string getDetectedIP()
    {
        return _detectedIP;
    }

    //
    // Private Methods
    //

    protected bool authenciateServer(string servername, SslStream sslStream, TcpClient socket)
    {
        /* TODO: enable certificate check */
        // The server name must match the name on the server certificate.
        try
        {
            sslStream.AuthenticateAsClient(servername);
        }
        //catch (AuthenticationException e)
        catch (Exception e)
        {
            _log.debug("Exception: " + e.Message);
            if (e.InnerException != null)
            {
                _log.debug("Inner exception: " + e.InnerException.Message);
            }
            _log.debug("Authentication failed - closing the connection.");
            socket.Close();
            return false;
        }

        return true;
    }

    // check if socket is available
    private bool checkSocket()
    {
        if (_init == false)
            return false;

        // if socket becomes unavailable but was init, try again
        if (!_available)
        {
            closeSocket();
            initSocket(_host, _port);
        }
        // socket is normal, but check for disconnection / termination
        else
        {
            // check disconnection
            if (_socket.Connected == false)
            {
                _available = false;
                return false;
            }

            try
            {
                // process FIN packet
                if (_socket.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] checkConn = new byte[1];
                    if (_socket.Client.Receive(checkConn, SocketFlags.Peek) == 0)
                    {
                        //Debug.LogError("_socket.Client.Receive() ===0");
                        _log.debug("_socket.Client.Receive() ===0");
                        closeSocket();
                        return false;
                    }
                }
            }
            catch (SocketException e)
            {
                _log.debug("ICNetSocket.checkSocket: " + e.ToString());
                _available = false;
                return false;
            }
        }

        return true;
    }

    private string readMessage(SslStream sslStream)
    {
        // Read the  message sent by the server.
        // The end of the message is signaled using the "\n" marker.
        byte[] buffer = new byte[2048];
        Decoder decoder = Encoding.UTF8.GetDecoder();
        StringBuilder messageData = new StringBuilder();
        int bytes = -1;
        do
        {
            bytes = sslStream.Read(buffer, 0, buffer.Length);

            // Use Decoder class to convert from bytes to UTF8
            // in case a character spans two buffers.
            
            char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
            decoder.GetChars(buffer, 0, bytes, chars, 0);
            messageData.Append(chars);

            // Check for EOF.
            if (messageData.ToString().IndexOf("\n") != -1)
            {
                break;
            }

        } while (bytes != 0);

        return messageData.ToString();
    }

}

