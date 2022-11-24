using System;
using System.Net.Sockets;
using System.Net;

#pragma warning disable CS0162 //Disable the unreachable code detected warning, because it's not a problem

namespace UpStreamProxy
{
    class FromNet
    {
        /// <summary>
        /// The server socket for the program
        /// </summary>
        private static Socket server;
        /// <summary>
        /// The size of the received buffer
        /// </summary>
        private const int bufferSize = 4096;
        /// <summary>
        /// The IPAddress this server if listening on
        /// </summary>
        private static IPAddress bindAddress = IPAddress.Any;
        /// <summary>
        /// The port, this server is listening on
        /// </summary>
        private const int bindPort = 8080;
        /// <summary>
        /// IP Address or DNS name of the remote proxy server
        /// </summary>
        private const string remoteProxyAddress = "127.0.0.1";
        /// <summary>
        /// Set to true if you specified a DNS name for the remote proxy server
        /// </summary>
        private const bool isDns = false; //Set the true if you defined a DNS name above
        /// <summary>
        /// The port of the remote proxy server
        /// </summary>
        private const int remoteProxyPort = 3000;
        /// <summary>
        /// The parsed IP based on what you specified
        /// </summary>
        private static IPAddress remoteIP = null;

        /// <summary>
        /// Application entry point
        /// </summary>
        /// <param name="args">The command line arguments</param>
        static void Maino(string[] args)
        {
            InitServer(); //Start the local server
            Console.WriteLine("Press enter to stop the server");
            Console.ReadLine(); //Wait for [eneter]
            StopServer(); //Stop the server
        }

        /// <summary>
        /// Start the local proxy server
        /// </summary>
        private static void InitServer()
        {
            if (server != null) return; //If the server is already started, return
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); //Create a new server socket
            IPEndPoint ep = new IPEndPoint(bindAddress, bindPort); //Create a local end point for the server
            server.Bind(ep); //Bind the sokcet to our end point
            server.Listen(5); //Listen for incoming connections (max. 5 pending connections)
            server.BeginAccept(AcceptClients, null); //Accept incoming connections
        }

        /// <summary>
        /// Stop the local server
        /// </summary>
        private static void StopServer()
        {
            if (server == null) return; //If the server isn't started, return
            Console.WriteLine("Stopping Server.");
            //Close and dispose the server socket
            server.Shutdown(SocketShutdown.Both);
            server.Disconnect(false);
            server.Close();
            server.Dispose();
            server = null;
        }

        /// <summary>
        /// Accept incoming connections
        /// </summary>
        /// <param name="ar">Async Result</param>
        private static void AcceptClients(IAsyncResult ar)
        {
            Socket browserSocket; //Define a new socket for the incoming client

            try
            {
                browserSocket = server.EndAccept(ar); //Get the socket of the new client
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to accept client. Reason: " + ex.ToString());
                return;
            }

            Console.WriteLine("Connection Accepted");

            HandleClientBrowser(browserSocket); //Start handling the new client

            server.BeginAccept(new AsyncCallback(AcceptClients), null); //Re-start accepting clients
        }

        /// <summary>
        /// Message object for async calls
        /// </summary>
        private struct MsgObj
        {
            public Socket s; //The socket of the client
            public byte[] buffer; //The receive buffer
            public Socket p; //A socket to the remote proxy
        }

        /// <summary>
        /// Read response from the remote proxy server
        /// </summary>
        /// <param name="ar">Async Result</param>
        private static void ReadProxy(IAsyncResult ar)
        {
            try
            {
                MsgObj clients = (MsgObj)ar.AsyncState; //Get the message object

                int bytesRead = clients.p.EndReceive(ar); //Read response from the remote proxy
                if (bytesRead > 0)
                {
                    clients.s.BeginSend(clients.buffer, 0, bytesRead, SocketFlags.None, new AsyncCallback(DataSend), clients.s); //Send the response back to the client
                }

                clients.p.BeginReceive(clients.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReadProxy), clients); //Re-start reading data from the remote proxy
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error, reading from the remote proxy server, Reason: " + ex.ToString());
            }
        }

        /// <summary>
        /// Read requests from the browsers
        /// </summary>
        /// <param name="ar">Async Result</param>
        private static void ReadBrowser(IAsyncResult ar)
        {
            MsgObj client = new MsgObj(); //Define a message object
            try
            {
                client = (MsgObj)ar.AsyncState; //Get the message object
                int bytesRead = client.s.EndReceive(ar); //Read request from browser
                Console.WriteLine("Got " + bytesRead + " bytes");
                if (bytesRead > 0)
                {
                    Socket toProxySocket; //Define a new socket to the remote proxy server
                    if (client.p == null) //Proxy socket not created
                    {
                        toProxySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); //Create a new socket
                        if (remoteIP == null) GetIpAddress(); //Parse the specified IPAddress if needed
                        IPEndPoint ep = new IPEndPoint(remoteIP, remoteProxyPort); //Create a new end point to the remote proxy
                        toProxySocket.Connect(ep); //Connect to the proxy
                        client.p = toProxySocket; //Set the proxy socket in the message object
                        MsgObj msg = new MsgObj() //Create a new message object
                        {
                            p = client.p,
                            s = client.s,
                            buffer = new byte[bufferSize]
                        };
                        client.p.BeginReceive(msg.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReadProxy), msg); //Read response from proxy server
                    }
                    else toProxySocket = client.p; //PRoxy socket open
                    toProxySocket.Send(client.buffer, 0, bytesRead, SocketFlags.None); //Send the request to the remote proxy
                    client.s.BeginReceive(client.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReadBrowser), client); //Re-start reading from the browser
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading data from browser, Reason: " + ex.ToString());
            }
        }

        /// <summary>
        /// Handle new incoming clients
        /// </summary>
        /// <param name="client">The socket of the new client</param>
        private static void HandleClientBrowser(Socket client)
        {
            try
            {
                MsgObj mo = new MsgObj() //Create a new message object for the client
                {
                    s = client,
                    buffer = new byte[bufferSize]
                };

                client.BeginReceive(mo.buffer, 0, bufferSize, SocketFlags.None, new AsyncCallback(ReadBrowser), mo); //Read requests from the browser
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }

        /// <summary>
        /// Send response to the browser
        /// </summary>
        /// <param name="ar">Async Result</param>
        private static void DataSend(IAsyncResult ar)
        {
            try
            {
                Socket browser = (Socket)ar.AsyncState; //Get the socket of the browser

                browser.EndSend(ar); //Send the data to the browser

                Console.WriteLine("Response sent to browser");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error, when sending data to client, Reason: " + ex.ToString());
            }
        }

        /// <summary>
        /// Parse the string ipaddress into an IPAddress object
        /// </summary>
        private static void GetIpAddress()
        {
            if (isDns) //If a DNS name is defined
            {
                try
                {
                    IPAddress[] ipAddresses = Dns.GetHostAddresses(remoteProxyAddress); //Resolve the DNS into IPAddresses
                    if (ipAddresses.Length < 1)
                    {
                        Console.WriteLine("Error, failed to lookup DNS name " + remoteProxyAddress);
                        Console.WriteLine("Press enter key to exit");
                        Console.ReadLine();
                        Environment.Exit(1);
                    }

                    if (ipAddresses.Length > 1) Console.WriteLine("Warning, DNS lookup resulted in multiple IP adresses! Using the first one.");

                    remoteIP = ipAddresses[0]; //Get the first successful result from the resolve
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error, failed to lookup DNS name Reason: " + ex.ToString());
                    Console.WriteLine("Press enter key to exit");
                    Console.ReadLine();
                    Environment.Exit(1);
                }
            }
            else //Plain ipaddress defined
            {
                try
                {
                    remoteIP = IPAddress.Parse(remoteProxyAddress); //Parse the ipaddress defined
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error, failed to convert string to IP, Reason: " + ex.ToString());
                    Console.WriteLine("Press enter key to exit");
                    Console.ReadLine();
                    Environment.Exit(1);
                }
            }
        }
    }
}