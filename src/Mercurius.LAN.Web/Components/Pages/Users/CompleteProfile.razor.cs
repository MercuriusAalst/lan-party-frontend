using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Refit;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Security.Claims;

namespace Mercurius.LAN.Web.Components.Pages.Users;

public partial class CompleteProfile
{
    private static readonly string[] AccountFields = [nameof(CompleteUserProfileRequest.Username)];
    private static readonly string[] AboutFields = [nameof(CompleteUserProfileRequest.Firstname), nameof(CompleteUserProfileRequest.Lastname)];
    private static readonly string[][] StepFields = [AccountFields, AboutFields, []];
    private string[] StepTitles => [
        Localization["General.CompleteProfile.StepAccount"],
        Localization["General.CompleteProfile.StepAbout"],
        Localization["General.CompleteProfile.StepGaming"]];
    private string[] StepDescriptions => [
        Localization["General.CompleteProfile.StepAccountDescription"],
        Localization["General.CompleteProfile.StepAboutDescription"],
        Localization["General.CompleteProfile.StepGamingDescription"]];
    private const int LastStepIndex = 2;

    private readonly CompleteUserProfileRequest _model = new();
    private EditContext? _editContext;
    private ValidationMessageStore? _validationMessageStore;
    private string? _email;
    private string? _usernameAvailabilityMessage;
    private string _usernameAvailabilityClass = "form-text";
    private bool? _usernameIsAvailable;
    private bool _isSaving;
    private bool _isCompleted;
    private int _activeStep;
    private string? _loadError;

    [Inject] private IUserClient UserClient { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Parameter, SupplyParameterFromQuery] public string? ReturnUrl { get; set; }
    [Parameter, SupplyParameterFromQuery] public bool Registration { get; set; }

    private bool IsRegistrationFlow => Registration;

    protected override Task OnInitializedAsync() => LoadProfileAsync();

    private async Task LoadProfileAsync()
    {
        _loadError = null;
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        PrefillFromClaims(authenticationState.User);

        try
        {
            var currentProfile = await UserClient.GetCurrentUserProfileAsync();
            _email = currentProfile.Email ?? currentProfile.User?.Email ?? _email;
            if(currentProfile.IsComplete)
            {
                NavigationManager.NavigateTo(LocalReturnUrlHelper.GetSafeLocalReturnUrl(ReturnUrl), true);
                return;
            }
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            NavigationManager.NavigateTo(GetLoginHref(), true);
            return;
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Gone) { NavigationManager.NavigateTo("/account/logout", true); return; }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound) { }
        catch(ApiException) { SetLoadError(); return; }
        catch(HttpRequestException) { SetLoadError(); return; }
        catch(TaskCanceledException) { SetLoadError(); return; }
        catch(UnauthorizedAccessException)
        {
            NavigationManager.NavigateTo(GetLoginHref(), true);
            return;
        }

        _editContext = new EditContext(_model);
        _validationMessageStore = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += HandleFieldChanged;
        _editContext.SetFieldCssClassProvider(new BootstrapValidationFieldClassProvider());
    }

    private async Task HandleSubmitAsync(EditContext editContext)
    {
        if(_activeStep < LastStepIndex) { await NextStepAsync(); return; }
        await SaveAsync();
    }

    private async Task NextStepAsync()
    {
        if(_isSaving || _activeStep >= LastStepIndex || !ValidateCurrentStep()) return;
        if(_activeStep == 0)
        {
            await CheckUsernameAvailabilityAsync();
            if(_usernameIsAvailable == false) return;
        }
        _activeStep++;
    }

    private void PreviousStep() { if(!_isSaving && _activeStep > 0) _activeStep--; }
    private void CancelOnboarding()
    {
        if(_isSaving) return;
        if(IsRegistrationFlow)
        {
            NavigationManager.NavigateTo("/account/logout", true);
            return;
        }

        NavigationManager.NavigateTo(LocalReturnUrlHelper.GetSafeLocalReturnUrl(ReturnUrl));
    }

    private string GetRetryHref()
    {
        var returnUrl = LocalReturnUrlHelper.GetSafeLocalReturnUrl(ReturnUrl);
        var returnUrlQuery = $"returnUrl={Uri.EscapeDataString(returnUrl)}";
        return IsRegistrationFlow
            ? $"/complete-profile?registration=true&{returnUrlQuery}"
            : $"/complete-profile?{returnUrlQuery}";
    }

    private string GetRecoveryHref() => IsRegistrationFlow ? "/account/logout" : "/";

    private string GetLoginHref()
    {
        var loginReturnUrl = IsRegistrationFlow ? GetRetryHref() : LocalReturnUrlHelper.GetSafeLocalReturnUrl(ReturnUrl);
        return $"/account/login?returnUrl={Uri.EscapeDataString(loginReturnUrl)}";
    }

    private void SetLoadError() => _loadError = Localization["General.CompleteProfile.LoadError"];
    private void ContinueAfterCompletion() => NavigationManager.NavigateTo(LocalReturnUrlHelper.GetSafeLocalReturnUrl(ReturnUrl), true);

    private bool ValidateCurrentStep()
    {
        return ValidateFields(StepFields[_activeStep]);
    }

    private bool ValidateAll()
    {
        return ValidateFields(StepFields.SelectMany(fields => fields));
    }

    private bool ValidateFields(IEnumerable<string> propertyNames, bool clearAll = true)
    {
        if(_editContext is null || _validationMessageStore is null) return false;
        if(clearAll) _validationMessageStore.Clear();
        var isValid = true;
        foreach(var propertyName in propertyNames)
        {
            var property = typeof(CompleteUserProfileRequest).GetProperty(propertyName);
            var field = new FieldIdentifier(_model, propertyName);
            if(property is null) continue;
            _validationMessageStore.Clear(field);
            var results = new List<ValidationResult>();
            var context = new ValidationContext(_model) { MemberName = propertyName };
            if(Validator.TryValidateProperty(property.GetValue(_model), context, results)) continue;
            isValid = false;
            foreach(var result in results)
                _validationMessageStore.Add(field, GetLocalizedValidationMessage(property, property.GetValue(_model), context, result));
        }
        _editContext.NotifyValidationStateChanged();
        return isValid;
    }

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        if(_validationMessageStore is null || _editContext is null) return;

        ValidateFields([args.FieldIdentifier.FieldName], clearAll: false);
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
        nameof(CompleteUserProfileRequest.Username) => Localization["profile.username"],
        nameof(CompleteUserProfileRequest.Firstname) => Localization["profile.firstName"],
        nameof(CompleteUserProfileRequest.Lastname) => Localization["profile.lastName"],
        nameof(CompleteUserProfileRequest.DiscordId) => Localization["shared.discord"],
        nameof(CompleteUserProfileRequest.SteamId) => Localization["shared.steam"],
        nameof(CompleteUserProfileRequest.RiotId) => Localization["shared.riot"],
        _ => propertyName
    };

    private async Task SaveAsync()
    {
        if(_isSaving || _editContext is null) return;
        if(!ValidateAll()) { _activeStep = GetFirstInvalidStep(); return; }
        _isSaving = true;
        try
        {
            await CheckUsernameAvailabilityAsync();
            if(_usernameIsAvailable == false) { _activeStep = 0; return; }
            await UserClient.CompleteCurrentUserProfileAsync(_model);
            ToastService.ShowSuccess(IsRegistrationFlow ? Localization["General.CompleteProfile.AccountCreated"] : Localization["General.CompleteProfile.ProfileCompleted"]);
            if(IsRegistrationFlow) _isCompleted = true;
            else NavigationManager.NavigateTo(LocalReturnUrlHelper.GetSafeLocalReturnUrl(ReturnUrl), true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.BadRequest || exception.StatusCode == HttpStatusCode.Conflict) { ToastService.ShowError(GetApiError(exception)); }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.NotFound) { ToastService.ShowError(Localization["General.CompleteProfile.ProfileNotCreated"]); }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            NavigationManager.NavigateTo(GetLoginHref(), true);
        }
        catch(ApiException exception) when(exception.StatusCode == HttpStatusCode.Gone) { NavigationManager.NavigateTo("/account/logout", true); }
        catch(UnauthorizedAccessException)
        {
            NavigationManager.NavigateTo(GetLoginHref(), true);
        }
        catch(Exception) { ToastService.ShowError(Localization["General.CompleteProfile.ProfileSaveFailed"]); }
        finally { _isSaving = false; }
    }

    private async Task CheckUsernameAvailabilityAsync()
    {
        _usernameAvailabilityMessage = null;
        _usernameAvailabilityClass = "form-text";
        _usernameIsAvailable = null;
        if(string.IsNullOrWhiteSpace(_model.Username) || !System.Text.RegularExpressions.Regex.IsMatch(_model.Username.Trim(), "^[a-zA-Z0-9]{3,32}$")) return;
        try
        {
            var availability = await UserClient.CheckUsernameAvailabilityAsync(_model.Username);
            _usernameIsAvailable = availability.IsAvailable;
            _usernameAvailabilityMessage = availability.IsAvailable ? Localization["General.CompleteProfile.UsernameAvailable"] : availability.Reason ?? Localization["General.CompleteProfile.UsernameUnavailable"];
            _usernameAvailabilityClass = availability.IsAvailable ? "form-text text-success" : "form-text text-danger";
        }
        catch(Exception) { _usernameAvailabilityMessage = Localization["General.CompleteProfile.UsernameCheckOnSave"]; _usernameAvailabilityClass = "form-text text-warning"; }
    }

    private int GetFirstInvalidStep()
    {
        foreach(var (fields, stepIndex) in StepFields.Select((fields, index) => (fields, index)))
            if(fields.Any(field => _editContext?.GetValidationMessages(new FieldIdentifier(_model, field)).Any() == true)) return stepIndex;
        return LastStepIndex;
    }

    private string GetStepClass(int stepIndex) => stepIndex < _activeStep ? "complete-profile-step complete-profile-step--complete" : stepIndex == _activeStep ? "complete-profile-step complete-profile-step--active" : "complete-profile-step";

    private void PrefillFromClaims(ClaimsPrincipal user)
    {
        _email = FindClaim(user, ClaimTypes.Email);
        _model.Username = FindClaim(user, "preferred_username", "nickname") ?? _model.Username;
        _model.Firstname = FindClaim(user, "given_name") ?? _model.Firstname;
        _model.Lastname = FindClaim(user, "family_name") ?? _model.Lastname;
        if(string.IsNullOrWhiteSpace(_model.Firstname) && string.IsNullOrWhiteSpace(_model.Lastname))
        {
            var name = FindClaim(user, "name");
            if(!string.IsNullOrWhiteSpace(name))
            {
                var nameParts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _model.Firstname = nameParts.ElementAtOrDefault(0) ?? _model.Firstname;
                _model.Lastname = nameParts.ElementAtOrDefault(1) ?? _model.Lastname;
            }
        }
    }

    private string GetApiError(ApiException exception, string? fallback = null)
    {
        fallback ??= Localization["General.CompleteProfile.ProfileSaveFailed"];
        var message = exception.GetApiError()?.Message;
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }

    private static string? FindClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach(var claimType in claimTypes)
        {
            var claimValue = user.FindFirst(claimType)?.Value;
            if(!string.IsNullOrWhiteSpace(claimValue)) return claimValue;
        }
        return null;
    }

}
