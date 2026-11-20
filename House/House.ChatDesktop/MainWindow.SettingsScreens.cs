using Avalonia.Controls;
using Avalonia.Interactivity;
using House.ChatDesktop.Controls;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

/// <summary>PROP-22.3 / 22.9 — Settings, Advanced, performance/system log, masked secret rows.</summary>
public partial class MainWindow
{
    private enum LogKind
    {
        Performance,
        System
    }

    private readonly SoulCoreSystemLogClient _systemLog = new();
    private LogKind _logKind = LogKind.Performance;
    private bool _settingsHydrating;

    private void InitSettingsScreens()
    {
        // Only Settings → Advanced edits the local client port; nothing else touches the bind.
        SystemBindBox.Text = ConnectionDefaults.DefaultHost;
        RefreshSecretRows();
    }

    private void RefreshSettingsScreen()
    {
        SeedNotificationControls();
        RefreshCompanionTokenStatus();
        RefreshSecretRows();

        _settingsHydrating = true;
        try
        {
            var left = _uiSettings.VmDrawerOnLeft();
            VmDrawerSideCombo.SelectedIndex = left ? 0 : 1;
        }
        finally
        {
            _settingsHydrating = false;
        }

        ApplySystemMailboxesList();
        _ = RefreshEmailAccountsAsync();
    }

    // ── Masked secret rows: status is "Saved" / "Not set"; the value is never put back on screen ──

    private (TextBox Box, TextBlock Status) SecretControls(string envName) =>
        envName switch
        {
            SecretOverrideStore.OllamaKey => (OllamaKeyBox, OllamaKeyStatusText),
            SecretOverrideStore.SonioxKey => (SonioxKeyBox, SonioxKeyStatusText),
            SecretOverrideStore.SyntheticKey => (SyntheticKeyBox, SyntheticKeyStatusText),
            _ => throw new ArgumentOutOfRangeException(nameof(envName), envName, "Unknown secret row")
        };

    private void RefreshSecretRows()
    {
        foreach (var env in new[]
                 {
                     SecretOverrideStore.OllamaKey,
                     SecretOverrideStore.SonioxKey,
                     SecretOverrideStore.SyntheticKey
                 })
        {
            var (box, status) = SecretControls(env);
            var set = SecretOverrideStore.IsSet(env);
            status.Text = set ? "Saved" : "Not set";
            box.Watermark = set ? "Saved — paste a new key to replace" : "Paste key";
        }
    }

    private void SecretSave_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not string env || !SecretOverrideStore.IsKnown(env))
            return;

        var (box, _) = SecretControls(env);
        var typed = box.Text?.Trim();
        if (string.IsNullOrEmpty(typed))
        {
            SecretStatusText.Text = "Paste a key first, then Save.";
            return;
        }

        if (!SecretOverrideStore.Save(env, typed))
        {
            SecretStatusText.Text = "Could not save the key on this PC.";
            return;
        }

        // A Presence-started Host inherits process env; push the new value now.
        SecretOverrideStore.ApplyToProcessEnv(overwrite: true);
        box.Text = "";
        RefreshSecretRows();
        SecretStatusText.Text = "Saved. Takes effect the next time Presence starts the Host (use Restart).";
        _performanceLog.Add($"Key saved: {env}");
    }

    private void SecretClear_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not string env || !SecretOverrideStore.IsKnown(env))
            return;

        var (box, _) = SecretControls(env);
        SecretOverrideStore.Clear(env);
        Environment.SetEnvironmentVariable(env, null);
        box.Text = "";
        RefreshSecretRows();
        SecretStatusText.Text = "Cleared from Presence. A value in SoulCore/.env still applies.";
        _performanceLog.Add($"Key cleared: {env}");
    }

    // ── VM drawer side ───────────────────────────────────────────────────────────────────────────

    private void VmDrawerSideCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_settingsHydrating || VmDrawerSideCombo.SelectedItem is not ComboBoxItem item)
            return;

        var side = item.Tag as string == LocalUiSettings.VmDrawerLeft
            ? LocalUiSettings.VmDrawerLeft
            : LocalUiSettings.VmDrawerRight;
        if (string.Equals(_uiSettings.VmDrawerSide, side, StringComparison.Ordinal))
            return;

        _uiSettings.VmDrawerSide = side;
        _uiSettings.Save();
        ApplyVmDrawerSide();
    }

    // ── Logs ─────────────────────────────────────────────────────────────────────────────────────

    private void PerformanceLog_Click(object? sender, RoutedEventArgs e)
    {
        _logKind = LogKind.Performance;
        ShowScreen(Screen.Log);
    }

    private void SystemLog_Click(object? sender, RoutedEventArgs e)
    {
        _logKind = LogKind.System;
        ShowScreen(Screen.Log);
    }

    private void RefreshLogScreen()
    {
        switch (_logKind)
        {
            case LogKind.Performance:
                RenderPerformanceLog();
                break;
            case LogKind.System:
                _ = RenderSystemLogAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_logKind), _logKind, null);
        }
    }

    private void RenderPerformanceLog()
    {
        LogTitleText.Text = "Performance log";
        LogClearButton.IsVisible = true;
        var text = _performanceLog.Render();
        LogBodyBox.Text = string.IsNullOrWhiteSpace(text)
            ? "Nothing logged this session. System notices (send failures, Host actions, key saves) land here instead of the chat."
            : text;
        LogStatusText.Text = $"{_performanceLog.Count} entries · this session only";
        LogBodyBox.CaretIndex = LogBodyBox.Text.Length;
    }

    private async Task RenderSystemLogAsync()
    {
        LogTitleText.Text = "System log";
        LogClearButton.IsVisible = false;
        LogStatusText.Text = "Loading…";
        var fetch = await _systemLog.GetAsync(300).ConfigureAwait(true);
        if (!fetch.Available)
        {
            LogBodyBox.Text = fetch.Problem ?? "System log is not available from this Host.";
            LogStatusText.Text = "";
            return;
        }

        LogBodyBox.Text = fetch.Lines.Count == 0 ? "(empty)" : string.Join('\n', fetch.Lines);
        LogStatusText.Text = string.IsNullOrWhiteSpace(fetch.Path)
            ? $"{fetch.Lines.Count} lines"
            : $"{fetch.Lines.Count} lines · {fetch.Path}";
        LogBodyBox.CaretIndex = LogBodyBox.Text.Length;
    }

    private void LogRefresh_Click(object? sender, RoutedEventArgs e) => RefreshLogScreen();

    private void LogClear_Click(object? sender, RoutedEventArgs e)
    {
        if (_logKind != LogKind.Performance)
            return;
        _performanceLog.Clear();
        RenderPerformanceLog();
    }

    // ── Advanced ─────────────────────────────────────────────────────────────────────────────────

    private void Advanced_Click(object? sender, RoutedEventArgs e) => ShowScreen(Screen.Advanced);

    private void RefreshAdvancedScreen()
    {
        SystemBindBox.Text = ConnectionDefaults.DefaultHost;
        SystemPortBox.Text = ConnectionDefaults.Port.ToString();
        EndpointText.Text = ConnectionDefaults.DisplayEndpoint;
        AdvancedPortBox.Text = "";
        ApplySystemStatus(_lastHealth);
        _ = RefreshEmailAccountsAsync();
    }

    private async void AdvancedPortSave_Click(object? sender, RoutedEventArgs e)
    {
        var typed = (AdvancedPortBox.Text ?? "").Trim();
        if (!int.TryParse(typed, out var port) || port is < 1024 or > 65535)
        {
            AdvancedPortStatusText.Text = "Enter a port between 1024 and 65535.";
            return;
        }

        if (ConnectionDefaults.PortPinnedByEnvironment)
        {
            AdvancedPortStatusText.Text = "HOUSE_SOULCORE_PORT is set in the environment and always wins — unset it to change the port here.";
            return;
        }

        if (port == ConnectionDefaults.Port)
        {
            AdvancedPortStatusText.Text = "That is already the port in use.";
            return;
        }

        var confirmed = await ConfirmDialogWindow.ShowAsync(
            this,
            "Change client port",
            $"Presence will look for the Host on 127.0.0.1:{port} instead of {ConnectionDefaults.Port}.",
            confirmText: "Change port",
            warning: "This only changes where this app connects. It does not move the Host. "
                     + "If the Host is not listening on the new port, Presence will go offline until you set it back.")
            .ConfigureAwait(true);
        if (!confirmed)
        {
            AdvancedPortStatusText.Text = "Cancelled — port unchanged.";
            return;
        }

        _uiSettings.ClientPort = port;
        _uiSettings.Save();
        ConnectionDefaults.UseClientPort(port);
        SystemPortBox.Text = port.ToString();
        EndpointText.Text = ConnectionDefaults.DisplayEndpoint;
        AdvancedPortBox.Text = "";
        AdvancedPortStatusText.Text = $"Client port set to {port}. Reconnecting…";
        _performanceLog.Add($"Client port → {port}");
        await _ws.ConnectAsync().ConfigureAwait(true);
        await ProbeHealthAsync().ConfigureAwait(true);
    }
}
