
/*
 *  A Generic Network Layer for ICNet
 *  
 * 
 *  Usage:
 * 
 *      init(entry_addr)                initialize the net layer with IP & port of a entry gateway
 *      stop()                          stop the net layer
 *      
 *      openSocket(addr, is_secure)     start connection with a given IP/port, get a socket_id
 *      closeSocket(socket_id)          close a given socket based on socket id
 *      send(socket_id, msg)            send a specific message to a node with socket_id
 *      receive(recv_msg)               receive a message with its socket_id
 *      
 *      isAlive(socket_id)              check if a socket is still connected    
 *      isInitialized()                 if net layer is ready
 *      getDetectedIP()                 obtain what the server sees as my IP/port
 */
 
using System;
using System.IO;
using System.Collections.Generic;

using System.Timers;                    // for doing timeout-based PING to servers

using System.Net;                       // for looking up IP/MAC
//using System.Net.NetworkInformation;    // for finding MAC address

// common interface for network layer
abstract public class ICNetLayer
{
    // init the layer with IP & port of the entry server
    abstract public bool init(string entryIP_port);

    // stop the net layer
    abstract public bool stop(); 
    
    // open & close connections to remote host
    abstract public ulong openSocket(string IP_port, bool is_secure);
    abstract public bool closeSocket(ulong socket_id);

    // send message to a socket & receive messages
    abstract public bool send(ulong socket_id, ref string message);
    abstract public ulong receive(out string message);

    // check if socket is still connected
    abstract public bool isAlive (ulong socket_id);

    // check if network layer is initialized
    abstract public bool isInitialized ();

    // get the IP from remote entry server
    abstract public string getDetectedIP();

    // utility to parse IP & port
    public static bool parseIP_Port(string IP_port, ref string IP, ref int port)
    {
        // extract IP & port from string
        int index = IP_port.IndexOf(':');
        if (index == -1)
            return false;

        try
        {
            IP = IP_port.Substring(0, index);
            port = Convert.ToInt32(IP_port.Substring(index + 1, IP_port.Length - (index + 1)));
        }
        catch (Exception e)
        {
            Console.WriteLine(e + ": cannot extract IP:port info");
            return false;
        }

        return true;
    }

    /*
    // source: http://stackoverflow.com/questions/850650/reliable-method-to-get-machines-mac-address-in-c-sharp
    /// <summary>
    /// returns the mac address of the NIC with max speed.
    /// </summary>
    /// <returns></returns>
    public static string getMAC()
    {
        const int MIN_MAC_ADDR_LENGTH = 12;
        string macAddress = "";
        long maxSpeed = -1;

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            Console.WriteLine("Found MAC Address: " + nic.GetPhysicalAddress().ToString() + " Type: " + nic.NetworkInterfaceType);
            string tempMac = nic.GetPhysicalAddress().ToString();
            if (nic.Speed > maxSpeed && !String.IsNullOrEmpty(tempMac) && tempMac.Length >= MIN_MAC_ADDR_LENGTH)
            {
                Console.WriteLine("New Max Speed = " + nic.Speed + ", MAC: " + tempMac);
                maxSpeed = nic.Speed;
                macAddress = tempMac;
            }
        }
        return macAddress;
    }
    */

    // obtain MAC address
    // source: http://stackoverflow.com/questions/850650/reliable-method-to-get-machines-mac-address-in-c-sharp
    public static string getMAC()
    {
        string macAddresses = "";

        /*
        foreach (System.Net.NetworkInformation.NetworkInterface nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            {
                macAddresses += nic.GetPhysicalAddress().ToString();
                
                // if we get something
                if (macAddresses != "")
                    break;
            }
        }
        */
 
        return macAddresses;
    }

    // get IP address
    public static string getIP()
    {
        string IP = "";

        // obtain IP 
        try
        {
            // ref to: http://stackoverflow.com/questions/1059526/get-ipv4-addresses-from-dns-gethostentry
            IPAddress[] ipv4Addresses = Array.FindAll(
            Dns.GetHostEntry(string.Empty).AddressList,
            a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            /*
            foreach (IPAddress ip in ipv4Addresses)
            {
                Console.WriteLine("    {0}", ip);
            }
            */

            IP = ipv4Addresses[0].ToString();
            //Console.WriteLine("IP obtained: " + IP);
        }
        catch (Exception e)
        {
            string err = "ICNetLayer get IP error";
            Console.WriteLine(err);
            icLog.getInstance().debug(err + "\n" + e, true);
        }

        return IP;
    }
}


public class ICNetLayer_SSL_Proxy : ICNetLayer
{
    private Dictionary<ulong, ICNetSocket> _sockets = new Dictionary<ulong, ICNetSocket>();
    
    // assuming that's enough socket for us
    private ulong _socket_id_counter = 1;

    const double _timeout_interval = 20 * 1000; // milliseconds to send out PING to avoid TCP timeout

    private Timer checkForTime = new Timer(_timeout_interval);

    // record for Gentry
    // TODO: cleaner way?
    private string _gateway_IPport = "";

    // record for detected IP
    private string _detected_IP = "";

    //private bool _is_secured;

    // constructor
    public ICNetLayer_SSL_Proxy(bool is_secure)
    {
        //_is_secured = is_secure;
    }

    // callback to check for time
    void checkTimeElapsed(object sender, ElapsedEventArgs e)
    {
        // check through all sockets and see we need to send a PING to server
        // to keep alive
        // TODO: should only send ping if there's no activity
        foreach (KeyValuePair<ulong, ICNetSocket> kvp in _sockets)
        {
            //ulong socket_id = kvp.Key;
            ICNetSocket socket = kvp.Value;

            // send only to alive sockets
            if (socket.isConnected())
            {
                //Console.WriteLine("send keepalive to " + socket_id);
                socket.writeSocket("\n");
            }
        }
    }

    // initialize network layer with the two servers used
    public override bool init(string entryIP_port)
    {
        _gateway_IPport = entryIP_port;

        // start timer to send periodic ping
        checkForTime.Elapsed += new ElapsedEventHandler(checkTimeElapsed);
        checkForTime.Enabled = true;

        return true;
    }

    // close network layer
    public override bool stop()
    {
        checkForTime.Enabled = false;

        // ShutVAST();
        // close down all connected sockets

        foreach (KeyValuePair<ulong, ICNetSocket> kvp in _sockets)
        {
            //ulong socket_id = kvp.Key;
            ICNetSocket socket = kvp.Value;

            socket.closeSocket();
        }

        _sockets.Clear();

        return true;
    }

    // check if network layer is initialized
    public override bool isInitialized()
    {
        return true;
    }

    // check if a socket is still connected
    public override bool isAlive(ulong socket_id)
    {
        if (_sockets.ContainsKey(socket_id) == false)
            return false;

        return _sockets[socket_id].isConnected();
    }

    // open a new connection
    public override ulong openSocket(string IP_port, bool is_secure)
    {
        string IP = "";
        int port = 0;

        if (parseIP_Port(IP_port, ref IP, ref port) == false)
            return 0;

        Console.WriteLine("IP: " + IP + " port: " + port);

        // new a socket
        ICNetSocket socket = new ICNetSocket(_gateway_IPport);

        // init it
        bool result = socket.initSocket(IP, port);

        // store it
        if (result)
        {
            Console.WriteLine("socket init success for: " + IP_port);
            ulong socket_id = ++_socket_id_counter;
            _sockets.Add(socket_id, socket);

            // store detectedIP, if any
            _detected_IP = socket.getDetectedIP();
            
            return socket_id;
        }
        else
        {
            Console.WriteLine("socket init failed for: " + IP_port);
            return 0;
        }
    }

    // close up an existing socket
    public override bool closeSocket(ulong socket_id)
    {
        if (_sockets.ContainsKey(socket_id) == false)
            return false;

        _sockets[socket_id].closeSocket();
        _sockets.Remove(socket_id);

        return true;
    }

    // send a message to a socket
    public override bool send(ulong socket_id, ref string message)
    {
        // TODO: do a flush to make sure the message is sent without disturbance        
        if (_sockets.ContainsKey(socket_id) == false)
            return false;

        return _sockets[socket_id].writeSocket(message);
    }

    // send a message to a socket
    public override ulong receive(out string message)
    {
        // clear out output
        message = "";

        // loop through all existing sockets to check if there are any messages
        string buf;

        foreach (KeyValuePair<ulong, ICNetSocket> kvp in _sockets)
        {
            ulong socket_id = kvp.Key;
            ICNetSocket socket = kvp.Value;

            //Console.WriteLine("reading socket: " + socket_id);

            // if we got come incoming message, return it
            if (socket.readSocket(out buf) && buf.Length > 0)
            {
                //Console.WriteLine("msg: " + buf);
                // valid incoming string, process it
                message = buf;
                return socket_id;
            }
        }

        return 0;
    }

    // obtain detected IP
    public override string getDetectedIP()
    {
        // default IP is what we detect
        if (_detected_IP == "")
            _detected_IP = ICNetLayer.getIP();

        return _detected_IP;
    }
}

