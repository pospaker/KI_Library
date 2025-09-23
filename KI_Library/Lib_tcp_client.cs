using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KINT_Lib
{
    public class Lib_TcpClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private string _ipAddress;
        private int _port;
        private volatile bool _isConnected;

        private volatile bool _receiveLoop;
        private Thread _receiveThread;
        private readonly object _lock = new object();

        // ====== Auto Reconnect ======
        private volatile bool _autoReconnect;
        private Task _reconnectTask;
        private CancellationTokenSource _reconnectCts;
        private int _reconnectInitialDelayMs = 1000;   // 시작 딜레이
        private int _reconnectMaxDelayMs = 15000;      // 최대 딜레이
        private Action<string> _log = Console.WriteLine;

        public delegate void DataReceiveClient(byte[] data);
        public event DataReceiveClient OnDataReceived;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<int> OnReconnectAttempt; // 현재 딜레이(ms) 알림

        public bool IsConnected => _isConnected;

        public Lib_TcpClient() { }

        // ====== Public: Auto Reconnect 제어 ======
        public void EnableAutoReconnect(int initialDelayMs = 1000, int maxDelayMs = 15000)
        {
            _autoReconnect = true;
            _reconnectInitialDelayMs = Math.Max(200, initialDelayMs);
            _reconnectMaxDelayMs = Math.Max(_reconnectInitialDelayMs, maxDelayMs);

            if (_reconnectTask == null || _reconnectTask.IsCompleted)
            {
                _reconnectCts?.Cancel();
                _reconnectCts = new CancellationTokenSource();
                _reconnectTask = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token));
            }
        }

        public void DisableAutoReconnect()
        {
            _autoReconnect = false;
            _reconnectCts?.Cancel();
        }

        // ====== Connect / Disconnect ======
        public bool Connect(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            return ConnectInternal();
        }

        private bool ConnectInternal()
        {
            try
            {
                lock (_lock)
                {
                    // 기존 소켓 정리
                    try { _stream?.Close(); } catch { }
                    try { _client?.Close(); } catch { }

                    _client = new TcpClient();
                    _client.NoDelay = true;
                    _client.Connect(_ipAddress, _port);

                    _stream = _client.GetStream();
                    _isConnected = true;
                }

                _log?.Invoke($"[TCP] ✅ Connected: {_ipAddress}:{_port}");
                OnConnected?.Invoke();

                StartReceiving();
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _log?.Invoke($"[TCP] ❌ Connect failed: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            _log?.Invoke("[TCP] Disconnect called");

            lock (_lock)
            {
                _receiveLoop = false;
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }
                _isConnected = false;
            }

            try { _receiveThread?.Join(300); } catch { }

            OnDisconnected?.Invoke();

            // 자동 재접속이 켜져 있으면 루프가 접수해서 재시도함
            EnsureReconnectLoopRunning();
        }

        // ====== Send ======
        public bool Send(byte[] data)
        {
            try
            {
                if (!_isConnected)
                {
                    _log?.Invoke("[TCP] Not connected. Will trigger reconnect.");
                    EnsureReconnectLoopRunning();
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
                _log?.Invoke($"[TCP] Send error: {ex.Message}");
                SafeDropConnection();
                return false;
            }
        }

        public bool Send(string message, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            return Send(encoding.GetBytes(message));
        }

        // ====== Receive ======
        private void StartReceiving()
        {
            _receiveLoop = true;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "TcpClient-ReceiveLoop"
            };
            _receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[4096];

            while (_receiveLoop)
            {
                try
                {
                    NetworkStream s;
                    TcpClient c;
                    lock (_lock)
                    {
                        s = _stream;
                        c = _client;
                    }

                    if (s != null && s.CanRead && c != null && c.Connected)
                    {
                        int bytesRead = s.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            var received = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, received, 0, bytesRead);
                            OnDataReceived?.Invoke(received);
                        }
                        else
                        {
                            _log?.Invoke("[TCP] Server closed connection.");
                            SafeDropConnection(); // 연결 끊김 → 재접속 루프가 처리
                        }
                    }
                    else
                    {
                        Thread.Sleep(20);
                    }
                }
                catch (IOException)
                {
                    _log?.Invoke("[TCP] Receive stream error.");
                    SafeDropConnection();
                }
                catch (ObjectDisposedException)
                {
                    // 종료 과정
                    break;
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"[TCP] Receive error: {ex.Message}");
                    SafeDropConnection();
                }

                Thread.Sleep(5); // CPU 세이브
            }
        }

        // ====== Auto Reconnect Loop ======
        private async Task ReconnectLoopAsync(CancellationToken token)
        {
            int delay = _reconnectInitialDelayMs;

            while (!token.IsCancellationRequested)
            {
                if (!_autoReconnect)
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                    continue;
                }

                // 연결되어 있으면 딜레이 초기화하고 대기
                if (_isConnected)
                {
                    delay = _reconnectInitialDelayMs;
                    await Task.Delay(200, token).ConfigureAwait(false);
                    continue;
                }

                // 연결 안 된 상태 → 재접속 시도
                OnReconnectAttempt?.Invoke(delay);
                _log?.Invoke($"[TCP] 🔁 Reconnect after {delay} ms");

                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { break; }

                if (token.IsCancellationRequested) break;

                bool ok = ConnectInternal();
                if (ok)
                {
                    delay = _reconnectInitialDelayMs; // 성공하면 초기화
                }
                else
                {
                    // 지수 백오프
                    delay = Math.Min(delay * 2, _reconnectMaxDelayMs);
                }
            }
        }

        private void EnsureReconnectLoopRunning()
        {
            if (!_autoReconnect) return;

            if (_reconnectTask == null || _reconnectTask.IsCompleted)
            {
                _reconnectCts?.Cancel();
                _reconnectCts = new CancellationTokenSource();
                _reconnectTask = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token));
            }
        }

        private void SafeDropConnection()
        {
            lock (_lock)
            {
                if (_isConnected == false) return;

                _isConnected = false;
                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }
            }

            OnDisconnected?.Invoke();
            EnsureReconnectLoopRunning();
        }

        // ====== Dispose ======
        public void Dispose()
        {
            _receiveLoop = false;
            DisableAutoReconnect();

            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }

            _isConnected = false;
        }
    }
}