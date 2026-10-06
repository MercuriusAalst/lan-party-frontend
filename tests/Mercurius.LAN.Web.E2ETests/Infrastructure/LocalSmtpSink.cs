using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mercurius.LAN.Web.E2ETests;

internal sealed record CapturedSmtpMessage(
    string EnvelopeSender,
    IReadOnlyList<string> EnvelopeRecipients,
    string Data,
    bool Accepted);

internal sealed class LocalSmtpSink : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _messageLock = new();
    private TaskCompletionSource<CapturedSmtpMessage> _messageReceived = NewMessageSource();
    private TaskCompletionSource? _acceptanceRelease;
    private CapturedSmtpMessage? _message;
    private readonly Task _acceptLoop;
    private int _rejectNextMessage;
    private int _disposed;

    private LocalSmtpSink()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptConnectionsAsync();
    }

    public int Port { get; }

    public bool HasReceivedMessage
    {
        get
        {
            lock (_messageLock)
                return _message is not null;
        }
    }

    public static LocalSmtpSink Start() => new();

    public void Reset()
    {
        TaskCompletionSource? acceptanceRelease;
        lock (_messageLock)
        {
            _message = null;
            _messageReceived = NewMessageSource();
            acceptanceRelease = _acceptanceRelease;
            _acceptanceRelease = null;
        }

        acceptanceRelease?.TrySetResult();
        Interlocked.Exchange(ref _rejectNextMessage, 0);
    }

    public void RejectNextMessage() => Interlocked.Exchange(ref _rejectNextMessage, 1);

    public void HoldNextAcceptance()
    {
        lock (_messageLock)
        {
            if (_acceptanceRelease is not null)
                throw new InvalidOperationException("An SMTP acceptance is already being held.");
            _acceptanceRelease = NewAcceptanceSource();
        }
    }

    public void ReleaseAcceptance()
    {
        TaskCompletionSource? acceptanceRelease;
        lock (_messageLock)
        {
            acceptanceRelease = _acceptanceRelease;
            _acceptanceRelease = null;
        }

        acceptanceRelease?.TrySetResult();
    }

    public Task<CapturedSmtpMessage> WaitForMessageAsync(CancellationToken cancellationToken = default)
    {
        lock (_messageLock)
            return _message is not null
                ? Task.FromResult(_message)
                : _messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    private async Task AcceptConnectionsAsync()
    {
        var cancellationToken = _shutdown.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // ponytail: serial sessions match the non-parallel E2E collection; add concurrent handlers if that changes.
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await HandleConnectionAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        await writer.WriteLineAsync("220 localhost ESMTP ready");
        var envelopeSender = string.Empty;
        var envelopeRecipients = new List<string>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var command = line.Split(' ', 2)[0].ToUpperInvariant();
            switch (command)
            {
                case "EHLO":
                case "HELO":
                    await writer.WriteLineAsync("250-localhost");
                    await writer.WriteLineAsync("250 OK");
                    break;
                case "MAIL":
                    envelopeSender = GetPath(line);
                    envelopeRecipients.Clear();
                    await writer.WriteLineAsync("250 Sender accepted");
                    break;
                case "RCPT":
                    envelopeRecipients.Add(GetPath(line));
                    await writer.WriteLineAsync("250 Recipient accepted");
                    break;
                case "DATA":
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    var data = new StringBuilder();
                    string? dataLine;
                    while ((dataLine = await reader.ReadLineAsync(cancellationToken)) is not null && dataLine != ".")
                    {
                        if (dataLine.StartsWith("..", StringComparison.Ordinal))
                            dataLine = dataLine[1..];
                        data.Append(dataLine).Append("\r\n");
                    }

                    if (dataLine is null)
                        return;

                    var accepted = Interlocked.Exchange(ref _rejectNextMessage, 0) == 0;
                    var acceptanceRelease = accepted ? GetAcceptanceRelease() : null;
                    RecordMessage(new CapturedSmtpMessage(envelopeSender, envelopeRecipients.ToArray(), data.ToString(), accepted));
                    if (acceptanceRelease is not null)
                        await acceptanceRelease.Task.WaitAsync(cancellationToken);
                    await writer.WriteLineAsync(accepted ? "250 Message accepted" : "451 Message rejected by E2E SMTP sink");
                    break;
                case "RSET":
                    envelopeSender = string.Empty;
                    envelopeRecipients.Clear();
                    await writer.WriteLineAsync("250 Reset");
                    break;
                case "NOOP":
                    await writer.WriteLineAsync("250 OK");
                    break;
                case "QUIT":
                    await writer.WriteLineAsync("221 localhost closing connection");
                    return;
                default:
                    await writer.WriteLineAsync("502 Command not implemented");
                    break;
            }
        }
    }

    private static string GetPath(string command)
    {
        var separator = command.IndexOf(':');
        return separator < 0 ? command : command[(separator + 1)..].Trim();
    }

    private void RecordMessage(CapturedSmtpMessage message)
    {
        lock (_messageLock)
        {
            _message = message;
            _messageReceived.TrySetResult(message);
        }
    }

    private TaskCompletionSource? GetAcceptanceRelease()
    {
        lock (_messageLock)
            return _acceptanceRelease;
    }

    private static TaskCompletionSource<CapturedSmtpMessage> NewMessageSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource NewAcceptanceSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        ReleaseAcceptance();
        _shutdown.Cancel();
        _listener.Stop();
        try
        {
            await _acceptLoop;
        }
        finally
        {
            _shutdown.Dispose();
        }
    }
}
