using System.Reflection;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

public sealed class E2EPageExtensionsTests
{
    [Fact]
    public async Task SocketFromUncommittedDocumentCannotFailCommittedGeneration()
    {
        var page = Fake<IPage>.Create();
        var frame = Fake<IFrame>.Create();
        var request = Fake<IRequest>.Create();
        page.Set("MainFrame", frame.Instance);
        request.Set("IsNavigationRequest", true);
        request.Set("ResourceType", "document");
        request.Set("Frame", frame.Instance);
        request.Set("Url", "http://example.test/");
        frame.Set("Url", "http://example.test/");
        E2EPageExtensions.Observe(page.Instance);

        page.Raise("Request", request.Instance);
        var oldSocket = Fake<IWebSocket>.Create();
        oldSocket.Set("Url", "ws://example.test/_blazor?id=old");
        page.Raise("WebSocket", oldSocket.Instance);
        page.Raise("FrameNavigated", frame.Instance);
        oldSocket.Raise("Close", oldSocket.Instance);

        var newSocket = Fake<IWebSocket>.Create();
        newSocket.Set("Url", "ws://example.test/_blazor?id=new");
        page.Raise("WebSocket", newSocket.Instance);
        newSocket.Raise("FrameReceived", Fake<IWebSocketFrame>.Create().Instance);

        await page.Instance.WaitForInteractiveAsync();
    }

    [Fact]
    public async Task CurrentSocketCloseStillFailsBeforeInteractivity()
    {
        var page = Fake<IPage>.Create();
        E2EPageExtensions.Observe(page.Instance);
        var socket = Fake<IWebSocket>.Create();
        socket.Set("Url", "ws://example.test/_blazor?id=current");
        page.Raise("WebSocket", socket.Instance);

        var wait = page.Instance.WaitForInteractiveAsync();
        socket.Raise("Close", socket.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => wait);
        Assert.Contains("circuit closed before becoming interactive", error.Message, StringComparison.Ordinal);
    }

    private sealed class Fake<T> where T : class
    {
        private readonly Proxy _proxy;

        private Fake(T instance, Proxy proxy)
        {
            Instance = instance;
            _proxy = proxy;
        }

        public T Instance { get; }

        public static Fake<T> Create()
        {
            var instance = DispatchProxy.Create<T, Proxy>();
            return new Fake<T>(instance, (Proxy)(object)instance);
        }

        public void Set(string property, object? value) => _proxy.Properties[property] = value;

        public void Raise(string eventName, object? eventArgs) => _proxy.Raise(eventName, Instance, eventArgs);

        public class Proxy : DispatchProxy
        {
            private readonly Dictionary<string, Delegate> _handlers = [];

            public Dictionary<string, object?> Properties { get; } = [];

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                var name = targetMethod?.Name ?? throw new InvalidOperationException("Missing proxy method.");
                if (name.StartsWith("add_", StringComparison.Ordinal))
                {
                    var eventName = name[4..];
                    _handlers.TryGetValue(eventName, out var current);
                    _handlers[eventName] = Delegate.Combine(current, (Delegate)args![0]!);
                    return null;
                }
                if (name.StartsWith("remove_", StringComparison.Ordinal))
                {
                    var eventName = name[7..];
                    if (_handlers.TryGetValue(eventName, out var current))
                    {
                        var updated = Delegate.Remove(current, (Delegate)args![0]!);
                        if (updated is null)
                            _handlers.Remove(eventName);
                        else
                            _handlers[eventName] = updated;
                    }
                    return null;
                }
                return name.StartsWith("get_", StringComparison.Ordinal)
                    ? Properties.GetValueOrDefault(name[4..])
                    : null;
            }

            public void Raise(string eventName, object? sender, object? eventArgs) =>
                _handlers.GetValueOrDefault(eventName)?.DynamicInvoke(sender, eventArgs);
        }
    }
}
