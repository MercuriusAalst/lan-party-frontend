using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Refit;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;

namespace Mercurius.LAN.Web.Components.Pages.Users;

public partial class Profile
{
    private static readonly string[] ProfileFields = [
        nameof(UpdateUserProfileRequest.Username),
        nameof(UpdateUserProfileRequest.Firstname),
        nameof(UpdateUserProfileRequest.Lastname),
        nameof(UpdateUserProfileRequest.DiscordId),
        nameof(UpdateUserProfileRequest.SteamId),
        nameof(UpdateUserProfileRequest.RiotId)];

    private readonly UpdateUserProfileRequest _model = new();
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

    [Inject] private IUserClient UserClient { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

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
        if(_isSaving)
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
            ToastService.ShowError(await GetApiErrorAsync(exception));
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
            ToastService.ShowError(await GetApiErrorAsync(exception));
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
            ToastService.ShowError(await GetApiErrorAsync(exception));
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
        if(!_canDelete || _isDeleting)
            return;

        _isDeleting = true;
        try
        {
            await UserClient.DeleteCurrentUserAsync();
            NavigationManager.NavigateTo("/account/logout", true);
        }
        catch(ApiException exception)
        {
            ToastService.ShowError(await GetApiErrorAsync(exception));
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

    private async Task<string> GetApiErrorAsync(ApiException exception, string? fallback = null)
    {
        fallback ??= Localization["General.Profile.RequestFailed"];
        try
        {
            var content = await exception.GetContentAsAsync<string>();
            return string.IsNullOrWhiteSpace(content) ? fallback : content;
        }
        catch
        {
            return string.IsNullOrWhiteSpace(exception.Content) ? fallback : exception.Content.Trim('"');
        }
    }
}
