using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Sponsors;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Refit;

namespace Mercurius.LAN.Web.Components.Pages.Admin;

public partial class SponsorManagement
{
    private List<Sponsor> _sponsors = new();
    private List<SponsorManagementDTO> _displaySponsors = new();
    private SponsorManagementDTO _selectedSponsor = new();
    private bool _isCreateMode = true;
    private bool _isLoading = true;
    private EditContext? _editContext;
    private ValidationMessageStore? _validationMessages;
    private CustomInputFile? _logoInputRef;
    private CustomInputFile? _lightLogoInputRef;

    private CustomAutocomplete<SponsorManagementDTO> _autoCompleteComponent = null!;

    [Inject]
    private ISponsorService SponsorService { get; set; } = null!;
    [Inject]
    private IToastService ToastService { get; set; } = null!;

    protected override void OnInitialized()
    {
        ReInitEditContext();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender)
        {
            try
            {
                _sponsors = (await SponsorService.GetSponsorsAsync()).ToList();
                SyncDisplaySponsors();
            }
            catch(Exception)
            {
                ToastService.ShowError(Localization["Admin.loadFailed"]);
            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private void ReInitEditContext()
    {
        if(_editContext is not null)
        {
            _editContext.OnValidationRequested -= HandleValidationRequested;
            _editContext.OnFieldChanged -= HandleFieldChanged;
        }

        _editContext = new(_selectedSponsor);
        _validationMessages = new(_editContext);
        _editContext.SetFieldCssClassProvider(new BootstrapValidationFieldClassProvider());
        _editContext.OnValidationRequested += HandleValidationRequested;
        _editContext.OnFieldChanged += HandleFieldChanged;
    }

    private void HandleFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        if(args.FieldIdentifier.FieldName == nameof(SponsorManagementDTO.LightLogo) && _selectedSponsor.LightLogo is not null)
            _selectedSponsor.RemoveLightLogo = false;

        _editContext?.Validate();
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        if(_validationMessages is null)
            return;

        _validationMessages.Clear();
        AddRequiredMessage(nameof(SponsorManagementDTO.Name), _selectedSponsor.Name, "Admin.name");
        AddRequiredMessage(nameof(SponsorManagementDTO.InfoUrl), _selectedSponsor.InfoUrl, "Admin.infoUrl");

        if(_selectedSponsor.IsCreateMode && _selectedSponsor.Logo is null)
        {
            var field = new FieldIdentifier(_selectedSponsor, nameof(SponsorManagementDTO.Logo));
            _validationMessages.Add(field, Localization.Get("form.requiredField", Localization["Admin.logo"]));
        }

        if(_selectedSponsor.LightLogo is not null && _selectedSponsor.RemoveLightLogo)
        {
            var field = new FieldIdentifier(_selectedSponsor, nameof(SponsorManagementDTO.RemoveLightLogo));
            _validationMessages.Add(field, Localization["Admin.removeLightLogoConflict"]);
        }
    }

    private void AddRequiredMessage(string propertyName, string? value, string labelKey)
    {
        if(!string.IsNullOrWhiteSpace(value))
            return;

        var field = new FieldIdentifier(_selectedSponsor, propertyName);
        _validationMessages?.Add(field, Localization.Get("form.requiredField", Localization[labelKey]));
    }
    private void OnSponsorSelected(SponsorManagementDTO sponsor)
    {
        ClearLogoInputs();
        _selectedSponsor = sponsor;
        _isCreateMode = false;
        ReInitEditContext();
        _selectedSponsor.IsCreateMode = false;
        StateHasChanged();
    }

    private void ClearForm()
    {
        ClearLogoInputs();
        _selectedSponsor = new SponsorManagementDTO();
        _isCreateMode = true;
        _selectedSponsor.IsCreateMode = true;
        SyncDisplaySponsors();
        _autoCompleteComponent.ClearSearchField();       
        ReInitEditContext();
        StateHasChanged();
    }
    private async Task HandleSubmit()
    {
        string? logoTempFilePath = _logoInputRef?.TempFilePath;
        string? logoContentType = _logoInputRef?.FileContentType;
        string? logoFileName = _logoInputRef?.FileName;
        string? lightLogoTempFilePath = _lightLogoInputRef?.TempFilePath;
        string? lightLogoContentType = _lightLogoInputRef?.FileContentType;
        string? lightLogoFileName = _lightLogoInputRef?.FileName;

        try
        {
            if(_isCreateMode)
            {
                var sponsor = await SponsorService.CreateSponsorAsync(
                    _selectedSponsor,
                    logoTempFilePath,
                    logoContentType,
                    logoFileName,
                    lightLogoTempFilePath,
                    lightLogoContentType,
                    lightLogoFileName);
                _sponsors.Add(sponsor);
                _selectedSponsor.Logo = null;
                _selectedSponsor.LightLogo = null;
                _selectedSponsor.LogoUrl = sponsor.LogoUrl;
                _selectedSponsor.LightLogoUrl = sponsor.LightLogoUrl;
                ClearLogoInputs();
                SyncDisplaySponsors();
                ToastService.ShowSuccess(Localization["Admin.created"]);
            }
            else
            {
                var sponsor = await SponsorService.UpdateSponsorAsync(
                    _selectedSponsor.Id,
                    _selectedSponsor,
                    logoTempFilePath,
                    logoContentType,
                    logoFileName,
                    lightLogoTempFilePath,
                    lightLogoContentType,
                    lightLogoFileName);
                var index = _sponsors.FindIndex(existing => existing.Id == sponsor.Id);
                if(index >= 0)
                    _sponsors[index] = sponsor;

                _selectedSponsor = new SponsorManagementDTO(sponsor);
                _isCreateMode = false;
                ClearLogoInputs();
                ReInitEditContext();
                SyncDisplaySponsors();
                ToastService.ShowSuccess(Localization["Admin.updated"]);
            }
        }
        catch(ApiException ex)
        {
            ToastService.ShowError(GetApiError(ex));
        }
    }

    private async Task DeleteSponsor()
    {
        if(_selectedSponsor == null)
            return;
        try
        {
            await SponsorService.DeleteSponsorAsync(_selectedSponsor.Id);
            _sponsors.Remove(_sponsors.SingleOrDefault(_sponsors => _sponsors.Id == _selectedSponsor.Id)!);
            SyncDisplaySponsors();
            ToastService.ShowSuccess(Localization["Admin.deleted"]);
            ClearForm();
        }
        catch(ApiException ex)
        {
            ToastService.ShowError(GetApiError(ex));
        }
    }

    private string GetApiError(ApiException exception)
    {
        var message = exception.GetApiError()?.Message;
        return string.IsNullOrWhiteSpace(message) ? Localization["common.unexpectedError"] : message;
    }

    private void SyncDisplaySponsors()
    {
        _displaySponsors = _sponsors
            .Select(sponsor =>
            {
                var dto = new SponsorManagementDTO(sponsor);
                dto.IsCreateMode = _isCreateMode;
                return dto;
            })
            .ToList();
    }

    private void ClearLogoInputs()
    {
        _logoInputRef?.ClearSelection();
        _lightLogoInputRef?.ClearSelection();
    }

    private string GetTierLabel(SponsorTier tier)
    {
        return tier switch
        {
            SponsorTier.Presenting => Localization["Admin.tier.presenting"],
            SponsorTier.Gold => Localization["Admin.tier.gold"],
            SponsorTier.Silver => Localization["Admin.tier.silver"],
            SponsorTier.Bronze => Localization["Admin.tier.bronze"],
            _ => tier.ToString()
        };
    }
}
