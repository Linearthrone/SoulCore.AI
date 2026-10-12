using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using House.ChatDesktop.Models;
using House.ChatDesktop.Services;

namespace House.ChatDesktop;

/// <summary>Dialog for Host mailbox accounts (unlimited ids; Host owns the set).</summary>
public partial class EmailSetupWindow : Window
{
    private readonly SoulCoreEmailSettingsClient _emailSettings;
    private IReadOnlyList<EmailAccountSnapshot> _emailAccounts = Array.Empty<EmailAccountSnapshot>();
    private bool _emailAccountsHydrating;
    private bool _savedAny;

    public bool SavedAny => _savedAny;

    public EmailSetupWindow() : this(new SoulCoreEmailSettingsClient())
    {
    }

    public EmailSetupWindow(SoulCoreEmailSettingsClient emailSettings)
    {
        _emailSettings = emailSettings;
        InitializeComponent();
        Opened += async (_, _) => await RefreshEmailAccountsAsync().ConfigureAwait(true);
    }

    private async void EmailAccountsRefresh_Click(object? sender, RoutedEventArgs e) =>
        await RefreshEmailAccountsAsync().ConfigureAwait(true);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void EmailGmailDefaults_Click(object? sender, RoutedEventArgs e)
    {
        if (EmailImapHostBox is not null) EmailImapHostBox.Text = "imap.gmail.com";
        if (EmailImapPortBox is not null) EmailImapPortBox.Text = "993";
        if (EmailImapSslCheck is not null) EmailImapSslCheck.IsChecked = true;
        if (EmailSmtpHostBox is not null) EmailSmtpHostBox.Text = "smtp.gmail.com";
        if (EmailSmtpPortBox is not null) EmailSmtpPortBox.Text = "587";
        if (EmailSmtpSslCheck is not null) EmailSmtpSslCheck.IsChecked = false;
        SetEmailStatus("Gmail defaults filled in — Save when ready", bad: false);
    }

    private async void EmailAccountSave_Click(object? sender, RoutedEventArgs e)
    {
        var id = ResolveMailboxIdForSave();
        if (string.IsNullOrWhiteSpace(id))
        {
            SetEmailStatus("Enter a mailbox id (any label — not limited to three roles)", bad: true);
            return;
        }

        if (!EmailMailboxChoice.IsSafeMailboxId(id))
        {
            SetEmailStatus("Mailbox id: letters, numbers, hyphen, underscore only", bad: true);
            return;
        }

        var address = (EmailAddressBox?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
        {
            SetEmailStatus("Enter a full email address (like name@gmail.com)", bad: true);
            return;
        }

        var password = EmailPasswordBox?.Text;
        var existing = _emailAccounts.FirstOrDefault(a =>
            string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(password) && existing is not { HasPassword: true })
        {
            SetEmailStatus("Paste an App Password once so she can sign in", bad: true);
            return;
        }

        int? imapPort = null;
        if (int.TryParse((EmailImapPortBox?.Text ?? "").Trim(), out var ip) && ip > 0)
            imapPort = ip;
        int? smtpPort = null;
        if (int.TryParse((EmailSmtpPortBox?.Text ?? "").Trim(), out var sp) && sp > 0)
            smtpPort = sp;

        var username = (EmailUsernameBox?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username))
            username = address;

        var write = new SoulCoreEmailSettingsClient.EmailAccountWriteDto
        {
            Id = id,
            Role = id,
            DisplayName = EmailDisplayNameBox?.Text,
            Address = address,
            Username = username,
            Password = string.IsNullOrWhiteSpace(password) ? null : password,
            ImapHost = EmailImapHostBox?.Text,
            ImapPort = imapPort,
            ImapUseSsl = EmailImapSslCheck?.IsChecked == true,
            SmtpHost = EmailSmtpHostBox?.Text,
            SmtpPort = smtpPort,
            SmtpUseSsl = EmailSmtpSslCheck?.IsChecked == true,
            Enabled = EmailEnabledCheck?.IsChecked == true
        };

        SetEmailStatus("Saving…", bad: false);
        var snap = await _emailSettings.UpsertAsync(write).ConfigureAwait(true);
        if (!snap.Reachable || (!string.IsNullOrWhiteSpace(snap.Detail) &&
                                snap.Detail.StartsWith("HTTP", StringComparison.Ordinal)))
        {
            ApplyEmailSettingsSnapshot(snap, saved: false, preferId: id);
            return;
        }

        _savedAny = true;
        ApplyEmailSettingsSnapshot(snap, saved: true, preferId: id);
        if (EmailPasswordBox is not null)
            EmailPasswordBox.Text = string.Empty;
    }

    private void EmailAccountCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_emailAccountsHydrating) return;
        var id = ResolveSelectedMailboxId();
        if (string.IsNullOrWhiteSpace(id)) return;
        if (EmailIdBox is not null)
            EmailIdBox.Text = id;
        var account = _emailAccounts.FirstOrDefault(a =>
            string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        if (account is not null)
            FillEmailEditor(account);
        else
            FillEmailEditor(BlankAccount(id));
    }

    private async Task RefreshEmailAccountsAsync()
    {
        var prefer = ResolveMailboxIdForSave() ?? ResolveSelectedMailboxId();
        var snap = await _emailSettings.GetAsync().ConfigureAwait(true);
        ApplyEmailSettingsSnapshot(snap, saved: false, preferId: prefer);
    }

    private void ApplyEmailSettingsSnapshot(EmailSettingsSnapshot snap, bool saved, string? preferId)
    {
        _emailAccounts = snap.Accounts ?? Array.Empty<EmailAccountSnapshot>();
        _emailAccountsHydrating = true;
        try
        {
            if (EmailAccountCombo is not null)
            {
                var choices = EmailMailboxChoice.BuildList(_emailAccounts);
                EmailAccountCombo.ItemsSource = choices;
                if (choices.Count > 0)
                {
                    var pickId = preferId;
                    if (string.IsNullOrWhiteSpace(pickId))
                        pickId = choices[0].Id;
                    var selected = choices.FirstOrDefault(c =>
                        string.Equals(c.Id, pickId, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
                    EmailAccountCombo.SelectedItem = selected;
                    if (EmailIdBox is not null)
                        EmailIdBox.Text = selected.Id;

                    var account = _emailAccounts.FirstOrDefault(a =>
                        string.Equals(a.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
                    FillEmailEditor(account ?? BlankAccount(selected.Id));
                }
                else
                {
                    EmailAccountCombo.SelectedItem = null;
                    if (EmailIdBox is not null && string.IsNullOrWhiteSpace(EmailIdBox.Text))
                        EmailIdBox.Text = "";
                    FillEmailEditor(BlankAccount(EmailIdBox?.Text ?? ""));
                }
            }
        }
        finally
        {
            _emailAccountsHydrating = false;
        }

        if (!snap.Reachable)
        {
            SetEmailStatus(snap.Detail ?? "Can't reach Host — is SoulCore running?", bad: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(snap.Detail) &&
            (snap.Detail.StartsWith("HTTP", StringComparison.Ordinal) ||
             snap.Detail.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)))
        {
            SetEmailStatus(snap.Detail, bad: true);
            return;
        }

        SetEmailStatus(
            saved
                ? $"Saved · {DateTimeOffset.Now:h:mm tt}"
                : $"Loaded · {DateTimeOffset.Now:h:mm tt}",
            bad: false);
    }

    private void FillEmailEditor(EmailAccountSnapshot account)
    {
        if (EmailEnabledCheck is not null) EmailEnabledCheck.IsChecked = account.Enabled;
        if (EmailDisplayNameBox is not null)
        {
            EmailDisplayNameBox.Text = string.IsNullOrWhiteSpace(account.DisplayName)
                ? EmailMailboxChoice.DefaultDisplayNameFor(account.Id)
                : account.DisplayName;
        }
        if (EmailAddressBox is not null) EmailAddressBox.Text = account.Address;
        if (EmailUsernameBox is not null) EmailUsernameBox.Text = account.Username;
        if (EmailImapHostBox is not null)
            EmailImapHostBox.Text = string.IsNullOrWhiteSpace(account.ImapHost) ? "imap.gmail.com" : account.ImapHost;
        if (EmailImapPortBox is not null)
            EmailImapPortBox.Text = (account.ImapPort > 0 ? account.ImapPort : 993).ToString();
        if (EmailImapSslCheck is not null) EmailImapSslCheck.IsChecked = account.ImapUseSsl;
        if (EmailSmtpHostBox is not null)
            EmailSmtpHostBox.Text = string.IsNullOrWhiteSpace(account.SmtpHost) ? "smtp.gmail.com" : account.SmtpHost;
        if (EmailSmtpPortBox is not null)
            EmailSmtpPortBox.Text = (account.SmtpPort > 0 ? account.SmtpPort : 587).ToString();
        if (EmailSmtpSslCheck is not null) EmailSmtpSslCheck.IsChecked = account.SmtpUseSsl;

        if (EmailPasswordHint is not null)
        {
            EmailPasswordHint.Text = account.HasPassword
                ? "App password already saved (won't show again — paste a new one only to replace it)"
                : "No app password saved yet";
        }

        UpdateEmailReadyBanner(account);
    }

    private void UpdateEmailReadyBanner(EmailAccountSnapshot account)
    {
        if (EmailReadyBanner is null) return;
        var title = EmailMailboxChoice.TitleFor(account.Id);
        if (string.IsNullOrWhiteSpace(account.Id))
        {
            EmailReadyBanner.Text = "Enter a mailbox id, address, and App Password, then Save.";
            EmailReadyBanner.Foreground = MuteBrush();
            return;
        }

        if (!account.Enabled)
        {
            EmailReadyBanner.Text = $"{title}: turned off — Victoria will skip this mailbox.";
            EmailReadyBanner.Foreground = WarnBrush();
            return;
        }

        if (account.IsConfigured)
        {
            EmailReadyBanner.Text = $"{title}: ready — {account.Address}";
            EmailReadyBanner.Foreground = OkBrush();
            return;
        }

        if (string.IsNullOrWhiteSpace(account.Address) && !account.HasPassword)
        {
            EmailReadyBanner.Text = $"{title}: not set up yet — add address + App Password, then Save.";
            EmailReadyBanner.Foreground = MuteBrush();
            return;
        }

        if (!account.HasPassword)
        {
            EmailReadyBanner.Text = $"{title}: needs an App Password before she can sign in.";
            EmailReadyBanner.Foreground = WarnBrush();
            return;
        }

        EmailReadyBanner.Text = $"{title}: almost ready — check the email address and Save again.";
        EmailReadyBanner.Foreground = WarnBrush();
    }

    private string? ResolveSelectedMailboxId() =>
        EmailAccountCombo?.SelectedItem switch
        {
            EmailMailboxChoice choice => choice.Id,
            string id => id,
            _ => null
        };

    private string? ResolveMailboxIdForSave()
    {
        var typed = (EmailIdBox?.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(typed))
            return typed;
        return ResolveSelectedMailboxId();
    }

    private void SetEmailStatus(string text, bool bad)
    {
        if (EmailSettingsStatusText is null) return;
        EmailSettingsStatusText.Text = text;
        EmailSettingsStatusText.Foreground = bad ? BadBrush() : MuteBrush();
    }

    private static EmailAccountSnapshot BlankAccount(string id) => new()
    {
        Id = id,
        Role = id,
        Enabled = true,
        ImapPort = 993,
        SmtpPort = 587,
        ImapUseSsl = true,
        ImapHost = "imap.gmail.com",
        SmtpHost = "smtp.gmail.com"
    };

    private IBrush OkBrush() => ResolveBrush("OkBrush", Brushes.SeaGreen);

    private IBrush WarnBrush() => ResolveBrush("WarnBrush", Brushes.Goldenrod);

    private IBrush BadBrush() => ResolveBrush("BadBrush", Brushes.IndianRed);

    private IBrush MuteBrush() => ResolveBrush("MutedBrush", Brushes.Gray);

    private IBrush ResolveBrush(string key, IBrush fallback)
    {
        if (Application.Current?.TryGetResource(key, out var resource) == true && resource is IBrush brush)
            return brush;
        return fallback;
    }
}
