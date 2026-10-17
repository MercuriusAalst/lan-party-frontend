using System.Net;
using System.Reflection;
using System.Text;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Pages.Users;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ProfilePictureBehaviorTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MercuriusAPI:BaseAddress"] = "https://api.example.test/v1/",
            ["FileSettings:MaxFileSizeInMB"] = "5"
        })
        .Build();

    [Fact]
    public async Task WithoutAPicture_TheAvatarRendersTheInitialsFallback()
    {
        var html = await RenderAvatarAsync("Morgan Player", null);

        Assert.DoesNotContain("<img", html);
        Assert.Contains(">MP<", html);
        Assert.Contains("role=\"img\"", html);
        Assert.Contains("aria-label=\"Morgan Player\"", html);
    }

    [Fact]
    public async Task WithABackendRelativePicture_TheAvatarRendersTheResolvedImage()
    {
        var html = await RenderAvatarAsync("Morgan Player", "images/users/abc.webp");

        Assert.Contains("<img", html);
        Assert.Contains("src=\"https://api.example.test/images/users/abc.webp\"", html);
        Assert.Contains("user-avatar-image", html);
        Assert.DoesNotContain(">MP<", html);
    }

    [Fact]
    public async Task DecorativeAvatarKeepsItsFallbackVisibleWithoutRepeatingTheAdjacentName()
    {
        var html = await RenderAvatarAsync("Morgan Player", null, decorative: true);

        Assert.Contains(">MP<", html);
        Assert.Contains("aria-hidden=\"true\"", html);
        Assert.DoesNotContain("role=\"img\"", html);
        Assert.DoesNotContain("aria-label=\"Morgan Player\"", html);
    }

    [Fact]
    public void WhenTheImageFails_TheInitialsReplaceTheBrokenImageAndANewReferenceCanLoadAgain()
    {
        var avatar = CreateAvatar("Morgan Player", "images/users/abc.webp");

        Assert.True(ReadAvatar<bool>(avatar, "HasImage"));

        InvokeAvatar(avatar, "HandleImageError");

        Assert.False(ReadAvatar<bool>(avatar, "HasImage"));
        Assert.Equal("MP", ReadAvatar<string>(avatar, "Initials"));

        SetAvatarParameter(avatar, nameof(UserAvatar.PictureUrl), "images/users/replaced.webp");
        InvokeAvatar(avatar, "OnParametersSet");

        Assert.True(ReadAvatar<bool>(avatar, "HasImage"));
        Assert.Contains("replaced.webp", ReadAvatar<string>(avatar, "ResolvedUrl"));
    }

    [Fact]
    public void TheAvatarCropsPicturesIntoFixedCircularSizes()
    {
        var stylesheet = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/UserAvatar.razor.css");

        Assert.Contains("border-radius: 50%", stylesheet);
        Assert.Contains("object-fit: cover", stylesheet);
        Assert.Contains(".user-avatar--compact", stylesheet);
        Assert.Contains(".user-avatar--regular", stylesheet);
        Assert.Contains(".user-avatar--prominent", stylesheet);
        Assert.Contains("border-radius: inherit", stylesheet);
        Assert.DoesNotContain("clamp(", stylesheet);
    }

    [Fact]
    public void ThePlayerIdentitySurfacesConsumeTheReturnedPictureReference()
    {
        var leaderboard = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Leaderboard/TournamentLeaderboardTab.razor");
        var leaderboardAttempts = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Leaderboard/RecordLeaderboardAttemptDialog.razor");
        var participantCard = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/ParticipantCardComponent.razor");
        var participantComponent = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/ParticipantComponent.razor");
        var publicTeamProfile = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Teams/PublicTeamProfile.razor");
        var publicUserProfile = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Users/PublicUserProfile.razor");
        var navMenu = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor");
        var inviteDialog = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Teams/InviteUserDialog.razor");
        var matchDetails = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Matches/DetailView/TournamentMatchDetailsDialog.razor.cs");
        var matchRow = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Matches/BracketView/MatchParticipantRow.razor");
        var registration = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentParticipantsTab.razor");
        var placements = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Tournaments/Tabs/TournamentPlacementsTab.razor");

        Assert.Contains("PictureUrl=\"@row.ProfilePictureUrl\"", leaderboard);
        Assert.Contains("PictureUrl=\"@result.ProfilePictureUrl\"", leaderboardAttempts);
        Assert.Contains("PictureUrl=\"@Participant.User?.ProfilePictureUrl\"", participantCard);
        Assert.Contains("PictureUrl=\"@member.ProfilePictureUrl\"", participantComponent);
        Assert.Contains("PictureUrl=\"@member.ProfilePictureUrl\"", publicTeamProfile);
        Assert.Contains("PictureUrl=\"@_profile.ProfilePictureUrl\"", publicUserProfile);
        Assert.Contains("PictureUrl=\"@_currentProfilePictureUrl\"", navMenu);
        Assert.Contains("<UserAvatar Name=\"@selected.DisplayLabel\" PictureUrl=\"@selected.ProfilePictureUrl\"", inviteDialog);
        Assert.Contains("ProfilePictureUrl = profile.ProfilePictureUrl", matchDetails);
        Assert.Contains("@if (Participant?.User is { } user)", matchRow);
        Assert.Contains("<UserAvatar Name=\"@ParticipantName\" PictureUrl=\"@user.ProfilePictureUrl\" Size=\"compact\" Decorative=\"true\" />", matchRow);
        Assert.Contains("Decorative=\"true\"", matchRow);
        Assert.Contains("Decorative=\"true\"", inviteDialog);
        Assert.Contains("Decorative=\"true\"", registration);
        Assert.Contains("class=\"placement-leaderboard-identity\"", placements);
        Assert.Contains("Decorative=\"true\"", placements);
        Assert.Contains("PictureUrl=\"@member.ProfilePictureUrl\"", registration);
        Assert.Contains("PictureUrl=\"@selectedMember.ProfilePictureUrl\"", registration);
    }

    [Fact]
    public async Task UserSearchServicePreservesThePictureReferenceFromItsApiProjection()
    {
        var lanClient = DispatchProxy.Create<ILANClient, RecordingSearchClientProxy>();
        var proxy = (RecordingSearchClientProxy)(object)lanClient;
        proxy.Response = new UserSearchResponseDTO
        {
            Results =
            [
                new UserSearchResultDTO
                {
                    Id = Guid.NewGuid(),
                    Username = "morgan",
                    DisplayLabel = "Morgan Player",
                    ProfilePictureUrl = "images/users/morgan.webp"
                }
            ]
        };

        var results = await new UserSearchService(lanClient).SearchAsync("morgan");

        Assert.Equal("images/users/morgan.webp", Assert.Single(results).ProfilePictureUrl);
    }

    [Fact]
    public void ManageTeamsMemberCardsCarryTheProjectedPictureIntoTheSharedAvatar()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Teams/ManageTeams.razor");
        var card = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Shared/ParticipantCardComponent.razor");
        var managementMember = new PublicUserDTO
        {
            Id = Guid.NewGuid(),
            Username = "morgan",
            DisplayName = "Morgan Player",
            ProfilePictureUrl = "images/users/morgan.webp"
        };
        var participant = ParticipantViewModel.FromUser(managementMember);

        Assert.Contains("ParticipantCardComponent Participant=\"ParticipantViewModel.FromUser(member)\"", markup);
        Assert.Contains("PictureUrl=\"@Participant.User?.ProfilePictureUrl\"", card);
        Assert.Equal("images/users/morgan.webp", participant.User?.ProfilePictureUrl);
    }

    [Fact]
    public void ProfilePicturePickerUsesTheStyledLabelAndKeepsItsFileInputKeyboardReachable()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor");
        var stylesheet = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Pages/Users/Profile.razor.css");
        var labelStart = markup.IndexOf("<label class=\"profile-picture-picker\"", StringComparison.Ordinal);
        Assert.True(labelStart >= 0);
        var input = markup.IndexOf("<InputFile", labelStart, StringComparison.Ordinal);
        Assert.True(input > labelStart);
        var labelEnd = markup.IndexOf("</label>", input, StringComparison.Ordinal);

        Assert.True(labelEnd > input);
        Assert.Contains("@Localization[\"form.chooseFile\"]", markup);
        Assert.Contains("@(_pendingPicture?.FileName ?? Localization[\"form.noFile\"])", markup);
        Assert.Contains(":focus-within", stylesheet);
        Assert.Contains("opacity: 0", stylesheet);
        Assert.Contains(".profile-picture-actions button:disabled", stylesheet);
        Assert.Contains("opacity: 0.55", stylesheet);
        Assert.Contains(".button-primary:disabled:hover", stylesheet);
        Assert.Contains("disabled=\"@(_pendingPicture is null || IsPictureActionBusy)\"", markup);
        Assert.Contains("disabled=\"@(string.IsNullOrWhiteSpace(_savedPictureUrl) || IsPictureActionBusy)\"", markup);
    }

    [Fact]
    public async Task SelectingAnUnsupportedFileType_IsRejectedLocallyWithoutCallingTheBackend()
    {
        var page = CreateProfile(out var userClient, out _);

        await SelectPictureAsync(page, new TestBrowserFile("notes.txt", "text/plain", [1, 2, 3]));

        Assert.Null(ReadProfileField<object?>(page, "_pendingPicture"));
        Assert.Equal("profile.pictureTypeNotAllowed", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(0, userClient.UploadCalls);
    }

    [Fact]
    public async Task InvalidReplacementClearsThePreviousPendingPreview()
    {
        var page = CreateProfile(out var userClient, out _);
        await SelectPictureAsync(page, AcceptedImage());

        await SelectPictureAsync(page, new TestBrowserFile("notes.txt", "text/plain", [1, 2, 3]));

        Assert.Null(ReadProfileField<object?>(page, "_pendingPicture"));
        Assert.Equal("profile.pictureTypeNotAllowed", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(0, userClient.UploadCalls);
    }

    [Fact]
    public async Task SelectingAnImageBeyondTheMediaLimit_IsRejectedLocallyWithoutCallingTheBackend()
    {
        var page = CreateProfile(out var userClient, out _);
        var oversize = new TestBrowserFile("huge.png", "image/png", [1, 2, 3], reportedSize: 6L * 1024 * 1024);

        await SelectPictureAsync(page, oversize);

        Assert.Null(ReadProfileField<object?>(page, "_pendingPicture"));
        Assert.Equal("shared.fileSizeLimit", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(0, userClient.UploadCalls);
    }

    [Fact]
    public async Task SelectingAnAcceptedImage_ShowsALocalPreviewBeforeUploading()
    {
        var page = CreateProfile(out var userClient, out _);

        await SelectPictureAsync(page, AcceptedImage());

        var pending = ReadProfileField<object>(page, "_pendingPicture");
        var previewDataUrl = (string)pending.GetType().GetProperty("PreviewDataUrl")!.GetValue(pending)!;
        Assert.StartsWith("data:image/png;base64,", previewDataUrl);
        Assert.Equal("profile.pictureSelected", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(0, userClient.UploadCalls);
    }

    [Fact]
    public async Task DisposingTheProfileCancelsAnInProgressPreviewRead()
    {
        var page = CreateProfile(out _, out _);
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selection = SelectPictureAsync(page, new BlockingBrowserFile(opened));

        await opened.Task.WaitAsync(TimeSpan.FromSeconds(2));
        page.Dispose();
        await selection;

        Assert.Null(ReadProfileField<object?>(page, "_pendingPicture"));
    }

    [Fact]
    public async Task DisposingTheProfileCancelsPictureUploadWithoutNotifyingStaleIdentity()
    {
        var page = CreateProfile(out var userClient, out var notifications);
        var pending = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        userClient.PendingUpload = pending;
        SetProfileField(page, "_savedPictureUrl", "images/saved.webp");
        await SelectPictureAsync(page, AcceptedImage());

        var upload = InvokeProfileTaskAsync(page, "UploadPictureAsync");
        page.Dispose();
        await upload;

        Assert.Equal("images/saved.webp", ReadProfileField<string?>(page, "_savedPictureUrl"));
        Assert.Equal(0, notifications.Notifications);
        Assert.Equal("profile.pictureSelected", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.True(userClient.UploadCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task SuccessfulUpload_ShowsTheConfirmedPictureAndRefreshesSharedIdentity()
    {
        var page = CreateProfile(out var userClient, out var notifications);
        userClient.UploadResponse = CreateProfileResponse("images/users/new.png");
        await SelectPictureAsync(page, AcceptedImage());

        await InvokeProfileTaskAsync(page, "UploadPictureAsync");

        Assert.Equal(1, userClient.UploadCalls);
        Assert.Equal("images/users/new.png", ReadProfileField<string?>(page, "_savedPictureUrl"));
        Assert.Null(ReadProfileField<object?>(page, "_pendingPicture"));
        Assert.False(ReadProfileField<bool>(page, "_isPictureBusy"));
        Assert.Equal("profile.pictureUploaded", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(1, notifications.Notifications);
    }

    [Fact]
    public async Task FailedUpload_KeepsThePreviousPictureAndThePendingSelection()
    {
        var page = CreateProfile(out var userClient, out var notifications);
        SetProfileField(page, "_savedPictureUrl", "images/users/saved.png");
        await SelectPictureAsync(page, AcceptedImage());
        userClient.UploadException = await CreateApiException(HttpStatusCode.BadRequest);

        await InvokeProfileTaskAsync(page, "UploadPictureAsync");

        Assert.Equal("images/users/saved.png", ReadProfileField<string?>(page, "_savedPictureUrl"));
        Assert.NotNull(ReadProfileField<object?>(page, "_pendingPicture"));
        Assert.False(ReadProfileField<bool>(page, "_isPictureBusy"));
        Assert.Equal("Profile could not be saved.", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(0, notifications.Notifications);
    }

    [Fact]
    public async Task SuccessfulRemoval_ClearsTheSavedPicture()
    {
        var page = CreateProfile(out var userClient, out var notifications);
        SetProfileField(page, "_savedPictureUrl", "images/users/saved.png");
        userClient.RemoveResponse = CreateProfileResponse(null);

        await InvokeProfileTaskAsync(page, "RemovePictureAsync");

        Assert.Equal(1, userClient.RemoveCalls);
        Assert.Null(ReadProfileField<string?>(page, "_savedPictureUrl"));
        Assert.Equal("profile.pictureRemoved", ReadProfileField<string>(page, "_pictureStatusText"));
        Assert.Equal(1, notifications.Notifications);
    }

    [Fact]
    public async Task WhileUploadIsPending_ASecondPictureActionIsIgnored()
    {
        var page = CreateProfile(out var userClient, out _);
        var pending = new TaskCompletionSource<CurrentUserProfileResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        userClient.PendingUpload = pending;
        await SelectPictureAsync(page, AcceptedImage());

        var upload = InvokeProfileTaskAsync(page, "UploadPictureAsync");
        Assert.True(ReadProfileField<bool>(page, "_isPictureBusy"));

        await InvokeProfileTaskAsync(page, "UploadPictureAsync");
        await InvokeProfileTaskAsync(page, "RemovePictureAsync");

        Assert.Equal(1, userClient.UploadCalls);
        Assert.Equal(0, userClient.RemoveCalls);

        pending.SetResult(CreateProfileResponse("images/users/new.png"));
        await upload;

        Assert.False(ReadProfileField<bool>(page, "_isPictureBusy"));
    }

    [Fact]
    public async Task ProfileSaveAndDeleteAreIgnoredWhilePictureMutationIsPending()
    {
        var page = CreateProfile(out var userClient, out _);
        SetProfileField(page, "_isPictureBusy", true);
        SetProfileField(page, "_originalUsername", "mockuser");
        SetProfileField(page, "_deleteConfirmation", "mockuser");

        await InvokeProfileTaskAsync(page, "SaveAsync");
        await InvokeProfileTaskAsync(page, "DeleteAccountAsync");

        Assert.Equal(0, userClient.UpdateCalls);
        Assert.Equal(0, userClient.DeleteCalls);
    }

    [Fact]
    public async Task PictureUploadIsIgnoredWhileProfileSaveIsPending()
    {
        var page = CreateProfile(out var userClient, out _);
        await SelectPictureAsync(page, AcceptedImage());
        SetProfileField(page, "_isSaving", true);

        await InvokeProfileTaskAsync(page, "UploadPictureAsync");

        Assert.Equal(0, userClient.UploadCalls);
        Assert.NotNull(ReadProfileField<object?>(page, "_pendingPicture"));
    }

    private static async Task<string> RenderAvatarAsync(string? name, string? pictureUrl, bool decorative = false)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Configuration)
            .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<UserAvatar>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(UserAvatar.Name)] = name,
                    [nameof(UserAvatar.PictureUrl)] = pictureUrl,
                    [nameof(UserAvatar.Decorative)] = decorative
                }));
            return component.ToHtmlString();
        });
    }

    private static UserAvatar CreateAvatar(string name, string? pictureUrl)
    {
        var avatar = new UserAvatar();
        SetProperty(avatar, "Configuration", Configuration);
        SetAvatarParameter(avatar, nameof(UserAvatar.Name), name);
        SetAvatarParameter(avatar, nameof(UserAvatar.PictureUrl), pictureUrl);
        InvokeAvatar(avatar, "OnParametersSet");
        return avatar;
    }

    private static void SetAvatarParameter(UserAvatar avatar, string name, object? value) =>
        SetProperty(avatar, name, value);

    private static void InvokeAvatar(UserAvatar avatar, string methodName)
    {
        var method = typeof(UserAvatar).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(UserAvatar).FullName, methodName);
        method.Invoke(avatar, null);
    }

    private static T ReadAvatar<T>(UserAvatar avatar, string name)
    {
        var property = typeof(UserAvatar).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(UserAvatar).FullName, name);
        return (T)property.GetValue(avatar)!;
    }

    private static Profile CreateProfile(out RecordingUserClientProxy userClient, out RecordingNotifier notifier)
    {
        var page = new Profile();
        SetProperty(page, "Localization", TestLocalizationService.Instance);

        var clientProxy = DispatchProxy.Create<IUserClient, RecordingUserClientProxy>();
        userClient = (RecordingUserClientProxy)(object)clientProxy;
        SetProperty(page, "UserClient", clientProxy);

        SetProperty(page, "ToastService", DispatchProxy.Create<IToastService, RecordingToastServiceProxy>());
        SetProperty(page, "NavigationManager", new RecordingNavigationManager());
        SetProperty(page, "Configuration", Configuration);

        notifier = new RecordingNotifier();
        SetProperty(page, "CurrentUserProfileNotifier", notifier);

        var onInitialized = typeof(Profile).GetMethod("OnInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Profile).FullName, "OnInitialized");
        onInitialized.Invoke(page, null);
        return page;
    }

    private static TestBrowserFile AcceptedImage() => new("avatar.png", "image/png", [137, 80, 78, 71]);

    private static Task SelectPictureAsync(Profile page, IBrowserFile file)
    {
        var method = typeof(Profile).GetMethod("SelectPictureAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Profile).FullName, "SelectPictureAsync");
        return (Task)method.Invoke(page, [new InputFileChangeEventArgs([file])])!;
    }

    private static async Task InvokeProfileTaskAsync(Profile page, string methodName)
    {
        var method = typeof(Profile).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Profile).FullName, methodName);
        await (Task)method.Invoke(page, null)!;
    }

    private static CurrentUserProfileResponse CreateProfileResponse(string? pictureUrl) =>
        new(
            true,
            new UserProfileDTO
            {
                Id = Guid.Parse("41111111-1111-1111-1111-111111111120"),
                Username = "mockuser",
                ProfilePictureUrl = pictureUrl
            },
            null,
            true);

    private static async Task<ApiException> CreateApiException(HttpStatusCode statusCode)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://api.example.test/v1/lan/users/me/picture");
        using var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{\"message\":\"Profile could not be saved.\"}", Encoding.UTF8, "application/json")
        };

        return await ApiException.Create(request, HttpMethod.Put, response, new RefitSettings(), innerException: null);
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        property.SetValue(instance, value);
    }

    private static void SetProfileField(Profile page, string name, object? value)
    {
        var field = typeof(Profile).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Profile).FullName, name);
        field.SetValue(page, value);
    }

    private static T ReadProfileField<T>(Profile page, string name)
    {
        var field = typeof(Profile).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Profile).FullName, name);
        return (T)field.GetValue(page)!;
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory != null)
        {
            var repositoryPath = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(repositoryPath))
                return File.ReadAllText(repositoryPath);

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed class TestBrowserFile(string name, string contentType, byte[] content, long? reportedSize = null) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => reportedSize ?? content.Length;
        public string ContentType => contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if(Size > maxAllowedSize)
                throw new IOException("The file exceeds the maximum allowed size.");

            return new MemoryStream(content);
        }
    }

    private sealed class BlockingBrowserFile(TaskCompletionSource opened) : IBrowserFile
    {
        public string Name => "avatar.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 4;
        public string ContentType => "image/png";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new BlockingReadStream(opened);
    }

    private sealed class BlockingReadStream(TaskCompletionSource opened) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 4;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            opened.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    public class RecordingUserClientProxy : DispatchProxy
    {
        public CurrentUserProfileResponse? UploadResponse { get; set; }
        public CurrentUserProfileResponse? RemoveResponse { get; set; }
        public ApiException? UploadException { get; set; }
        public TaskCompletionSource<CurrentUserProfileResponse>? PendingUpload { get; set; }
        public int UploadCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public CancellationToken UploadCancellationToken { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IUserClient.UploadCurrentUserProfilePictureAsync) => Upload((CancellationToken)args![1]!),
                nameof(IUserClient.RemoveCurrentUserProfilePictureAsync) => Remove(),
                nameof(IUserClient.UpdateCurrentUserProfileAsync) => Update(),
                nameof(IUserClient.DeleteCurrentUserAsync) => Delete(),
                _ => throw new NotSupportedException($"Unexpected user client call: {targetMethod?.Name}")
            };
        }

        private object Upload(CancellationToken cancellationToken)
        {
            UploadCalls++;
            UploadCancellationToken = cancellationToken;
            if(UploadException is not null)
                throw UploadException;

            return PendingUpload?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(UploadResponse ?? CreateProfileResponse("images/users/new.png"));
        }

        private object Remove()
        {
            RemoveCalls++;
            return Task.FromResult(RemoveResponse ?? CreateProfileResponse(null));
        }

        private object Update()
        {
            UpdateCalls++;
            return Task.FromResult(new UserProfileDTO { Username = "mockuser" });
        }

        private object Delete()
        {
            DeleteCalls++;
            return Task.FromResult(new UserActionResponse("deleted"));
        }
    }

    private sealed class RecordingNotifier : ICurrentUserProfileNotifier
    {
        public event Func<Task>? Changed;

        public int Notifications { get; private set; }

        public Task NotifyChangedAsync()
        {
            Notifications++;
            return Changed?.Invoke() ?? Task.CompletedTask;
        }
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager()
        {
            Initialize("https://example.test/", "https://example.test/profile");
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    public class RecordingToastServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    public class RecordingSearchClientProxy : DispatchProxy
    {
        public UserSearchResponseDTO Response { get; set; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(ILANClient.SearchUsersAsync)
                ? Task.FromResult(Response)
                : throw new NotSupportedException($"Unexpected LAN client call: {targetMethod?.Name}");
    }
}
