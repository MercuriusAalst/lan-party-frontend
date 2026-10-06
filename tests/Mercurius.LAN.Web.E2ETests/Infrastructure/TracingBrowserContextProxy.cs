using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace Mercurius.LAN.Web.E2ETests.Infrastructure;

public class TracingBrowserContextProxy : DispatchProxy
{
    private IBrowserContext _context = null!;
    private Func<Task> _close = null!;

    public static IBrowserContext Create(IBrowserContext context, Func<Task> close)
    {
        var proxy = DispatchProxy.Create<IBrowserContext, TracingBrowserContextProxy>();
        var handler = (TracingBrowserContextProxy)(object)proxy;
        handler._context = context;
        handler._close = close;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            throw new InvalidOperationException("Playwright called a browser context without a method.");

        if (targetMethod.Name == nameof(IAsyncDisposable.DisposeAsync))
            return new ValueTask(_close());

        if (targetMethod.Name == nameof(IBrowserContext.CloseAsync))
            return _close();

        try
        {
            return targetMethod.Invoke(_context, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
