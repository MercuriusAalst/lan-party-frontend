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
    private CustomInputFile? _imageInputRef;

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
        _selectedSponsor = sponsor;
        _isCreateMode = false;
        ReInitEditContext();
        _selectedSponsor.IsCreateMode = false;
        StateHasChanged();
    }

    private void ClearForm()
    {
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
        string? tempFilePath = _imageInputRef?.TempFilePath;
        string? contentType = _imageInputRef?.FileContentType;
        string? fileName = _imageInputRef?.FileName;
        try
        {
            if(_isCreateMode)
            {
                var sponsor = await SponsorService.CreateSponsorAsync(new SponsorManagementDTO
                {
                    Name = _selectedSponsor!.Name,
                    InfoUrl = _selectedSponsor.InfoUrl,
                    SponsorTier = _selectedSponsor.SponsorTier,
                    Description = _selectedSponsor.Description
                }, tempFilePath,
            contentType,
            fileName);
                _sponsors.Add(sponsor);
                SyncDisplaySponsors();
                ToastService.ShowSuccess(Localization["Admin.created"]);
            }
            else
            {
                await SponsorService.UpdateSponsorAsync(_selectedSponsor.Id, new SponsorManagementDTO
                {
                    Name = _selectedSponsor.Name,
                    InfoUrl = _selectedSponsor.InfoUrl,
                    SponsorTier = _selectedSponsor.SponsorTier,
                    Description = _selectedSponsor.Description
                }, tempFilePath,
            contentType,
            fileName);
                var existingSponsor = _sponsors.SingleOrDefault(sponsor => sponsor.Id == _selectedSponsor.Id);
                if(existingSponsor != null)
                {
                    existingSponsor.Name = _selectedSponsor.Name;
                    existingSponsor.InfoUrl = _selectedSponsor.InfoUrl;
                    existingSponsor.SponsorTier = _selectedSponsor.SponsorTier;
                    existingSponsor.Description = _selectedSponsor.Description;
                }
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
        _displaySponsors = _sponsors.Select(sponsor => new SponsorManagementDTO
        {
            Name = sponsor.Name,
            InfoUrl = sponsor.InfoUrl,
            SponsorTier = sponsor.SponsorTier,
            Description = sponsor.Description,
            Id = sponsor.Id,
            IsCreateMode = _isCreateMode
        }).ToList();
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
