using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KINT_Lib
{
    public class Lib_TcpClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private string _ipAddress;
        private int _port;
        private bool _isConnected;
        private bool _receiveLoop;
        private Thread _receiveThread;
        private readonly object _lock = new object();

        public delegate void DataReceiveClient(byte[] data);
        public event DataReceiveClient OnDataReceived;

        public bool IsConnected => _isConnected;

        public Lib_TcpClient()
        {
        }

        public bool Connect(string ipAddress, int port)
        {
            try
            {
                _ipAddress = ipAddress;
                _port = port;
                _client = new TcpClient();
                _client.Connect(_ipAddress, _port);
                _stream = _client.GetStream();
                _isConnected = true;
                if (_isConnected) StartReceiving();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] 연결 실패: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }


        public void Disconnect()
        {
            lock (_lock)
            {
                _stream?.Close();
                _client?.Close();
                _isConnected = false;
            }
        }

        public bool Send(byte[] data)
        {
            try
            {
                if (!_isConnected)
                {
                    Console.WriteLine("[TCP] 연결되어 있지 않음. 재연결 시도.");
                    if (!Connect(_ipAddress, _port))
                        return false;
                }

                lock (_lock)
                {
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP] 전송 오류: {ex.Message}");
                _isConnected = false;
                Disconnect();
                return false;
            }
        }

        public bool Send(string message, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
            return Send(encoding.GetBytes(message));
        }


        private void StartReceiving()
        {
            _receiveLoop = true;
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[1024];

            while (_receiveLoop)
            {
                try
                {
                    if (_stream != null && _stream.CanRead && _client.Connected)
                    {
                        int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            byte[] received = new byte[bytesRead];
                            Array.Copy(buffer, 0, received, 0, bytesRead);

                            OnDataReceived?.Invoke(received);
                        }
                        else
                        {
                            // 연결 끊김
                            Console.WriteLine("[TCP] 서버 연결 종료 감지");
                            Disconnect();
                        }
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("[TCP] 수신 중단됨 (스트림 오류)");
                    Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TCP] 수신 오류: {ex.Message}");
                    Disconnect();
                }

                Thread.Sleep(10); // CPU 사용량 방지
            }
        }


        public void Dispose()
        {
            Disconnect();
        }
    }
}