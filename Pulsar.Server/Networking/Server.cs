﻿using Pulsar.Common.Extensions;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Other;
using Pulsar.Server.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace Pulsar.Server.Networking
{
    public class Server
    {
        /// <summary>
        /// Occurs when the state of the server changes.
        /// </summary>
        public event ServerStateEventHandler ServerState;

        /// <summary>
        /// Represents a method that will handle a change in the server's state.
        /// </summary>
        /// <param name="s">The server which changed its state.</param>
        /// <param name="listening">The new listening state of the server.</param>
        /// <param name="port">The port the server is listening on, if listening is True.</param>
        public delegate void ServerStateEventHandler(Server s, bool listening, ushort port);

        /// <summary>
        /// Fires an event that informs subscribers that the server has changed it's state.
        /// </summary>
        /// <param name="listening">The new listening state of the server.</param>
        private void OnServerState(bool listening)
        {
            if (Listening == listening) return;

            Listening = listening;

            var handler = ServerState;
            handler?.Invoke(this, listening, Port);
        }

        /// <summary>
        /// Occurs when the state of a client changes.
        /// </summary>
        public event ClientStateEventHandler ClientState;

        /// <summary>
        /// Represents a method that will handle a change in a client's state.
        /// </summary>
        /// <param name="s">The server, the client is connected to.</param>
        /// <param name="c">The client which changed its state.</param>
        /// <param name="connected">The new connection state of the client.</param>
        public delegate void ClientStateEventHandler(Server s, Client c, bool connected);

        /// <summary>
        /// Fires an event that informs subscribers that a client has changed its state.
        /// </summary>
        /// <param name="c">The client which changed its state.</param>
        /// <param name="connected">The new connection state of the client.</param>
        private void OnClientState(Client c, bool connected)
        {
            if (!connected)
                RemoveClient(c);

            var handler = ClientState;
            handler?.Invoke(this, c, connected);
        }

        /// <summary>
        /// Occurs when a message is received by a client.
        /// </summary>
        public event ClientReadEventHandler ClientRead;

        /// <summary>
        /// Represents a method that will handle a message received from a client.
        /// </summary>
        /// <param name="s">The server, the client is connected to.</param>
        /// <param name="c">The client that has received the message.</param>
        /// <param name="message">The message that received by the client.</param>
        public delegate void ClientReadEventHandler(Server s, Client c, IMessage message);

        /// <summary>
        /// Fires an event that informs subscribers that a message has been
        /// received from the client.
        /// </summary>
        /// <param name="c">The client that has received the message.</param>
        /// <param name="message">The message that received by the client.</param>
        /// <param name="messageLength">The length of the message.</param>
        private void OnClientRead(Client c, IMessage message, int messageLength)
        {
            BytesReceived += messageLength;
            var handler = ClientRead;
            handler?.Invoke(this, c, message);
        }

        /// <summary>
        /// Occurs when a message is sent by a client.
        /// </summary>
        public event ClientWriteEventHandler ClientWrite;

        /// <summary>
        /// Represents the method that will handle the sent message by a client.
        /// </summary>
        /// <param name="s">The server, the client is connected to.</param>
        /// <param name="c">The client that has sent the message.</param>
        /// <param name="message">The message that has been sent by the client.</param>
        public delegate void ClientWriteEventHandler(Server s, Client c, IMessage message);

        /// <summary>
        /// Fires an event that informs subscribers that the client has sent a message.
        /// </summary>
        /// <param name="c">The client that has sent the message.</param>
        /// <param name="message">The message that has been sent by the client.</param>
        /// <param name="messageLength">The length of the message.</param>
        private void OnClientWrite(Client c, IMessage message, int messageLength)
        {
            BytesSent += messageLength;
            var handler = ClientWrite;
            handler?.Invoke(this, c, message);
        }

        /// <summary>
        /// The port on which the server is listening.
        /// </summary>
        public ushort Port { get; private set; }

        /// <summary>
        /// The total amount of received bytes.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// The total amount of sent bytes.
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// The keep-alive time in ms.
        /// </summary>
        private const uint KeepAliveTime = 25000; // 25 s

        /// <summary>
        /// The keep-alive interval in ms.
        /// </summary>
        private const uint KeepAliveInterval = 25000; // 25 s        


        /// <summary>
        /// The listening state of the server. True if listening, else False.
        /// </summary>
        public bool Listening { get; private set; }

        /// <summary>
        /// Gets the clients currently connected to the server.
        /// </summary>
        protected Client[] Clients
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the number of clients currently connected to the server without array allocation.
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.Count;
                }
            }
        }

        /// <summary>
        /// Handle of the Server Socket.
        /// </summary>
        private Socket _handle;

        /// <summary>
        /// The server certificate.
        /// </summary>
        protected readonly X509Certificate2 ServerCertificate;

        /// <summary>
        /// The event to accept new connections asynchronously.
        /// </summary>
        private SocketAsyncEventArgs _item;

        /// <summary>
        /// List of the clients connected to the server.
        /// </summary>
        private readonly List<Client> _clients = new List<Client>();

        /// <summary>
        /// The UPnP service used to discover, create and delete port mappings.
        /// </summary>
        private UPnPService _UPnPService;

        /// <summary>
        /// Lock object for the list of clients.
        /// </summary>
        private readonly object _clientsLock = new object();

        /// <summary>
        /// Determines if the server is currently processing Disconnect method. 
        /// </summary>
        protected bool ProcessingDisconnect { get; set; }

        /// <summary>
        /// Constructor of the server, initializes serializer types.
        /// </summary>
        /// <param name="serverCertificate">The server certificate.</param>
        protected Server(X509Certificate2 serverCertificate)
        {
            ServerCertificate = serverCertificate;
            TypeRegistry.AddTypesToSerializer(typeof(IMessage), TypeRegistry.GetPacketTypes(typeof(IMessage)).ToArray());
        }

        /// <summary>
        /// Updates the status strip icon for the server listening state.
        /// </summary>
        /// <param name="isListening">True if server is listening, false otherwise.</param>
        private void UpdateServerStatusIcon(bool isListening)
        {
            var mainForm = GetMainFormSafe();
            if (mainForm == null) return;

            var iconResource = isListening
                ? Properties.Resources.bullet_green
                : Properties.Resources.bullet_red;

            try
            {
                if (mainForm.InvokeRequired)
                {
                    mainForm.BeginInvoke(new Action(() => SetStatusStripIcon(mainForm, iconResource)));
                }
                else
                {
                    SetStatusStripIcon(mainForm, iconResource);
                }
            }
            catch (Exception)
            {
                // ChatGPT simplified ts
            }
        }

        /// <summary>
        /// Safely gets the main form instance if it exists and is valid.
        /// </summary>
        /// <returns>The main form instance or null if not available.</returns>
        private static FrmMain GetMainFormSafe()
        {
            var mainForm = Application.OpenForms.OfType<FrmMain>().FirstOrDefault();
            return (mainForm != null && !mainForm.IsDisposed && !mainForm.Disposing) ? mainForm : null;
        }

        /// <summary>
        /// Sets the status strip icon if the control is valid.
        /// </summary>
        /// <param name="mainForm">The main form instance.</param>
        /// <param name="icon">The icon to set.</param>
        private static void SetStatusStripIcon(FrmMain mainForm, System.Drawing.Image icon)
        {
            if (mainForm.statusStrip?.IsDisposed == false &&
                mainForm.statusStrip.Items.ContainsKey("listenToolStripStatusLabel"))
            {
                mainForm.statusStrip.Items["listenToolStripStatusLabel"].Image = icon;
            }
        }

        /// <summary>
        /// Begins listening for clients.
        /// </summary>
        /// <param name="port">Port to listen for clients on.</param>
        /// <param name="ipv6">If set to true, use a dual-stack socket to allow IPv4/6 connections. Otherwise use IPv4-only socket.</param>
        /// <param name="enableUPnP">Enables the automatic UPnP port forwarding.</param>
        public void Listen(ushort port, bool ipv6, bool enableUPnP)
        {
            if (Listening) return;
            this.Port = port;

            if (enableUPnP)
            {
                _UPnPService = new UPnPService();
                _UPnPService.CreatePortMapAsync(port);
            }

            if (Socket.OSSupportsIPv6 && ipv6)
            {
                _handle = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                _handle.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                _handle.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
            }
            else
            {
                _handle = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _handle.Bind(new IPEndPoint(IPAddress.Any, port));
            }

            _handle.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _handle.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            _handle.Listen(1000);

            OnServerState(true);

            _item = new SocketAsyncEventArgs();
            _item.Completed += AcceptClient;

            if (!_handle.AcceptAsync(_item))
                AcceptClient(this, _item);

            var mainForm = GetMainFormSafe();
            if (mainForm != null)
            {
                try
                {
                    if (mainForm.InvokeRequired)
                    {
                        mainForm.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                mainForm.EventLog($"Started listening for connections on port: {port}", "info");
                                UpdateServerStatusIcon(true);
                            }
                            catch (Exception)
                            {
                            }
                        }));
                    }
                    else
                    {
                        mainForm.EventLog($"Started listening for connections on port: {port}", "info");
                        UpdateServerStatusIcon(true);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Accepts and begins authenticating an incoming client.
        /// </summary>
        /// <param name="s">The sender.</param>
        /// <param name="e">Asynchronous socket event.</param>
        private void AcceptClient(object s, SocketAsyncEventArgs e)
        {
            try
            {
                do
                {
                    switch (e.SocketError)
                    {
                        case SocketError.Success:
                            SslStream sslStream = null;
                            try
                            {
                                Socket clientSocket = e.AcceptSocket;
                                clientSocket.SetKeepAliveEx(KeepAliveInterval, KeepAliveTime);
                                sslStream = new SslStream(new NetworkStream(clientSocket, true), false);
                                // the SslStream owns the socket and on disposing also disposes the NetworkStream and Socket
                                sslStream.BeginAuthenticateAsServer(ServerCertificate, false, SslProtocols.Tls12, false, EndAuthenticateClient,
                                    new PendingClient { Stream = sslStream, EndPoint = (IPEndPoint)clientSocket.RemoteEndPoint });
                            }
                            catch (Exception)
                            {
                                sslStream?.Close();
                            }
                            break;
                        case SocketError.ConnectionReset:
                            break;
                        default:
                            throw new SocketException((int)e.SocketError);
                    }

                    e.AcceptSocket = null; // enable reuse
                } while (!_handle.AcceptAsync(e));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private class PendingClient
        {
            public SslStream Stream { get; set; }
            public IPEndPoint EndPoint { get; set; }
        }

        /// <summary>
        /// Ends the authentication process of a newly connected client.
        /// </summary>
        /// <param name="ar">The status of the asynchronous operation.</param>
        private void EndAuthenticateClient(IAsyncResult ar)
        {
            var con = (PendingClient)ar.AsyncState;
            try
            {
                con.Stream.EndAuthenticateAsServer(ar);

                Client client = new Client(con.Stream, con.EndPoint);
                AddClient(client);
                OnClientState(client, true);
            }
            catch (Exception)
            {
                con.Stream.Close();
            }
        }

        /// <summary>
        /// Adds a connected client to the list of clients,
        /// subscribes to the client's events.
        /// </summary>
        /// <param name="client">The client to add.</param>
        private void AddClient(Client client)
        {
            lock (_clientsLock)
            {
                client.ClientState += OnClientState;
                client.ClientRead += OnClientRead;

                _clients.Add(client);
            }
        }

        /// <summary>
        /// Removes a disconnected client from the list of clients,
        /// unsubscribes from the client's events.
        /// </summary>
        /// <param name="client">The client to remove.</param>
        private void RemoveClient(Client client)
        {
            if (ProcessingDisconnect) return;

            lock (_clientsLock)
            {
                client.ClientState -= OnClientState;
                client.ClientRead -= OnClientRead;

                _clients.Remove(client);
            }
        }

        /// <summary>
        /// Disconnect the server from all of the clients and discontinue
        /// listening (placing the server in an "off" state).
        /// </summary>
        public void Disconnect()
        {
            if (ProcessingDisconnect) return;
            ProcessingDisconnect = true;

            if (_handle != null)
            {
                _handle.Close();
                _handle = null;
            }

            if (_item != null)
            {
                _item.Dispose();
                _item = null;
            }

            if (_UPnPService != null)
            {
                _UPnPService.DeletePortMapAsync(Port);
                _UPnPService = null;
            }

            lock (_clientsLock)
            {
                var clientsToDisconnect = _clients.ToList();
                _clients.Clear();

                foreach (var client in clientsToDisconnect)
                {
                    try
                    {
                        client.Disconnect();
                        client.ClientState -= OnClientState;
                        client.ClientRead -= OnClientRead;
                    }
                    catch
                    {
                        // Silently continue with other clients
                    }
                }
            }

            ProcessingDisconnect = false;
            OnServerState(false);
            UpdateServerStatusIcon(false);
        }
    }
}

