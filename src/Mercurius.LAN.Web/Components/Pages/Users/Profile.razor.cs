using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Refit;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;

namespace Mercurius.LAN.Web.Components.Pages.Users;

public partial class Profile : IDisposable
{
    private const string ProfilePictureAccept = "image/png,image/jpeg,image/webp,image/gif";
    private static readonly string[] ProfilePictureContentTypes =
        ["image/png", "image/jpeg", "image/webp", "image/gif"];

    private static readonly string[] ProfileFields = [
        nameof(UpdateUserProfileRequest.Username),
        nameof(UpdateUserProfileRequest.Firstname),
        nameof(UpdateUserProfileRequest.Lastname),
        nameof(UpdateUserProfileRequest.DiscordId),
        nameof(UpdateUserProfileRequest.SteamId),
        nameof(UpdateUserProfileRequest.RiotId)];

    private readonly UpdateUserProfileRequest _model = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private EditContext? _editContext;
    private ValidationMessageStore? _validationMessageStore;
    private string _emailDisplay = string.Empty;
    private bool _emailVerified;
    private string _emailStatusText = string.Empty;
    private string _emailStatusClass = "form-text";
    private string? _usernameAvailabilityMessage;
    private string _usernameAvailabilityClass = "form-text";
    private string _originalUsername = string.Empty;
    private string _deleteConfirmation = string.Empty;
    private bool _isSaving;
    private bool _isSendingVerification;
    private bool _isSendingPasswordReset;
    private bool _isDeleting;
    private bool _canDelete =>
        !string.IsNullOrWhiteSpace(_originalUsername) &&
        string.Equals(_deleteConfirmation?.Trim(), _originalUsername, StringComparison.OrdinalIgnoreCase);
    private string? _loadError;
    private string? _savedPictureUrl;
    private ProfilePictureSelection? _pendingPicture;
    private bool _isPictureBusy;
    private bool _isPictureSelecting;
    private bool _disposed;
    private string _pictureStatusText = string.Empty;
    private long _maxPictureBytes;
    private bool IsProfileMutationBusy => _isSaving || _isDeleting || _isPictureBusy;
    private bool IsPictureActionBusy => IsProfileMutationBusy || _isPictureSelecting;

    [Inject] private IUserClient UserClient { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private ICurrentUserProfileNotifier CurrentUserProfileNotifier { get; set; } = null!;

    protected override void OnInitialized()
    {
        _maxPictureBytes = Configuration.GetValue<int>("FileSettings:MaxFileSizeInMB") * 1024L * 1024L;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(!firstRender)
            return;

        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        _loadError = null;
        try
        {
            var currentProfile = await UserClient.GetCurrentUserProfileAsync();
            if(!currentProfile.IsComplete)
            {
                NavigationManager.NavigateTo("/complete-profile?returnUrl=/profile");
                return;
            }

            ApplyProfile(currentProfile);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            NavigationManager.NavigateTo("/account/login?returnUrl=/profile", true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Gone)
        {
            NavigationManager.NavigateTo("/account/logout", true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            NavigationManager.NavigateTo("/complete-profile?returnUrl=/profile");
        }
        catch(ApiException)
        {
            // The load failure is shown on the status page, so it must not echo the raw API response.
            _loadError = Localization["General.Profile.LoadError"];
        }
        catch(UnauthorizedAccessException)
        {
            NavigationManager.NavigateTo("/account/login?returnUrl=/profile", true);
        }
        catch(HttpRequestException)
        {
            _loadError = Localization["General.Profile.LoadError"];
        }
        catch(TaskCanceledException)
        {
            _loadError = Localization["General.Profile.LoadError"];
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SaveAsync()
    {
        if(IsProfileMutationBusy || _isPictureSelecting)
            return;

        _isSaving = true;
        try
        {
            if(!string.Equals(_model.Username.Trim(), _originalUsername, StringComparison.OrdinalIgnoreCase))
            {
                var availability = await UserClient.CheckUsernameAvailabilityAsync(_model.Username);
                if(!availability.IsAvailable)
                {
                    _usernameAvailabilityMessage = availability.Reason ?? Localization["General.Profile.UsernameUnavailable"];
                    _usernameAvailabilityClass = "form-text text-danger";
                    return;
                }
            }

            var profile = await UserClient.UpdateCurrentUserProfileAsync(_model);
            _originalUsername = (profile.Username ?? string.Empty).Trim();
            ToastService.ShowSuccess(Localization["General.Profile.Saved"]);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.BadRequest || exception.StatusCode == HttpStatusCode.Conflict)
        {
            ToastService.ShowError(GetApiError(exception));
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            NavigationManager.NavigateTo("/account/login?returnUrl=/profile", true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Gone)
        {
            NavigationManager.NavigateTo("/account/logout", true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            NavigationManager.NavigateTo("/complete-profile?returnUrl=/profile");
        }
        catch(HttpRequestException)
        {
            ToastService.ShowError(Localization["General.Profile.RequestFailed"]);
        }
        catch(TaskCanceledException)
        {
            ToastService.ShowError(Localization["General.Profile.RequestFailed"]);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SelectPictureAsync(InputFileChangeEventArgs args)
    {
        if(IsPictureActionBusy)
            return;

        _isPictureSelecting = true;
        _pendingPicture = null;
        var file = args.File;
        if(!ProfilePictureContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            _pictureStatusText = Localization["profile.pictureTypeNotAllowed"];
            _isPictureSelecting = false;
            return;
        }

        if(file.Size > _maxPictureBytes)
        {
            _pictureStatusText = Localization.Get("shared.fileSizeLimit", _maxPictureBytes / (1024 * 1024));
            _isPictureSelecting = false;
            return;
        }

        try
        {
            var content = new byte[file.Size];
            await using var stream = file.OpenReadStream(_maxPictureBytes, _lifetimeCancellation.Token);
            await stream.ReadExactlyAsync(content, _lifetimeCancellation.Token);

            _pendingPicture = new ProfilePictureSelection(file.Name, file.ContentType, content);
            _pictureStatusText = Localization.Get("profile.pictureSelected", file.Name);
        }
        catch(IOException)
        {
            _pendingPicture = null;
            _pictureStatusText = Localization.Get("shared.fileSizeLimit", _maxPictureBytes / (1024 * 1024));
        }
        catch(OperationCanceledException) when(_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _isPictureSelecting = false;
        }
    }

    private void ClearPendingPicture()
    {
        if(_isPictureBusy)
            return;

        _pendingPicture = null;
        _pictureStatusText = string.Empty;
    }

    private Task UploadPictureAsync()
    {
        if(_pendingPicture is not { } picture)
            return Task.CompletedTask;

        return RunPictureActionAsync(async () =>
        {
            await using var stream = new MemoryStream(picture.Content);
            var response = await UserClient.UploadCurrentUserProfilePictureAsync(
                new StreamPart(stream, picture.FileName, picture.ContentType),
                _lifetimeCancellation.Token);

            if(_disposed)
                return;
            _pendingPicture = null;
            _savedPictureUrl = response.User?.ProfilePictureUrl;
        }, Localization["profile.pictureUploaded"]);
    }

    private Task RemovePictureAsync() => RunPictureActionAsync(async () =>
    {
        var response = await UserClient.RemoveCurrentUserProfilePictureAsync(_lifetimeCancellation.Token);

        if(_disposed)
            return;
        _pendingPicture = null;
        _savedPictureUrl = response.User?.ProfilePictureUrl;
    }, Localization["profile.pictureRemoved"]);

    private async Task RunPictureActionAsync(Func<Task> pictureAction, string successMessage)
    {
        if(IsProfileMutationBusy || _isPictureSelecting)
            return;

        _isPictureBusy = true;
        try
        {
            await pictureAction();
            if(_disposed)
                return;
            _pictureStatusText = successMessage;
            ToastService.ShowSuccess(successMessage);
            try
            {
                await CurrentUserProfileNotifier.NotifyChangedAsync();
            }
            catch(Exception) when(_disposed)
            {
            }
        }
        catch(ApiException) when(_disposed)
        {
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.BadRequest)
        {
            FailPicture(GetApiError(exception, Localization["profile.pictureFailed"]));
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            NavigationManager.NavigateTo("/account/login?returnUrl=/profile", true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Gone)
        {
            NavigationManager.NavigateTo("/account/logout", true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
        {
            NavigationManager.NavigateTo("/complete-profile?returnUrl=/profile");
        }
        catch(ApiException exception)
        {
            FailPicture(GetApiError(exception, Localization["profile.pictureFailed"]));
        }
        catch(Exception exception) when(
            (exception is HttpRequestException or TaskCanceledException) &&
            !_lifetimeCancellation.IsCancellationRequested)
        {
            FailPicture(Localization["profile.pictureFailed"]);
        }
        catch(OperationCanceledException) when(_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _isPictureBusy = false;
        }
    }

    private void FailPicture(string message)
    {
        _pendingPicture = null;
        _pictureStatusText = message;
        ToastService.ShowError(message);
    }

    private async Task CheckUsernameAvailabilityAsync()
    {
        _usernameAvailabilityMessage = null;

        if(string.IsNullOrWhiteSpace(_model.Username) ||
           !System.Text.RegularExpressions.Regex.IsMatch(_model.Username.Trim(), "^[a-zA-Z0-9]{3,32}$") ||
           string.Equals(_model.Username.Trim(), _originalUsername, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var availability = await UserClient.CheckUsernameAvailabilityAsync(_model.Username);
            _usernameAvailabilityMessage = availability.IsAvailable
                ? Localization["General.Profile.UsernameAvailable"]
                : availability.Reason ?? Localization["General.Profile.UsernameUnavailable"];
            _usernameAvailabilityClass = availability.IsAvailable ? "form-text text-success" : "form-text text-danger";
        }
        catch(Exception exception) when(exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            _usernameAvailabilityMessage = null;
        }
    }

    private async Task ResendVerificationEmailAsync()
    {
        _isSendingVerification = true;
        try
        {
            var result = await UserClient.ResendVerificationEmailAsync();
            ToastService.ShowInfo(result.Message);
        }
        catch(ApiException exception)
        {
            ToastService.ShowError(GetApiError(exception));
        }
        catch(Exception exception) when(exception is HttpRequestException or TaskCanceledException)
        {
            ToastService.ShowError(Localization["General.Profile.RequestFailed"]);
        }
        finally
        {
            _isSendingVerification = false;
        }
    }

    private async Task SendPasswordResetEmailAsync()
    {
        _isSendingPasswordReset = true;
        try
        {
            var result = await UserClient.SendPasswordResetEmailAsync();
            ToastService.ShowInfo(result.Message);
        }
        catch(ApiException exception)
        {
            ToastService.ShowError(GetApiError(exception));
        }
        catch(Exception exception) when(exception is HttpRequestException or TaskCanceledException)
        {
            ToastService.ShowError(Localization["General.Profile.RequestFailed"]);
        }
        finally
        {
            _isSendingPasswordReset = false;
        }
    }

    private async Task DeleteAccountAsync()
    {
        if(!_canDelete || IsProfileMutationBusy || _isPictureSelecting)
            return;

        _isDeleting = true;
        try
        {
            var response = await UserClient.DeleteCurrentUserAsync();
            var logoutUrl = response.DeletionPending
                ? $"/account/logout?returnUrl={Uri.EscapeDataString("/?accountDeletion=pending")}"
                : "/account/logout";
            NavigationManager.NavigateTo(logoutUrl, true);
        }
        catch(ApiException exception)
        {
            ToastService.ShowError(GetApiError(exception));
        }
        catch(Exception exception) when(exception is HttpRequestException or TaskCanceledException)
        {
            ToastService.ShowError(Localization["General.Profile.RequestFailed"]);
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private void ApplyProfile(CurrentUserProfileResponse response)
    {
        var profile = response.User;
        _model.Username = profile?.Username ?? string.Empty;
        _model.Firstname = profile?.Firstname ?? string.Empty;
        _model.Lastname = profile?.Lastname ?? string.Empty;
        _model.DiscordId = profile?.DiscordId;
        _model.SteamId = profile?.SteamId;
        _model.RiotId = profile?.RiotId;
        _originalUsername = _model.Username.Trim();
        _savedPictureUrl = profile?.ProfilePictureUrl;
        _pendingPicture = null;
        _pictureStatusText = string.Empty;

        _emailDisplay = response.Email ?? profile?.Email ?? string.Empty;
        _emailVerified = response.EmailVerified || profile?.EmailVerified == true;
        _emailStatusText = _emailVerified ? Localization["General.Profile.Verified"] : Localization["General.Profile.Unverified"];
        _emailStatusClass = _emailVerified ? "form-text text-success" : "form-text text-warning";

        _editContext = new EditContext(_model);
        _validationMessageStore = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += HandleFieldChanged;
        _editContext.SetFieldCssClassProvider(new BootstrapValidationFieldClassProvider());
    }

    private async Task HandleSubmitAsync(EditContext editContext)
    {
        if(!ValidateProfile()) return;
        await SaveAsync();
    }

    private bool ValidateProfile()
    {
        if(_editContext is null || _validationMessageStore is null) return false;
        _validationMessageStore.Clear();
        var isValid = true;

        foreach(var propertyName in ProfileFields)
        {
            var property = typeof(UpdateUserProfileRequest).GetProperty(propertyName);
            var field = new FieldIdentifier(_model, propertyName);
            if(property is null) continue;

            _validationMessageStore.Clear(field);
            var value = property.GetValue(_model);
            var context = new ValidationContext(_model) { MemberName = propertyName };
            var results = new List<ValidationResult>();
            if(Validator.TryValidateProperty(value, context, results)) continue;

            isValid = false;
            foreach(var result in results)
                _validationMessageStore.Add(field, GetLocalizedValidationMessage(property, value, context, result));
        }

        _editContext.NotifyValidationStateChanged();
        return isValid;
    }

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        if(_validationMessageStore is null || _editContext is null) return;

        var propertyName = args.FieldIdentifier.FieldName;
        var property = typeof(UpdateUserProfileRequest).GetProperty(propertyName);
        if(property is null) return;

        _validationMessageStore.Clear(args.FieldIdentifier);
        var value = property.GetValue(_model);
        var context = new ValidationContext(_model) { MemberName = propertyName };
        var results = new List<ValidationResult>();
        if(!Validator.TryValidateProperty(value, context, results))
        {
            foreach(var result in results)
                _validationMessageStore.Add(args.FieldIdentifier, GetLocalizedValidationMessage(property, value, context, result));
        }

        _editContext.NotifyValidationStateChanged();
    }

    private string GetLocalizedValidationMessage(PropertyInfo property, object? value, ValidationContext context, ValidationResult result)
    {
        var failedAttribute = property.GetCustomAttributes<ValidationAttribute>()
            .FirstOrDefault(attribute => string.Equals(
                attribute.GetValidationResult(value, context)?.ErrorMessage,
                result.ErrorMessage,
                StringComparison.Ordinal));

        return failedAttribute switch
        {
            RequiredAttribute => Localization.Get("form.requiredField", GetFieldLabel(property.Name)),
            RegularExpressionAttribute => Localization["form.usernamePattern"],
            StringLengthAttribute stringLength => Localization.Get("form.maxLengthField", GetFieldLabel(property.Name), stringLength.MaximumLength),
            _ => Localization["form.invalid"]
        };
    }

    private string GetFieldLabel(string propertyName) => propertyName switch
    {
        nameof(UpdateUserProfileRequest.Username) => Localization["profile.username"],
        nameof(UpdateUserProfileRequest.Firstname) => Localization["profile.firstName"],
        nameof(UpdateUserProfileRequest.Lastname) => Localization["profile.lastName"],
        nameof(UpdateUserProfileRequest.DiscordId) => Localization["shared.discord"],
        nameof(UpdateUserProfileRequest.SteamId) => Localization["shared.steam"],
        nameof(UpdateUserProfileRequest.RiotId) => Localization["shared.riot"],
        _ => propertyName
    };

    private string GetApiError(ApiException exception, string? fallback = null)
    {
        fallback ??= Localization["General.Profile.RequestFailed"];
        var message = exception.GetApiError()?.Message;
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    private sealed record ProfilePictureSelection(string FileName, string ContentType, byte[] Content)
    {
        public string PreviewDataUrl => $"data:{ContentType};base64,{Convert.ToBase64String(Content)}";
    }

    public void Dispose()
    {
        _disposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }
}
