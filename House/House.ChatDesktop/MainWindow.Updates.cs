using System;
using System.Runtime.InteropServices;
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
    private bool _stackRestartBusy;

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
                ? "Installed build — Update pulls Host + Presence (Velopack) when a presence-v* release exists."
                : "Unpackaged build — Host update still runs; Presence pack needs Setup.exe. Use pack-presence.ps1 -Publish.";
        }

        RefreshBuildVersionChrome(hostVersion: null);
        _ = RefreshUpdateStatusAsync(showToastIfAvailable: false, fromButton: false);

        // Quiet background Presence check (installed builds only).
        if (_updates.IsInstalled)
            _ = CheckForUpdatesAsync(showToastIfAvailable: true, fromButton: false);
    }

    private void RefreshBuildVersionChrome(string? hostVersion)
    {
        var presence = _updates?.CurrentVersion ?? "—";
        var host = string.IsNullOrWhiteSpace(hostVersion) ? "—" : hostVersion.Trim();
        if (BuildVersionsText is not null)
            BuildVersionsText.Text = $"Presence {presence} · Host {host}";
        if (UpdateHostVersionBox is not null)
            UpdateHostVersionBox.Text = host;
        Title = $"House Victoria — Presence {presence}";
    }

    private async void UpdateCheck_Click(object? sender, RoutedEventArgs e) =>
        await RefreshUpdateStatusAsync(showToastIfAvailable: true, fromButton: true);

    private async void UpdateNow_Click(object? sender, RoutedEventArgs e) =>
        await RunFullUpdatePipelineAsync();

    private async void RestartStack_Click(object? sender, RoutedEventArgs e) =>
        await RunRestartStackAsync();

    private async void UpdateApply_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateBusy || _stackRestartBusy || _updates is null || _pendingUpdate is null)
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

    /// <summary>Settings "Check for updates" — status only (git + Host /health + Presence feed).</summary>
    private async Task RefreshUpdateStatusAsync(bool showToastIfAvailable, bool fromButton)
    {
        if (_updateBusy || _stackRestartBusy || _updates is null)
            return;

        _updateBusy = true;
        SetUpdateUiBusy(true, fromButton ? "Checking for updates…" : null);
        try
        {
            var git = await _stack.GetGitStatusAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (UpdateGitStatusBox is not null)
                    UpdateGitStatusBox.Text = git.Ok ? git.Detail : git.Detail;
            });

            var health = await _health.ProbeAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                RefreshBuildVersionChrome(health.HostVersion));

            var result = await _updates.CheckAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                ApplyUpdateCheckResult(result, showToastIfAvailable, fromButton));
        }
        finally
        {
            _updateBusy = false;
            SetUpdateUiBusy(false, null);
        }
    }

    /// <summary>Quiet Presence-only check used at startup for installed builds.</summary>
    private async Task CheckForUpdatesAsync(bool showToastIfAvailable, bool fromButton)
    {
        if (_updateBusy || _stackRestartBusy || _updates is null)
            return;

        _updateBusy = true;
        try
        {
            var result = await _updates.CheckAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                ApplyUpdateCheckResult(result, showToastIfAvailable, fromButton));
        }
        finally
        {
            _updateBusy = false;
        }
    }

    /// <summary>
    /// PROP-16: git pull-if-behind → Host rebuild/restart → Presence Velopack apply last.
    /// </summary>
    private async Task RunFullUpdatePipelineAsync()
    {
        if (_updateBusy || _stackRestartBusy || _updates is null)
            return;

        _updateBusy = true;
        SetUpdateUiBusy(true, "Starting update…");
        try
        {
            if (_stack.RepoRoot is null)
            {
                var msg =
                    "SoulCore repo not found. Set HOUSE_SOULCORE_REPO or Presence Settings → SoulCore repo folder " +
                    "(the folder that contains ALLSTART.ps1), then try Update again.";
                SetUpdateStatus(msg);
                ShowUpdateToast(msg, showAction: false);
                return;
            }

            SetUpdateStatus("Checking git…");
            var git = await _stack.GetGitStatusAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (UpdateGitStatusBox is not null)
                    UpdateGitStatusBox.Text = git.Detail;
            });

            if (!git.Ok)
            {
                SetUpdateStatus(git.Detail);
                ShowUpdateToast(git.Detail, showAction: false);
                return;
            }

            var skipPull = false;
            if (git.Behind > 0)
            {
                var pullOk = await ConfirmYesNoAsync(
                    $"Checkout is behind upstream by {git.Behind} commit(s).\n\nPull with git pull --ff-only?",
                    "Update — git pull").ConfigureAwait(true);
                if (!pullOk)
                {
                    if (git.Dirty)
                    {
                        var skip = await ConfirmYesNoAsync(
                            "Pull skipped. Working tree is dirty — continue Host rebuild from disk without pulling?",
                            "Update — skip pull").ConfigureAwait(true);
                        if (!skip)
                        {
                            SetUpdateStatus("Update cancelled (pull declined).");
                            return;
                        }

                        skipPull = true;
                    }
                    else
                    {
                        var skip = await ConfirmYesNoAsync(
                            "Pull skipped. Continue Host rebuild from current disk state?",
                            "Update — skip pull").ConfigureAwait(true);
                        if (!skip)
                        {
                            SetUpdateStatus("Update cancelled (pull declined).");
                            return;
                        }

                        skipPull = true;
                    }
                }
                else if (git.Dirty)
                {
                    // Warn: ff-only may still fail if dirty conflicts — try pull; on fail offer skip.
                }

                if (!skipPull)
                {
                    SetUpdateStatus("git pull --ff-only…");
                    var pull = await _stack.PullAsync().ConfigureAwait(false);
                    if (!pull.Ok)
                    {
                        var continueAnyway = await ConfirmYesNoAsync(
                            $"Pull failed (no force / no hard reset):\n{pull.Detail}\n\n" +
                            "Skip pull and continue Host rebuild from disk?",
                            "Update — pull failed").ConfigureAwait(true);
                        if (!continueAnyway)
                        {
                            SetUpdateStatus($"Update stopped: {pull.Detail}");
                            ShowUpdateToast(pull.Detail, showAction: false);
                            return;
                        }
                    }
                    else
                    {
                        SetUpdateStatus($"Pulled: {pull.Detail}");
                    }
                }
            }
            else if (git.Dirty)
            {
                SetUpdateStatus("Git up to date (working tree dirty) — rebuilding Host from disk.");
            }

            var progress = new Progress<string>(msg =>
                Dispatcher.UIThread.Post(() => SetUpdateStatus(msg)));
            SetUpdateStatus("Updating Host…");
            var host = await _stack.UpdateHostAsync(progress: progress).ConfigureAwait(false);
            if (!host.Ok)
            {
                SetUpdateStatus(host.Detail);
                ShowUpdateToast(host.Detail, showAction: false);
                return;
            }

            var health = await _health.ProbeAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                RefreshBuildVersionChrome(health.HostVersion));

            SetUpdateStatus("Checking Presence feed…");
            var presence = await _updates.CheckAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                ApplyUpdateCheckResult(presence, showToastIfAvailable: false, fromButton: true));

            if (presence.Status == PresenceUpdateCheckResult.Kind.Available && presence.Update is not null)
            {
                var apply = await ConfirmYesNoAsync(
                    $"{presence.Message}\n\nDownload & restart Presence now? (Host update already finished.)",
                    "Update — Presence").ConfigureAwait(true);
                if (apply)
                {
                    SetUpdateStatus("Downloading Presence update…");
                    var applied = await _updates.DownloadAndApplyAsync(
                        presence.Update,
                        p => Dispatcher.UIThread.Post(() =>
                        {
                            if (UpdateStatusText is not null)
                                UpdateStatusText.Text = $"Downloading… {p}%";
                        })).ConfigureAwait(false);
                    SetUpdateStatus(applied.Message);
                    if (!applied.Ok)
                        ShowUpdateToast(applied.Message, showAction: false);
                    return;
                }
            }

            var done = presence.Status switch
            {
                PresenceUpdateCheckResult.Kind.Available =>
                    $"Host updated. Presence update available — use Download & restart when ready. ({host.Detail})",
                PresenceUpdateCheckResult.Kind.DevBuild or PresenceUpdateCheckResult.Kind.FeedEmpty =>
                    $"Host updated. {presence.Message}",
                PresenceUpdateCheckResult.Kind.UpToDate =>
                    $"Host + Presence up to date. {host.Detail}",
                _ => $"Host updated. Presence: {presence.Message}"
            };
            SetUpdateStatus(done);
            ShowUpdateToast(done, showAction: presence.Status == PresenceUpdateCheckResult.Kind.Available);
        }
        finally
        {
            _updateBusy = false;
            SetUpdateUiBusy(false, null);
        }
    }

    private async Task RunRestartStackAsync()
    {
        if (_updateBusy || _stackRestartBusy)
            return;

        var ok = await ConfirmYesNoAsync(
            "This will stop Presence and bring the full stack back up (ALLSTOP → ALLSTART).\n\nContinue?",
            "Restart stack").ConfigureAwait(true);
        if (!ok)
            return;

        _stackRestartBusy = true;
        SetUpdateUiBusy(true, "Restarting stack…");
        try
        {
            if (_stack.RepoRoot is null)
            {
                var msg =
                    "SoulCore repo not found. Set HOUSE_SOULCORE_REPO or Presence Settings → SoulCore repo folder.";
                SetUpdateStatus(msg);
                ShowUpdateToast(msg, showAction: false);
                return;
            }

            var result = await _stack.RestartStackAsync().ConfigureAwait(false);
            if (!result.Ok)
            {
                SetUpdateStatus(result.Detail);
                ShowUpdateToast(result.Detail, showAction: false);
                return;
            }

            SetUpdateStatus("Restarting stack… Presence will exit when ALLSTOP runs.");
            ShowUpdateToast("Restarting stack…", showAction: false);
        }
        finally
        {
            _stackRestartBusy = false;
            SetUpdateUiBusy(false, null);
        }
    }

    private void ApplyUpdateCheckResult(PresenceUpdateCheckResult result, bool showToastIfAvailable, bool fromButton)
    {
        _pendingUpdate = result.Update;
        if (UpdateApplyButton is not null)
            UpdateApplyButton.IsEnabled = result.Status == PresenceUpdateCheckResult.Kind.Available;
        if (UpdateVersionBox is not null && !string.IsNullOrWhiteSpace(result.CurrentVersion))
            UpdateVersionBox.Text = result.CurrentVersion;

        SetUpdateStatus(result.Message);

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
        var enable = !busy;
        if (UpdateCheckButton is not null)
            UpdateCheckButton.IsEnabled = enable;
        if (UpdateNowButton is not null)
            UpdateNowButton.IsEnabled = enable;
        if (RestartStackButton is not null)
            RestartStackButton.IsEnabled = enable;
        if (NavUpdate is not null)
            NavUpdate.IsEnabled = enable;
        if (NavRestartStack is not null)
            NavRestartStack.IsEnabled = enable;
        if (UpdateApplyButton is not null && !busy)
            UpdateApplyButton.IsEnabled = _pendingUpdate is not null;
        else if (UpdateApplyButton is not null && busy)
            UpdateApplyButton.IsEnabled = false;
        if (status is not null)
            SetUpdateStatus(status);
    }

    private static Task<bool> ConfirmYesNoAsync(string message, string caption)
    {
        if (OperatingSystem.IsWindows())
        {
            // MB_YESNO | MB_ICONQUESTION | MB_TOPMOST
            const uint flags = 0x00000004 | 0x00000020 | 0x00040000;
            var result = MessageBoxW(IntPtr.Zero, message, caption, flags);
            // IDYES = 6
            return Task.FromResult(result == 6);
        }

        // Non-Windows (CI/dev): auto-decline destructive confirms so agents never nuke a checkout.
        return Task.FromResult(false);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
