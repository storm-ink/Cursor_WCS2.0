using System.Net.Sockets;
using System.Text;

namespace Wcs.Bs.Plc;

public class PlcClient : IDisposable
{
    private readonly string _ip;
    private readonly int _port;
    private readonly string _deviceCode;
    private readonly ILogger _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private bool _connected;

    public event Action<string, string>? OnMessageReceived;
    public event Action<string, PlcMessage>? OnReportReceived;
    public event Action<string, bool>? OnConnectionChanged;
    public bool IsConnected => _connected;
    public string DeviceCode => _deviceCode;

    public PlcClient(string deviceCode, string ip, int port, ILogger logger)
    {
        _deviceCode = deviceCode;
        _ip = ip;
        _port = port;
        _logger = logger;
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!_connected)
                    {
                        _client = new TcpClient();
                        await _client.ConnectAsync(_ip, _port, _cts.Token);
                        _stream = _client.GetStream();
                        SetConnected(true);
                        _logger.LogInformation("[PLC] Connected to {Device} at {Ip}:{Port}", _deviceCode, _ip, _port);
                    }

                    await ReceiveLoopAsync(_cts.Token);
                }
                catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
                {
                    if (_connected) SetConnected(false);
                    _logger.LogWarning("[PLC] Connection to {Device} lost: {Msg}. Retrying in 5s...", _deviceCode, ex.Message);
                    await Task.Delay(5000, _cts.Token);
                }
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    private void SetConnected(bool value)
    {
        _connected = value;
        OnConnectionChanged?.Invoke(_deviceCode, value);
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[4096];
        var sb = new StringBuilder();

        while (!token.IsCancellationRequested && _stream != null)
        {
            var bytesRead = await _stream.ReadAsync(buffer, token);
            if (bytesRead == 0)
            {
                SetConnected(false);
                break;
            }

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            var data = sb.ToString();

            while (true)
            {
                var start = data.IndexOf('[');
                var end = data.IndexOf(']');
                if (start < 0 || end < 0 || end <= start) break;

                var message = data[start..(end + 1)];
                data = data[(end + 1)..];

                OnMessageReceived?.Invoke(_deviceCode, message);
                OnReportReceived?.Invoke(_deviceCode, PlcMessage.Parse(message));
            }

            sb.Clear();
            sb.Append(data);
        }
    }

    public async Task SendAsync(string message)
    {
        if (_stream == null || !_connected)
        {
            _logger.LogWarning("[PLC] Cannot send to {Device}: not connected", _deviceCode);
            return;
        }

        try
        {
            var bytes = Encoding.ASCII.GetBytes(message);
            await _stream.WriteAsync(bytes);
            OnMessageReceived?.Invoke(_deviceCode, $"[SENT] {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PLC] Failed to send to {Device}", _deviceCode);
            SetConnected(false);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
    }
}
