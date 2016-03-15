using SteamBot.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public delegate void TCPMessageReceived(TcpClient sender, String args);

namespace SteamBot.Networking.TCP_server
{
    public class Client
    {
        private string clNo;
        public TcpClient clientSocket { get; private set; }
        public bool running { get; set; }
        public string Name { get; set; }

        public event TCPMessageReceived MessageReceived;
        protected virtual void OnMessageReceive(TcpClient socket, String msg)
        {
            if (MessageReceived != null)
                MessageReceived(socket, msg);
        }

        public Client(string name = "UNDEFINED -> Use REGISTERME")
        {
            running = true;
            Name = name;
        }

        public void startClient(TcpClient inClientSocket, string clineNo)
        {
            this.clientSocket = inClientSocket;
            this.clNo = clineNo;
            Thread ctThread = new Thread(HookMessage);
            ctThread.Start();
        }

        private void HookMessage()
        {
            byte[] bytesFrom = new byte[10025];
            string dataFromClient = null;

            while (running)
            {
                try
                {
                    if (clientSocket.Connected)
                    {
                        NetworkStream networkStream = clientSocket.GetStream();
                        networkStream.Read(bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
                        dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                        dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("\0"));
                        OnMessageReceive(clientSocket, dataFromClient);
                    }
                    else
                    {
                        running = false;
                        Console.WriteLine("Error client disconnected, removing...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while processing messages ! Please, give those next lines to Arkarr (AM) !");
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
