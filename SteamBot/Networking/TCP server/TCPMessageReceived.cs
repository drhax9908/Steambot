using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SteamBot.Event
{
    public class TCPMessageReceived : EventArgs
    {
        private readonly string arg;
        private readonly TcpClient socket;

        public TCPMessageReceived(TcpClient socket, string arg)
        {
            this.arg = arg;
            this.socket = socket;
        }

        public string GetMessage
        {
            get { return this.arg; }
        }
        public TcpClient GetSocket
        {
            get { return this.socket; }
        }
    }
}
