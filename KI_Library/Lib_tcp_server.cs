using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KINT_Lib
{
    public class Lib_Tcp_Server
    {
        private TcpListener _listener;
        private bool _isRunning;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _lock = new object();

        public bool IsRunning => _isRunning;

        public delegate void DataReceiveServer(TcpClient client, byte[] data);
        public event DataReceiveServer OnDataReceived;

        public void Start(string ipAddress, int port)
        {
            if (!IPAddress.TryParse(ipAddress, out IPAddress ip))
                throw new ArgumentException("Invalid IP address");

            _listener = new TcpListener(ip, port);
            _listener.Start();
            _isRunning = true;

            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        private void AcceptClients()
        {
            while (_isRunning)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    lock (_lock)
                    {
                        _clients.Add(client);
                    }

                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch
                {
                    break;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[1024];

                while (_isRunning && client.Connected)
                {
                    int byteCount = stream.Read(buffer, 0, buffer.Length);
                    if (byteCount == 0) break;
        
                    byte[] received = new byte[byteCount];
                    Array.Copy(buffer, received, byteCount);
                    
                    OnDataReceived?.Invoke(client, received);
                }
            }
            catch { }
            lock (_lock)
            {
                _clients.Remove(client);
            }
            try { client.Close(); } catch { }
        }

        public void Stop()
        {
            _isRunning = false;
            try { _listener.Stop(); } catch { }

            lock (_lock)
            {
                foreach (var client in _clients)
                {
                    try { client.Close(); } catch { }
                }
                _clients.Clear();
            }
        }

        public void Broadcast(string message, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;

            Broadcast(encoding.GetBytes(message));
        }

        public void Broadcast(byte[] data)
        {
            List<TcpClient> clientsCopy;
            lock (_lock)
            {
                clientsCopy = new List<TcpClient>(_clients);
            }

            foreach (var client in clientsCopy)
            {
                if (client == null || !client.Connected) continue;

                try
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
                catch
                {
                    // 예외 무시 또는 로깅
                }
            }
        }
    }
}