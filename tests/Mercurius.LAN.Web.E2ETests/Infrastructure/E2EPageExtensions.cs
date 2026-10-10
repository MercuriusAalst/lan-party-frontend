using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace Mercurius.LAN.Web.E2ETests;

public static class E2EPageExtensions
{
    private static readonly ConditionalWeakTable<IPage, InteractivePageState> InteractivePages = new();

    /// <summary>
    /// Substrings of browser console messages that a healthy circuit never emits. The remote
    /// renderer reports a failed component render to the browser console before the circuit
    /// disconnects, so these are the observable client-side fingerprint of a fatal render.
    /// </summary>
    private static readonly string[] FatalConsoleFragments =
    [
        "There is already a subscriber to the content with the given section ID",
        "Unhandled exception rendering component",
        "Unhandled exception in circuit"
    ];

    /// <summary>
    /// Blazor opens the circuit websocket before it applies the interactive render, and a click or
    /// keystroke dispatched in that window is dropped by the browser because no DOM event handler is
    /// bound yet. The prerendered interactive root is delivered as a <c>&lt;!--bl-root--&gt;</c>
    /// placeholder comment and is only replaced once the client applies the first render batch, so
    /// this is the observable "the shipped shell really is interactive now" signal.
    /// </summary>
    private const string InteractiveRootAppliedExpression =
        """
        () => {
            const walker = document.createTreeWalker(document.documentElement, NodeFilter.SHOW_COMMENT);
            for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
                if (node.textContent === "bl-root") {
                    return false;
                }
            }
            return true;
        }
        """;

    private const int InteractiveRootTimeoutMilliseconds = 15000;

    internal static void Observe(IPage page)
    {
        var state = new InteractivePageState();
        InteractivePages.Add(page, state);
        page.Console += (_, message) =>
        {
            if (message.Type != "error")
                return;

            foreach (var fragment in FatalConsoleFragments)
            {
                if (message.Text.Contains(fragment, StringComparison.Ordinal))
                {
                    state.RecordFatalConsoleMessage(message.Text);
                    return;
                }
            }
        };
        page.Request += (_, request) =>
        {
            if (request.IsNavigationRequest &&
                request.ResourceType == "document" &&
                ReferenceEquals(request.Frame, page.MainFrame))
            {
                state.BeginNavigation();
            }
        };
        page.WebSocket += (_, socket) =>
        {
            if (!Uri.TryCreate(socket.Url, UriKind.Absolute, out var address) || !address.AbsolutePath.EndsWith("/_blazor", StringComparison.Ordinal))
                return;
            var connection = state.Current;
            socket.FrameReceived += (_, _) => connection.TrySetResult();
            socket.Close += (_, _) =>
            {
                if (!connection.Task.IsCompleted)
                    connection.TrySetException(new InvalidOperationException("The Blazor circuit closed before becoming interactive."));
            };
        };
        page.Close += (_, _) => state.Close();
    }

    /// <summary>
    /// Returns the fatal circuit/render console messages seen on this page since it was created.
    /// Empty means the browser never observed a broken component render.
    /// </summary>
    public static IReadOnlyList<string> FatalConsoleMessages(this IPage page)
    {
        if (!InteractivePages.TryGetValue(page, out var state))
            throw new InvalidOperationException("Create pages from PlaywrightE2EFixture.NewContextAsync() before reading fatal console messages.");

        return state.FatalConsoleMessages;
    }

    public static async Task WaitForInteractiveAsync(this IPage page, CancellationToken cancellationToken = default)
    {
        if (!InteractivePages.TryGetValue(page, out var state))
            throw new InvalidOperationException("Create pages from PlaywrightE2EFixture.NewContextAsync() before waiting for Blazor interactivity.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        while (true)
        {
            var connection = state.Current;
            try
            {
                await connection.Task.WaitAsync(waitCancellation.Token);
            }
            catch (OperationCanceledException exception) when (state.IsClosed && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("The page closed before the Blazor circuit became interactive.", exception);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !timeout.IsCancellationRequested)
            {
                // A document navigation replaces the circuit generation. Wait for the new
                // document instead of reusing a handshake from the page that just unloaded.
                continue;
            }
            catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("The Blazor circuit did not become interactive within 20 seconds.", exception);
            }

            if (ReferenceEquals(connection, state.Current))
            {
                if (await WaitForInteractiveRootAsync(page, connection, state, cancellationToken))
                    return;

                // A document navigation replaced the circuit while its first render batch was
                // pending, so wait for the replacement document's circuit instead of returning.
                continue;
            }
        }
    }

    private static async Task<bool> WaitForInteractiveRootAsync(
        IPage page,
        TaskCompletionSource connection,
        InteractivePageState state,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        try
        {
            await page.WaitForFunctionAsync(
                InteractiveRootAppliedExpression,
                null,
                new PageWaitForFunctionOptions { Timeout = InteractiveRootTimeoutMilliseconds });
            return true;
        }
        catch (PlaywrightException) when (state.IsClosed && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The page closed before the Blazor circuit became interactive.");
        }
        catch (PlaywrightException) when (!ReferenceEquals(connection, state.Current))
        {
            // A document navigation replaced the circuit while its first render batch was pending.
            return false;
        }
        catch (PlaywrightException exception)
        {
            throw new TimeoutException(
                "The Blazor circuit connected but never applied its interactive render, so page interactions would be dropped.",
                exception);
        }
    }

    /// <summary>
    /// Reports whether Blazor has replaced the prerendered interactive-root placeholder, i.e. whether
    /// the DOM event handlers bound by the circuit are in place. Exposed so the readiness gate itself
    /// is covered by a regression test.
    /// </summary>
    internal static Task<bool> IsInteractiveRootAppliedAsync(this IPage page) =>
        page.EvaluateAsync<bool>(InteractiveRootAppliedExpression);

    public static async Task ClickWhenInteractiveAsync(
        this IPage page,
        ILocator target,
        CancellationToken cancellationToken = default)
    {
        await page.WaitForInteractiveAsync(cancellationToken);
        await target.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
    }

    public static async Task ClickWhenInteractiveAsync(
        this ILocator target,
        CancellationToken cancellationToken = default)
    {
        await target.Page.WaitForInteractiveAsync(cancellationToken);
        await target.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
    }

    /// <summary>
    /// A recovery page that only passes server-rendered assertions can still be a broken
    /// interactive page: a duplicate layout provider fails the Blazor circuit and disconnects the
    /// shell while the SSR DOM still looks correct. Prove the circuit is alive by driving shared
    /// shell controls and confirming the browser saw no fatal render message.
    /// </summary>
    public static async Task AssertShippedShellStaysInteractiveAsync(this IPage page)
    {
        var shell = page.Locator(".layout-shell");
        await Assertions.Expect(shell).ToHaveAttributeAsync("data-theme-state", "light");

        // The theme toggle is a Blazor click handler, so it only responds while the circuit is
        // alive; a duplicate layout provider would have failed the circuit before this point.
        await page.ClickWhenInteractiveAsync(page.Locator(".theme-toggle"));
        await Assertions.Expect(shell).ToHaveAttributeAsync("data-theme-state", "dark");
        await Assertions.Expect(page.Locator(".theme-toggle")).ToHaveAttributeAsync("aria-pressed", "true");

        var fatal = page.FatalConsoleMessages();
        if (fatal.Count > 0)
            throw new Xunit.Sdk.XunitException("The page reported a fatal circuit render message: " + string.Join(" | ", fatal));
    }

    private sealed class InteractivePageState
    {
        private readonly object _sync = new();
        private readonly List<string> _fatalConsoleMessages = [];
        private TaskCompletionSource _current = NewConnectionSource();
        private bool _closed;

        public IReadOnlyList<string> FatalConsoleMessages
        {
            get
            {
                lock (_sync)
                    return _fatalConsoleMessages.ToArray();
            }
        }

        public void RecordFatalConsoleMessage(string message)
        {
            lock (_sync)
            {
                if (!_closed)
                    _fatalConsoleMessages.Add(message);
            }
        }

        public TaskCompletionSource Current
        {
            get
            {
                lock (_sync)
                    return _current;
            }
        }

        public bool IsClosed
        {
            get
            {
                lock (_sync)
                    return _closed;
            }
        }

        public void BeginNavigation()
        {
            TaskCompletionSource previous;
            lock (_sync)
            {
                if (_closed)
                    return;

                previous = _current;
                _current = NewConnectionSource();
            }
            previous.TrySetCanceled();
        }

        public void Close()
        {
            TaskCompletionSource current;
            lock (_sync)
            {
                if (_closed)
                    return;

                _closed = true;
                current = _current;
            }
            current.TrySetCanceled();
        }

        private static TaskCompletionSource NewConnectionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
