using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace EasyChat.Service;

public sealed class ScreenShareServer : IDisposable
{
    private const int FrameIntervalMs = 100;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private readonly int _maxFrameWidth;
    private readonly int _maxFrameHeight;
    private readonly List<TcpClient> _clients = [];
    private readonly object _clientsLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TcpListener _listener;
    private bool _isStarted;

    public ScreenShareServer(int preferredPort, int maxFrameWidth, int maxFrameHeight)
    {
        _maxFrameWidth = Math.Max(320, maxFrameWidth);
        _maxFrameHeight = Math.Max(180, maxFrameHeight);
        (_listener, Port) = CreateStartedListener(preferredPort);
    }

    public int Port { get; }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private static (TcpListener Listener, int Port) CreateStartedListener(int preferredPort)
    {
        var port = preferredPort;
        while (true)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                return (listener, port);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                port++;
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                client.NoDelay = true;
                lock (_clientsLock)
                {
                    _clients.Add(client);
                }

                _ = Task.Run(() => StreamClientAsync(client, token), token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task StreamClientAsync(TcpClient client, CancellationToken token)
    {
        try
        {
            using var stream = client.GetStream();
            while (!token.IsCancellationRequested)
            {
                var frame = CaptureFrame();
                await stream.WriteAsync(BitConverter.GetBytes(frame.Length), token);
                await stream.WriteAsync(frame, token);
                await stream.FlushAsync(token);
                await Task.Delay(FrameIntervalMs, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Screen share stream closed: {ex.Message}");
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Screen share socket closed: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Screen share failed: {ex.Message}");
        }
        finally
        {
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }

            client.Close();
            client.Dispose();
        }
    }

    private byte[] CaptureFrame()
    {
        var bounds = GetVirtualScreenBounds();
        using var source = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        using var frame = ScaleFrame(source);
        using var memoryStream = new MemoryStream();
        frame.Save(memoryStream, ImageFormat.Jpeg);
        return memoryStream.ToArray();
    }

    private Bitmap ScaleFrame(Bitmap source)
    {
        var ratio = Math.Min(_maxFrameWidth / (double)source.Width, _maxFrameHeight / (double)source.Height);
        ratio = Math.Min(1, ratio);
        var width = Math.Max(1, (int)(source.Width * ratio));
        var height = Math.Max(1, (int)(source.Height * ratio));

        var target = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(target);
        graphics.InterpolationMode = InterpolationMode.Low;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.SmoothingMode = SmoothingMode.HighSpeed;
        graphics.DrawImage(source, 0, 0, width, height);
        return target;
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("无法获取屏幕尺寸");
        }

        return new Rectangle(
            GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            width,
            height);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();

        lock (_clientsLock)
        {
            foreach (var client in _clients.ToList())
            {
                client.Close();
                client.Dispose();
            }

            _clients.Clear();
        }

        _cts.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
