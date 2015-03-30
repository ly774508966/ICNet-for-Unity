/*
 *  Definition for Generic Packet Logic (can be extended for specific games)
 *  
 * a packet logic defines the to and from of packets to game-specific data structures
 * (which will be processed by game logic at higher level)
 * 
 * functions supported:
 * 
 *       bool sendPacket(string packet_name, ref JObject para)
 *       bool sendPacket(string packet_name, string para)
 *       bool sendPacket(ref PacketMessage msg)
 *       void close()
 *       void log(string msg) 
 * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;        // for writing log file

using System.Reflection;  // reflection namespace

// for JSON preparation
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;

//using ImonCloud;

namespace ImonCloud
{
    public abstract class ICPacketLogic
    {
        // name of the specific server type (for example, fruit_silver)
        private string _serverName = null;

        // private
        private ICNet _net = null;

        // constructor
        public ICPacketLogic(string server_name)
        {
            _serverName = server_name;
        }

        ~ICPacketLogic()
        {
        }

        // the handler for response messages from Lobby Server
        private void connectionChangedCallback(string server_name, bool connected)
        {
            if (server_name == _serverName)
            {
                if (connected)
                    serverConnectedHandler(_serverName);
                else {
					//onDisconnectedHandler(_serverName);

					// unregister callbacks
					_net.EventConnectionChanged -= new ImonCloud.ICNet.ConnectionChangedHandler(connectionChangedCallback);
					_net.EventPacketReceived -= new ImonCloud.ICNet.PacketReceivedHandler(packetReceivedCallback);
					
                    serverDisconnectedHandler(_serverName);
				}
            } 
        }

        // callback to notify incoming JSON object 
        private void packetReceivedCallback(ref PacketMessage msg)
        {
            // we process only packets for us
            if (msg.server_name == _serverName)
                packetReceivedHandler(ref msg);
        }

        // init logic layer
        public void initByFile(string ini_file, string log_fileprefix)
        {
            // prevent multiple-init
            if (_net != null)
                return;

            _net = ICNet.getInstance();
            _net.initByFile(ini_file, log_fileprefix);

            // register callbacks
            _net.EventConnectionChanged += new ImonCloud.ICNet.ConnectionChangedHandler(connectionChangedCallback);
            _net.EventPacketReceived += new ImonCloud.ICNet.PacketReceivedHandler(packetReceivedCallback);

            // make initial connectiont to server
            _net.open(_serverName);        
        }

        public void init(string gateway_IPport, string lobby_IPport)
        {
            // prevent multiple-init
            if (_net != null)
                return;


            _net = ICNet.getInstance();
            _net.init(gateway_IPport, lobby_IPport);

            // register callbacks
            _net.EventConnectionChanged += new ImonCloud.ICNet.ConnectionChangedHandler(connectionChangedCallback);
            _net.EventPacketReceived += new ImonCloud.ICNet.PacketReceivedHandler(packetReceivedCallback);
        
			// make initial connectiont to server
            _net.open(_serverName);
		}
		
        // shutdown logic layer
        public void shutdown()
        {
            // unregister callbacks
            //_net.EventConnectionChanged -= new ImonCloud.ICNet.ConnectionChangedHandler(connectionChangedCallback);
            //_net.EventPacketReceived -= new ImonCloud.ICNet.PacketReceivedHandler(packetReceivedCallback);
            close();
			ICNet.removeInstance();
            _net = null;
        }

        // perform periodic network processing
        public void tick()
        {
            _net.tick();
        }

        // NOTE: the following can be override by a sub-class, but the sub-class does not need to
        // the handler for response messages from Lobby Server
        protected virtual void serverConnectedHandler(string server_name)
        {
        }
		
		/*
		protected void onDisconnectedHandler(string server_name)
		{
            // unregister callbacks
            _net.EventConnectionChanged -= new ImonCloud.ICNet.ConnectionChangedHandler(connectionChangedCallback);
            _net.EventPacketReceived -= new ImonCloud.ICNet.PacketReceivedHandler(packetReceivedCallback);
			serverDisconnectedHandler(server_name);
		}
		*/
		
        protected virtual void serverDisconnectedHandler(string server_name)
        {
        }

        // handler to notify incoming packets 
        protected virtual void packetReceivedHandler(ref PacketMessage msg)
        {
        }

        // wrappers to protect private variables
        protected bool sendPacket(string packet_name, ref JObject para)
        {
            return _net.sendPacket(_serverName, packet_name, ref para);
        }

        protected bool sendPacket(string packet_name, string para)
        {
            return _net.sendPacket(_serverName, packet_name, para);
        }

        protected bool sendPacket(ref PacketMessage msg)
        {
            return _net.sendPacket(ref msg);
        }

        public void close()
        {
            _net.close(_serverName);
        }

        protected void log(string msg) 
        {
            _net.log(msg);
        }
    }
}

