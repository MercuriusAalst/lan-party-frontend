using System.Collections;
using System.Reflection;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Pages.Teams;
using Mercurius.LAN.Web.Components.Pages.Users;
using Mercurius.LAN.Web.Components.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ImageSelectionRecoveryTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileSettings:MaxFileSizeInMB"] = "5"
        })
        .Build();

    [Fact]
    public async Task ProfileReadTimeoutClearsPendingPictureAndAllowsRetry()
    {
        var page = CreateProfile();
        await SelectPictureAsync(page, new MemoryBrowserFile("avatar.png", "image/png", [1, 2, 3, 4]));

        await SelectPictureAsync(page, new FailingBrowserFile(new TimeoutException()));

        Assert.Null(ReadField<object?>(page, "_pendingPicture"));
        Assert.Equal("shared.fileReadFailed", ReadField<string>(page, "_pictureStatusText"));
        Assert.False(ReadProperty<bool>(page, "IsPictureActionBusy"));

        await SelectPictureAsync(page, new MemoryBrowserFile("retry.png", "image/png", [5, 6, 7, 8]));

        Assert.NotNull(ReadField<object?>(page, "_pendingPicture"));
    }

    [Fact]
    public async Task CreateTeamReadFailureClearsLogoAndAllowsRetry()
    {
        var dialog = new CreateTeamDialog();
        SetProperty(dialog, "Localization", TestLocalizationService.Instance);
        await SelectLogoAsync(dialog, new MemoryBrowserFile("logo.png", "image/png", [1, 2, 3, 4]));

        await SelectLogoAsync(dialog, new FailingBrowserFile(new JSException("read interrupted")));

        Assert.Null(ReadField<object?>(dialog, "_logo"));
        Assert.Equal("shared.fileReadFailed", ReadField<string>(dialog, "_error"));

        await SelectLogoAsync(dialog, new MemoryBrowserFile("retry.png", "image/png", [5, 6, 7, 8]));

        Assert.NotNull(ReadField<object?>(dialog, "_logo"));
    }

    [Fact]
    public async Task ManageTeamReadFailureClearsStaleLogoAndAllowsRetry()
    {
        var page = new ManageTeams();
        var toast = DispatchProxy.Create<IToastService, RecordingToastServiceProxy>();
        var recorder = (RecordingToastServiceProxy)(object)toast;
        SetProperty(page, "Localization", TestLocalizationService.Instance);
        SetProperty(page, "ToastService", toast);
        var teamId = Guid.NewGuid();
        await SelectLogoAsync(page, teamId, new MemoryBrowserFile("logo.png", "image/png", [1, 2, 3, 4]));

        await SelectLogoAsync(page, teamId, new FailingBrowserFile(new JSException("read interrupted")));

        var selections = (IDictionary)ReadField<object>(page, "_selectedLogos");
        Assert.Empty(selections);
        Assert.Contains("shared.fileReadFailed", recorder.Messages);

        await SelectLogoAsync(page, teamId, new MemoryBrowserFile("retry.png", "image/png", [5, 6, 7, 8]));

        Assert.Single(selections);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("js")]
    public async Task SharedPickerReadFailureShowsRetryFeedbackAndAllowsRetry(string failureKind)
    {
        using var input = new CustomInputFile();
        SetProperty(input, "Localization", TestLocalizationService.Instance);
        SetProperty(input, "Configuration", Configuration);
        var model = new TestFileModel();
        var editContext = new EditContext(model);
        SetProperty(input, "EditContext", editContext);
        SetProperty(input, "ValueExpression", (System.Linq.Expressions.Expression<Func<IBrowserFile>>)(() => model.File!));
        Invoke(input, "OnInitialized");
        Invoke(input, "OnParametersSet");
        SetProperty(input, "FieldIdentifier", new FieldIdentifier(model, nameof(TestFileModel.File)));
        Assert.Equal(nameof(TestFileModel.File), ReadProperty<FieldIdentifier>(input, "FieldIdentifier").FieldName);
        await SelectSharedFileAsync(input, new MemoryBrowserFile("image.png", "image/png", [1, 2, 3, 4]));

        await SelectSharedFileAsync(input, new FailingBrowserFile(CreateReadFailure(failureKind)));

        Assert.Null(ReadProperty<IBrowserFile?>(input, "CurrentValue"));
        Assert.Equal("shared.fileReadFailed", ReadField<string>(input, "_readError"));
        Assert.Contains("shared.fileReadFailed", editContext.GetValidationMessages());

        await SelectSharedFileAsync(input, new MemoryBrowserFile("retry.png", "image/png", [5, 6, 7, 8]));

        Assert.NotNull(ReadProperty<IBrowserFile?>(input, "CurrentValue"));
        Assert.Empty(ReadField<string>(input, "_readError"));
        Assert.Empty(editContext.GetValidationMessages());
    }

    private static Profile CreateProfile()
    {
        var page = new Profile();
        SetProperty(page, "Localization", TestLocalizationService.Instance);
        SetProperty(page, "Configuration", Configuration);
        Invoke(page, "OnInitialized");
        return page;
    }

    private static Task SelectPictureAsync(Profile page, IBrowserFile file)
    {
        var method = typeof(Profile).GetMethod("SelectPictureAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Profile).FullName, "SelectPictureAsync");
        return (Task)method.Invoke(page, [new InputFileChangeEventArgs([file])])!;
    }

    private static Task SelectLogoAsync(CreateTeamDialog dialog, IBrowserFile file)
    {
        var method = typeof(CreateTeamDialog).GetMethod("SelectLogoAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(CreateTeamDialog).FullName, "SelectLogoAsync");
        return (Task)method.Invoke(dialog, [new InputFileChangeEventArgs([file])])!;
    }

    private static Task SelectLogoAsync(ManageTeams page, Guid teamId, IBrowserFile file)
    {
        var method = typeof(ManageTeams).GetMethod("SelectLogoAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(ManageTeams).FullName, "SelectLogoAsync");
        return (Task)method.Invoke(page, [teamId, new InputFileChangeEventArgs([file])])!;
    }

    private static Task SelectSharedFileAsync(CustomInputFile input, IBrowserFile file)
    {
        var method = typeof(CustomInputFile).GetMethod("HandleFileChange", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(CustomInputFile).FullName, "HandleFileChange");
        return (Task)method.Invoke(input, [new InputFileChangeEventArgs([file])])!;
    }

    private static void Invoke(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        method.Invoke(instance, null);
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        property.SetValue(instance, value);
    }

    private static T ReadProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        return (T)property.GetValue(instance)!;
    }

    private static T ReadField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)field.GetValue(instance)!;
    }

    private static Exception CreateReadFailure(string failureKind) =>
        failureKind switch
        {
            "io" => new IOException("read interrupted"),
            "timeout" => new TimeoutException("read interrupted"),
            "js" => new JSException("read interrupted"),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind))
        };

    private sealed class MemoryBrowserFile(string name, string contentType, byte[] content) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => content.Length;
        public string ContentType => contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(content);
    }

    private sealed class FailingBrowserFile(Exception exception) : IBrowserFile
    {
        public string Name => "selected.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 4;
        public string ContentType => "image/png";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new FailingReadStream(exception);
    }

    private sealed class FailingReadStream(Exception exception) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw exception;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(exception);
    }

    private sealed class TestFileModel
    {
        public IBrowserFile? File { get; set; }
    }

    public class RecordingToastServiceProxy : DispatchProxy
    {
        public List<string> Messages { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(args?.FirstOrDefault() is string message)
                Messages.Add(message);
            return null;
        }
    }
}
