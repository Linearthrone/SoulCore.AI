using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using House.ChatDesktop.Services;
using Velopack;

namespace House.ChatDesktop;

public partial class MainWindow
{
    private PresenceUpdateService? _updates;
    private UpdateInfo? _pendingUpdate;
    private bool _updateBusy;
    private string? _lastHostVersion;
    private bool? _healthHasPersona;
    private bool? _healthCapabilitiesPersonas;
    private PresenceUpdateCheckResult.Kind? _presenceUpdateKind;
    private string? _presenceAvailableVersion;

    private void InitPresenceUpdates()
    {
        _updates = new PresenceUpdateService(_uiSettings.UpdateFeedUrl);
        if (UpdateVersionBox is not null)
            UpdateVersionBox.Text = _updates.CurrentVersion;
        if (UpdateFeedBox is not null)
            UpdateFeedBox.Text = _updates.FeedDescription;
        if (UpdateStatusText is not null)
        {
            UpdateStatusText.Text = _updates.IsInstalled
                ? "Installed build — Update downloads Presence from CreatorEdition GitHub Releases when a presence-v* release exists."
                : "Unpackaged build — Update cannot install. Pack with pack-presence.ps1 -Publish, install Setup.exe, then Update works.";
        }

        // Unpackaged is already known without a network check.
        if (!_updates.IsInstalled)
            _presenceUpdateKind = PresenceUpdateCheckResult.Kind.DevBuild;

        RefreshBuildVersionChrome(hostVersion: null);

        // Quiet background check (installed builds only).
        if (_updates.IsInstalled)
            _ = CheckForUpdatesAsync(showToastIfAvailable: true, fromButton: false);
    }

    private void RefreshBuildVersionChrome(string? hostVersion)
    {
        if (hostVersion is not null)
            _lastHostVersion = string.IsNullOrWhiteSpace(hostVersion) ? null : hostVersion.Trim();

        var presence = _updates?.CurrentVersion ?? "—";
        var host = string.IsNullOrWhiteSpace(_lastHostVersion) ? "—" : _lastHostVersion;
        var installed = _updates?.IsInstalled ?? false;
        var hostBehind = BuildCurrencyStatus.IsHostBehindPresence(
            host == "—" ? null : host,
            presence == "—" ? null : presence);
        var presenceCurrency = BuildCurrencyStatus.PresenceCurrencyLabel(
            _presenceUpdateKind,
            _presenceAvailableVersion,
            installed);
        var hostCurrency = BuildCurrencyStatus.HostCurrencyLabel(
            _personaApiOk,
            _healthHasPersona,
            hostBehind,
            _healthCapabilitiesPersonas);
        var hostShort = BuildCurrencyStatus.HostCurrencyShort(
            _personaApiOk,
            _healthHasPersona,
            hostBehind,
            _healthCapabilitiesPersonas);

        if (BuildVersionsText is not null)
        {
            BuildVersionsText.Text = BuildCurrencyStatus.FormatTitleChrome(
                presence,
                host == "—" ? null : host,
                presenceCurrency,
                hostShort);
        }

        if (UpdateHostVersionBox is not null)
            UpdateHostVersionBox.Text = host;
        if (UpdateVersionBox is not null && _updates is not null)
            UpdateVersionBox.Text = _updates.CurrentVersion;
        if (UpdatePresenceCurrencyBox is not null)
            UpdatePresenceCurrencyBox.Text = presenceCurrency;
        if (UpdateHostCurrencyBox is not null)
            UpdateHostCurrencyBox.Text = hostCurrency;

        Title = $"SoulCore CreatorEdition — Presence {presence}";
    }

    private async void UpdateCheck_Click(object? sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(showToastIfAvailable: true, fromButton: true);

    private async void UpdateApply_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateBusy || _updates is null || _pendingUpdate is null)
            return;

        _updateBusy = true;
        SetUpdateUiBusy(true, "Downloading update…");
        try
        {
            var result = await _updates.DownloadAndApplyAsync(
                _pendingUpdate,
                progress => Dispatcher.UIThread.Post(() =>
                {
                    if (UpdateStatusText is not null)
                        UpdateStatusText.Text = $"Downloading… {progress}%";
                }));

            SetUpdateStatus(result.Message);
            if (!result.Ok)
                ShowUpdateToast(result.Message, showAction: false);
        }
        finally
        {
            _updateBusy = false;
            SetUpdateUiBusy(false, null);
        }
    }

    private void UpdateToastDismiss_Click(object? sender, RoutedEventArgs e)
    {
        if (UpdateToastBar is not null)
            UpdateToastBar.IsVisible = false;
    }

    private async Task CheckForUpdatesAsync(bool showToastIfAvailable, bool fromButton)
    {
        if (_updateBusy || _updates is null)
            return;

        _updateBusy = true;
        SetUpdateUiBusy(true, fromButton ? "Checking for updates…" : null);
        try
        {
            var result = await _updates.CheckAsync();
            await Dispatcher.UIThread.InvokeAsync(() => ApplyUpdateCheckResult(result, showToastIfAvailable, fromButton));
        }
        finally
        {
            _updateBusy = false;
            SetUpdateUiBusy(false, null);
        }
    }

    private void ApplyUpdateCheckResult(PresenceUpdateCheckResult result, bool showToastIfAvailable, bool fromButton)
    {
        _pendingUpdate = result.Update;
        _presenceUpdateKind = result.Status;
        _presenceAvailableVersion = result.AvailableVersion;
        if (UpdateApplyButton is not null)
            UpdateApplyButton.IsEnabled = result.Status == PresenceUpdateCheckResult.Kind.Available;
        if (UpdateVersionBox is not null && !string.IsNullOrWhiteSpace(result.CurrentVersion))
            UpdateVersionBox.Text = result.CurrentVersion;

        SetUpdateStatus(result.Message);
        RefreshBuildVersionChrome(_lastHostVersion);

        if (result.Status == PresenceUpdateCheckResult.Kind.Available && showToastIfAvailable)
            ShowUpdateToast(result.Message, showAction: true);
        else if (showToastIfAvailable && fromButton
                 && result.Status is PresenceUpdateCheckResult.Kind.Failed
                     or PresenceUpdateCheckResult.Kind.FeedEmpty
                     or PresenceUpdateCheckResult.Kind.DevBuild)
            ShowUpdateToast(result.Message, showAction: false);
    }

    private void ShowUpdateToast(string message, bool showAction)
    {
        if (UpdateToastBar is null || UpdateToastText is null)
            return;
        UpdateToastText.Text = message;
        if (UpdateToastActionButton is not null)
            UpdateToastActionButton.IsVisible = showAction;
        UpdateToastBar.IsVisible = true;
    }

    private void SetUpdateStatus(string message)
    {
        if (UpdateStatusText is not null)
            UpdateStatusText.Text = message;
    }

    private void SetUpdateUiBusy(bool busy, string? status)
    {
        if (UpdateCheckButton is not null)
            UpdateCheckButton.IsEnabled = !busy;
        if (UpdateApplyButton is not null && !busy)
            UpdateApplyButton.IsEnabled = _pendingUpdate is not null;
        else if (UpdateApplyButton is not null && busy)
            UpdateApplyButton.IsEnabled = false;
        if (status is not null)
            SetUpdateStatus(status);
    }
}
