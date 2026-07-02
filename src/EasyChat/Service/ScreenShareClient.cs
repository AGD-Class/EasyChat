using System.IO;
using System.Net.Sockets;

namespace EasyChat.Service;

public sealed class ScreenShareClient : IDisposable
{
    private const int MaxFrameBytes = 20 * 1024 * 1024;
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;

    public ScreenShareClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public event Action<byte[]>? FrameReceived;

    public async Task StartAsync(CancellationToken token)
    {
        using var client = new TcpClient(AddressFamily.InterNetwork);
        _client = client;
        await client.ConnectAsync(_host, _port, token);
        client.NoDelay = true;

        using var stream = client.GetStream();
        var lengthBuffer = new byte[4];
        while (!token.IsCancellationRequested)
        {
            await ReadExactAsync(stream, lengthBuffer, token);
            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > MaxFrameBytes)
            {
                throw new InvalidDataException("屏幕共享帧数据无效");
            }

            var frame = new byte[length];
            await ReadExactAsync(stream, frame, token);
            FrameReceived?.Invoke(frame);
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token);
            if (read == 0)
            {
                throw new EndOfStreamException("屏幕共享已断开");
            }

            offset += read;
        }
    }

    public void Dispose()
    {
        _client?.Close();
        _client?.Dispose();
    }
}
