
/*
 *  ICNet (C#)
 * 
 *  A client-side network binding to connect to ImonCloud services
 * 
 *  Usage:
 *  
 *      init(interval)                          initialize network with default logging interval
 *      shutdown()                              close the network layer
 *      tick()                                  perform relevant network processing 
 *      log(msg)                                log a given message to the network log
 *      open(server)                            connect to a particular server type
 *      close(server)                           disconnect from a particular server
 *      sendPacket(server, packet_name, para)   send a packet to a given server, with JSON parameter
 *      setMapping(server_name, ip_port)        store mapping between a given server name & ip_port pair
 *      getDetectedIP()                         get the detected IP of this host (from server or self)
 *      getInstance()                           get a singleton instance of the ICNet object (globally unique, useful for sharing ICNet among multiple objects)
 * 
 *   the user of ICNet needs to provide two event handlers:
 *  
 *   // callback to notify connection status, 
 *   public delegate void ConnectionChangedHandler(string server_name, bool connected);
 *
 *   // callback to notify incoming JSON object 
 *   public delegate void PacketReceivedHandler(ref PacketMessage msg); 
 *   
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;        // for writing log file

using System.Reflection;  // reflection namespace

// for JSON parsing
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace ImonCloud {

    public class PacketMessage
    {
        public string server_name;
        public string packet_name;
        public JToken para;

        public T toObject<T>()
        {
            return JavaScriptConvert.DeserializeObject<T>(para.ToString());
        }
    }

    // states regrading a connection
    class SocketState
    {
        public SocketState(string ip_port)
        {
            IPport = ip_port;
            socket_id = 0;
            connected = false;
        }

        public string IPport;
        public ulong socket_id;
        public bool connected;
    }
    
    // main ICNet
    public class ICNet {
        
        // internally defined name
        public static string lobbyServerName = "lobby";
        public static string managerServerName = "manager";

        // default logfile
        private string logFilePrefix = "";

        // port increment from socket port for manager server
        private int managerServerPortIncrement = 1;
        
        // callback to notify connection status, 
        //      server_name: 'lobby' 'mjzone' 'fruit' ... , 
        //      connected: true/false, 
        public delegate void ConnectionChangedHandler(string server_name, bool connected);

        // callback to notify incoming JSON object 
        public delegate void PacketReceivedHandler(ref PacketMessage msg);

        // Define events
        public event ConnectionChangedHandler EventConnectionChanged;
        public event PacketReceivedHandler EventPacketReceived;

        // constructor
        public ICNet() {
            Console.WriteLine("ICNet (C#) init...");

            // get log instance
            _log = icLog.getInstance();
        }
  
        // destructor
        ~ICNet() {

            Console.WriteLine("ICNet (C#) shutdown...");
            shutdown();
            _log = null;
        }

        public bool initByFile(string ini_file, string log_file)
        {
            logFilePrefix = log_file;

            // gateway & lobby to connect
            string gateway_IPport = "";
            string lobby_IPport = "";

            // load config from ICNet.ini file
            try
            {                
                IniParser parser = new IniParser(ini_file);
                gateway_IPport = parser.GetSetting("IP Config", "ENTRY_SERVER");
                lobby_IPport = parser.GetSetting("IP Config", "LOBBY_SERVER");
            }
            catch (System.IO.FileNotFoundException e)
            {
                string err = "ICNet ini file not found";
                Console.WriteLine(err);
                _log.debug(err + "\n" + e, true);
            }
            catch (Exception e)
            {
                string err = "ICNet.ini parse error, use default";
                Console.WriteLine(err);
                _log.debug(err + "\n" + e, true);
            }

            return init(gateway_IPport, lobby_IPport);
        }

        public bool init(string gateway_IPport, string lobby_IPport)
        {
            bool result = true;
            
            // init only once
            if (_net == null)
            {
                // default there's no log output
                _log.init(logFilePrefix, 0);
                _log.debug("ICNet init", true);

                _secure_conn = (gateway_IPport != null);
                _log.debug("Entry Server: " + gateway_IPport, true);
                _log.debug("secure connection: " + _secure_conn + "\n", true);

                // store connection record on lobby   
                _socket_states[lobbyServerName] = new SocketState(lobby_IPport);

                // store connection record on manager
                string IP = "";
                int port = 0;
                ICNetLayer.parseIP_Port(lobby_IPport, ref IP, ref port);
                port += managerServerPortIncrement;

                string manager_IPport = IP + ":" + port;
                _socket_states[managerServerName] = new SocketState(manager_IPport);

                // create net layer (handling multiple sockets at once)
                _net = new ICNetLayer_SSL_Proxy(_secure_conn);

                // init network
                // NOTE: it's important to initialize network first, because EventConnectionChanged might trigger new 
                result = _net.init(gateway_IPport);

                // make initial connection to lobby
                open(lobbyServerName);
            }
            else
                _log.debug("_net already init...");

            return result;
        }

        // close down the network
        public bool shutdown()
        {
            // notify for disconnection?
            // check for connection status changes (both connect & disconnect)
            foreach (KeyValuePair<string, SocketState> kvp in _socket_states)
            {
                string server_name = kvp.Key;
                SocketState state = kvp.Value;

                // check whether to notify disconnection
                if (state.connected == true)
                {
                    state.connected = false;
                    if (EventConnectionChanged != null)
                        EventConnectionChanged(server_name, false);
                }
            }

            if (_log != null)
            {
                _log.debug("ICNet shutdown");
                _log.close();
            }

            if (_net == null)
                return true;

            bool result = _net.stop();
            _net = null;

            return result;
        }
    
        // perform ticking under the time constrain, 
        // return microseconds remaining, optional 'per_sec' shows whether another second has passed
        public int tick() {
            if (_net == null)
                return 0;

            // TODO: should adopt callbacks for these (instead of active polling)
            // BUG: if '\n' is never received, or connection broke,
            //      buffer message may exist indefinitely
            // check if we've connected to gateway
            if (_net.isInitialized ()) {

                string buf;

                // process incoming socket messages                
                ulong socket_id = 0;
                if ((socket_id = _net.receive (out buf)) != 0) {

                    // parse JSON message
                    _socket_buf = _socket_buf + buf;

                    while (true) {

                        // check if there is something to be parsed (if '\n' is found)
                        int index = _socket_buf.IndexOf('\n');
                        if (index == -1)
                            break;

                        try {
                            // extract parsable message (while keep the rest in buffer)
                            string json_str = _socket_buf.Substring(0, index);

                            // check if there's more to process                            
                            if (json_str.Length < (_socket_buf.Length-1)) {

                                int length = _socket_buf.Length - (index + 1);
                                _socket_buf = _socket_buf.Substring(index + 1, length);
                            }

                            else
                                _socket_buf = "";

                            // record message
                            _log.debug("jsonstr: '" + json_str + "' sockbuf: '" + _socket_buf + "'");

                            processJSON(_id2name[socket_id], json_str.Trim() + '\0');
                        }
                        catch (Exception e) {
                            Console.WriteLine(e);
                            _log.debug(e.ToString());
                        }

                    } // while true
                }   // receive                                      
            }

            _disconnected.Clear();

            // check for connection status changes (both connect & disconnect)
            // TODO: cleaner way instead of polling continously?
            foreach (KeyValuePair<string, SocketState> kvp in _socket_states) {
                string server_name = kvp.Key;
                SocketState state = kvp.Value;

                // check if socket is still alive
                if (_net.isAlive(state.socket_id) == false) {

                    _disconnected.Add(server_name);
                    // check whether to notify disconnection
                    if (state.connected == true) {
                        state.connected = false;
                        if (EventConnectionChanged != null)
                            EventConnectionChanged(server_name, false);
                    }
                }
                else if (state.connected == false) {
                    state.connected = true;
                    if (EventConnectionChanged != null)
                        EventConnectionChanged(server_name, true);
                }
            }

            // remove server info 
            foreach (string server_name in _disconnected) 
                clearServerInfo(server_name);

            _log.flush();

            return 0;
        }

        // store a message to network log
        public void log(string msg)
        {
            _log.debug(msg);
        }

        // send an event to server
        public bool sendPacket(string server_name, string packet_name, ref JObject para) {
            PacketMessage msg = new PacketMessage();
            msg.server_name = server_name;
            msg.packet_name = packet_name;
            msg.para = para;
         
            return sendPacket(ref msg);
        }

        public bool sendPacket(string server_name, string packet_name, string para)
        {
            PacketMessage msg = new PacketMessage();
            msg.server_name = server_name;
            msg.packet_name = packet_name;

            // NOTE: para is string so needs to be a JValue (not JObject)
            msg.para = new JValue(para);

            return sendPacket(ref msg);
        }
       
        public bool sendPacket(ref PacketMessage msg) {

            if (_net == null)
                return false;

            JObject root =
                new JObject(
                    new JProperty(_eventHeader, msg.packet_name),
                    new JProperty(_paraHeader, msg.para)
                );

            return sendJSON(root, msg.server_name);
        }

        // open connection to a particular server
        public bool open(string server_name)
        {
            // if IP/port info does not exist, cannot connect
            if (_socket_states.ContainsKey(server_name) == false)
                return false;

            // check if already connected
            if (_socket_states[server_name].socket_id != 0)
                return true;

            // attempt to connect (or re-connect)
            ulong socket_id = _net.openSocket(_socket_states[server_name].IPport, _secure_conn);

            // if connection fail
            if (socket_id == 0)
            {
                _log.debug("open(): connect attempt to server [" + server_name + "] fail\n");
                return false;
            }

            _socket_states[server_name].socket_id = socket_id;

            // store id to server name mapping
            _id2name[socket_id] = server_name;

            return true;
        }

        // close connection to a particular server
        public bool close(string server_name) {
            
            // check if we indeed is connected to the server
            if (_socket_states.ContainsKey(server_name) == false ||
                _socket_states[server_name].socket_id == 0)
            {
                _log.debug("close: no connection to server [" + server_name + "]");
                return false;
            }

			ulong socket_id = _socket_states[server_name].socket_id;
			
            // disconnect and clear the socket
            _net.closeSocket(socket_id);
            _socket_states[server_name].socket_id = 0;
			
			// clear id to name mapping
			_id2name.Remove(socket_id);			

            return true;
        }

        // store the IP/port pair for a given server name
        public bool setMapping(string server_name, string IP_port) {

            // check if server info exists, if not, create new
            if (_socket_states.ContainsKey(server_name) == false)
                _socket_states[server_name] = new SocketState(IP_port);
            else
                _socket_states[server_name].IPport = IP_port;                    

            return true;
        }

        // get IP address as seen by a remote server
        public string getDetectedIP()
        {
            return _net.getDetectedIP();
        }

        //
        // private methods
        //
		
		private bool clearServerInfo(string server_name) {

            // check if we know about info for a socket
            if (_socket_states.ContainsKey(server_name) == false)
                return false;

			// remove id to server mapping
			_id2name.Remove(_socket_states[server_name].socket_id);
			_socket_states[server_name].socket_id = 0;
            
            // do not erase for lobby & manager info
            if (server_name.Equals(lobbyServerName) || server_name.Equals(managerServerName))
                return true;

            _socket_states.Remove(server_name);
            _log.debug("server [" + server_name + "] info removed...");
			
            return true;
		}

        // helper generic function (with error checking) to send JSON messages to a server
        private bool sendJSON(JObject root, string server_name)
        {
            if (_net == null)
                return false;

            // check if we know about info for a socket
            if (_socket_states.ContainsKey(server_name) == false)
            {
                _log.debug("sendJSON (): no info on server [" + server_name + "], query lobby first... \n");

                // cache packet for now under this server name
                if (_pending.ContainsKey(server_name) == false)
                    _pending[server_name] = new List<JObject>();
                _pending[server_name].Add(root);

                // send request to lobby to query for the server's IP & port
                queryServerInfo(server_name);       

                return true;
            }

            // open new connection (will do nothing if already connected)            
            if (open(server_name) == false)
                return false;

            // for debug purpose, this is indented formatted JSON string (with \r\n at end)
            _log.debug("\nsend to [" + server_name + "]:\n" + root.ToString());

            // Make a new JSON document with no special formatting (not indented)
            // see: http://james.newtonking.com/projects/json/help/html/T_Newtonsoft_Json_Formatting.htm

            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            string outstr = "";
            using (StringWriter sw = new StringWriter())
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, root);
                outstr = sw.ToString();
            }

            // attach \n at end to indicate end of message
            outstr = outstr + '\n';

            bool result = _net.send(_socket_states[server_name].socket_id, ref outstr);

            // check if we've been disconnected
            if (result == false) {
				_log.debug("sendJSON (): socket connection to [" + server_name + "] lost\n");			
				clearServerInfo(server_name);
			}

            return result;
        }
        
        // TODO: clean this up (more general)
        // query IP/port for a given game server from lobby server
        private void queryServerInfo(string server_name) {

            JObject root =
                new JObject(
                    new JProperty(_eventHeader, _typeQueryServer),
                    new JProperty(_paraHeader, new JObject(
                        new JProperty("server", server_name)
                    ))
                );
 
            // deliver to manager to ask
            //sendJSON(root, managerServerName);
			sendJSON(root, lobbyServerName);
        }

        // deliver JSON to pending server
        private int deliverPendingPackets(string server_name) {

            if (_pending.ContainsKey(server_name) == false)
                return 0;

            int count = 0;
            foreach (var packet in _pending[server_name]) {
                if (sendJSON(packet, server_name) == true)
                    count++;
            }
			
			// remove all pending packets
			_pending[server_name].Clear();
			
            return count;
        }

        // process incoming socket messages
        private bool processJSON(string server_name, string json_str)
        {
            //_log.debug("JSON str recv: " + json_str);

            try
            {
                JObject json = JObject.Parse(json_str);

                // pass json object to callback for further processing
                // TODO: check if processing is successful
                PacketMessage msg = new PacketMessage();
                msg.server_name = server_name;
                msg.packet_name = (string)json[_updateHeader];
                msg.para = json[_paraHeader];

                // check for notification of server info
                if (msg.packet_name == _typeQueryServerResponse)
                {
                    string server = (string)msg.para["server"];
                    string IPport = (string)msg.para["IPport"];

                    // check if valid IPport is returned
                    if (IPport == "")
                    {
                        Console.WriteLine("ERROR: no IP/port reported for server: " + server);
                        _log.debug("ERROR: no IP/port reported for server: " + server);
                        if (EventConnectionChanged != null)
                            EventConnectionChanged(server, false);

                        return true;
                    }

                    // store as server info
                    _socket_states[server] = new SocketState(IPport);

                    // deliver pending messages
                    deliverPendingPackets(server);
                }
                else
                    EventPacketReceived(ref msg);
            }            
            catch (ArgumentException e)
            {
                Console.WriteLine(e);
                _log.debug(e.ToString());
                return false;
            }
            catch (JsonSerializationException e)
            {
                Console.WriteLine(e);
                _log.debug(e.ToString());
                return false;
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e);
                _log.debug(e.ToString());
                return false;
            }
            // everything else
            catch (Exception e)
            {
                Console.WriteLine(e);
                _log.debug(e.ToString());
                return false;
            }

            return true;
        }

        //
        // Singleton
        //

        static private ICNet _ICnet = null;

        // get a global singleton instance (note it's not thread-safe)
        static public ICNet getInstance()
        {
            if (_ICnet == null)
            {
                Console.WriteLine("ICNet getInstance(): creating instance");
                _ICnet = new ICNet();
            }

            return _ICnet;
        }

        static public void removeInstance()
        {            
            // delete the instace if available
            if (_ICnet != null)
            {
                Console.WriteLine("ICNet removeInstance(): deleting instance");
                _ICnet.shutdown();
                _ICnet = null;
            }
        }

        //
        // Private Variables
        //

        // actual network layer (implementation)
        private ICNetLayer _net = null;

        // headers used for JSON message
        private string _eventHeader  = "E";        // used to be "ocm"        
        private string _updateHeader = "U";      // used to be "ex" 
        private string _paraHeader   = "P";       // used to be "ref"

        //private string _typeQueryServer = "IC_REQ_QUERY_SERVER";
        private string _typeQueryServer = "IC_SYS_QUERY_SERVER";
		
		private string _typeQueryServerResponse = "ICR_SYS_QUERY_SERVER";

        // buffer for incoming socket messages
        private string _socket_buf = "";

        // IP, port & connection status of servers, mapped from name        
        private Dictionary<string, SocketState> _socket_states = new Dictionary<string, SocketState>();

        // list of disconnected servers
        private List<string> _disconnected = new List<string>();

        // pending messages for sending to server after IP/port is queried        
        private Dictionary<string, List<JObject>> _pending = new Dictionary<string, List<JObject>>();

        // mapping from socket id to server name
        private Dictionary<ulong, string> _id2name = new Dictionary<ulong, string>();

        // secure connection
        private bool _secure_conn = false;     // whether connections are secure using TLS

        // log file
        private icLog _log = null;
    }
}
