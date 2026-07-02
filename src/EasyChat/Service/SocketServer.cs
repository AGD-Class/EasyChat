using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace EasyChat.Service
{
    public class SocketServer
    {
        private static SocketServer? _instance;
        private readonly ConcurrentQueue<ReceiveFileRequest> _pendingReceiveRequests = new();
        private readonly object _receiveLock = new();
        private readonly TcpListener _tcpListener;
        private bool _isReceiving;

        private sealed record ReceiveFileRequest(string SavePath, IProgress<double>? Progress);

        private SocketServer(int port)
        {
            _tcpListener = OkPort(port);
            _tcpListener.Start();
        }

        public static SocketServer GetInstance(int port)
        {
            _instance ??= new SocketServer(port);
            return _instance;
        }

        private TcpListener OkPort(int port)
        {
            try
            {
                return new TcpListener(IPAddress.Any, port);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return OkPort(++port);
            }
        }

        public Task StartReceiveAsync(string savePath, IProgress<double>? progress = null)
        {
            _pendingReceiveRequests.Enqueue(new ReceiveFileRequest(savePath, progress));
            EnsureReceiveLoop();
            return Task.CompletedTask;
        }

        private void EnsureReceiveLoop()
        {
            lock (_receiveLock)
            {
                if (_isReceiving)
                {
                    return;
                }

                _isReceiving = true;
                _ = Task.Run(AcceptLoopAsync);
            }
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (_pendingReceiveRequests.TryDequeue(out var request))
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    await ReceiveFileAsync(client, request.SavePath, request.Progress);
                }
            }
            finally
            {
                lock (_receiveLock)
                {
                    _isReceiving = false;
                    if (!_pendingReceiveRequests.IsEmpty)
                    {
                        EnsureReceiveLoop();
                    }
                }
            }
        }

        private static async Task ReceiveFileAsync(TcpClient client, string savePath, IProgress<double>? progress = null)
        {
            try
            {
                progress?.Report(0);
                await using var networkStream = client.GetStream();
                networkStream.ReadTimeout = 5000;

                var fileNameLengthBuffer = new byte[4];
                await networkStream.ReadExactlyAsync(fileNameLengthBuffer, 0, fileNameLengthBuffer.Length);
                var fileNameLength = BitConverter.ToInt32(fileNameLengthBuffer, 0);

                var fileNameBuffer = new byte[fileNameLength];
                await networkStream.ReadExactlyAsync(fileNameBuffer, 0, fileNameBuffer.Length);

                var fileSizeBuffer = new byte[8];
                await networkStream.ReadExactlyAsync(fileSizeBuffer, 0, fileSizeBuffer.Length);
                var fileSize = BitConverter.ToInt64(fileSizeBuffer, 0);

                await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);
                var buffer = new byte[SocketClient.ChunkSize];
                long totalBytesReceived = 0;
                int bytesRead;
                long receivedChunks = 0;
                var totalChunks = SocketClient.GetTotalChunks(fileSize);

                while (totalBytesReceived < fileSize
                       && (bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesReceived += bytesRead;
                    receivedChunks++;
                    progress?.Report(Math.Clamp(receivedChunks * 100d / totalChunks, 0, 100));
                }

                progress?.Report(100);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"文件接收失败：{ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        public void StopListening()
        {
            _tcpListener.Stop();
        }
    }
}
