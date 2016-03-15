using SteamBot.Networking.TCP_server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SteamBot.Networking
{
    public class TCPServer
    {
        private TcpListener serverSocket = new TcpListener(11000);
        private TcpClient clientSocket = default(TcpClient);

        public bool HookForNewClients { get; set; }
        public List<Client> Clients = new List<Client>();
        public Bot myBot;

        public TCPServer(Bot bot)
        {
            HookForNewClients = true;
            myBot = bot;
        }

        public void StartServer()
        {
            int counter = 0;

            serverSocket.Start();
            Console.WriteLine(" >> " + "Server Started");

            counter = 0;
            while (HookForNewClients)
            {
                counter += 1;
                clientSocket = serverSocket.AcceptTcpClient();
                Console.WriteLine("Got a new server connection ! (" + (IPEndPoint)clientSocket.Client.RemoteEndPoint + ")");
                Client client = new Client();
                client.startClient(clientSocket, Convert.ToString(counter));

                client.MessageReceived += new TCPMessageReceived(myBot.OnTCPMessageReceived);
                Clients.Add(client);
            }

            clientSocket.Close();
            serverSocket.Stop();
        }

        public void Send(TcpClient socket, string msg)
        {
            Client client = Clients.Find(x => x.clientSocket == socket);
            if (client != null)
            {
                if (client.clientSocket.Client.Connected)
                    client.clientSocket.Client.Send(Encoding.ASCII.GetBytes(msg));
                else
                    Clients.Remove(client);
            }
            else
            {
                Console.WriteLine("Unable to send data to client, client socket not in connected clients list.");
            }
        }

        public void SendToAll(string msg)
        {
            foreach(Client client in Clients)
                Send(client.clientSocket, msg);
        }
    }
}
