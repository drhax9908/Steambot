using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public delegate void TCPMessageReceived(Socket sender, String args);

public class StateObject
{
    public Socket workSocket = null;
    public const int BufferSize = 1024;
    public byte[] buffer = new byte[BufferSize];
    public StringBuilder sb = new StringBuilder();
}

public class AsynchronousSocketListener
{
    public event TCPMessageReceived MessageReceived;
    public List<Socket> Sockets = new List<Socket>();

    protected virtual void OnMessageReceive(Socket socket, String msg)
    {
        if (MessageReceived != null)
            MessageReceived(socket, msg);
    }

    public AsynchronousSocketListener()
    {
        
    }

    public void StartListening()
    {
        byte[] bytes = new Byte[1024];

        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddress = ipHostInfo.AddressList[ipHostInfo.AddressList.Length-1];
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }

    public void AcceptCallback(IAsyncResult ar)
    {
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }

    public void ReadCallback(IAsyncResult ar)
    {
        String content = String.Empty;

        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;


        if (!Sockets.Contains(handler))
        {
            Console.WriteLine("Got a new server connection ! (" + (IPEndPoint)handler.RemoteEndPoint + ")");
            Sockets.Add(handler);
        }

        int bytesRead = handler.EndReceive(ar);

        if (bytesRead > 0)
        {
            state.sb.Append(Encoding.ASCII.GetString(
                state.buffer, 0, bytesRead));

            content = state.sb.ToString();
            int index = content.IndexOf("\0");
            if (index > -1)
            {
                content = content.Remove(index);
                OnMessageReceive(handler, content);

                SendToAll(handler, content);
            }
            else
            {
                // Not all data received. Get more.
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
        }
    }

    public void Send(Socket handler, String data)
    {
        if (Sockets.Contains(handler))
        {
            if (handler.Connected == false)
            {
                Sockets.Remove(handler);
            }
            else
            {
                try
                {
                    byte[] byteData = Encoding.ASCII.GetBytes(data);
                    handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
                }
                catch(Exception e)
                {
                    Console.WriteLine(">>> a error with sockets happened, but it shouldn't. Please ignore the error message bellow.");
                    Console.WriteLine(e.ToString());
                    Sockets.Remove(handler);
                }
            }
        }
    }

    public void SendToAll(Socket handler, String data)
    {
        for (int i = 0; i < Sockets.Count; i++ )
        {
            if (Sockets[i] != handler)
                Send(Sockets[i], data);
        }
    }

    private void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket handler = (Socket)ar.AsyncState;
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}