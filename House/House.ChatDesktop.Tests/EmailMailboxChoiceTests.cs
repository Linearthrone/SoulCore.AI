using House.ChatDesktop.Models;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public sealed class EmailMailboxChoiceTests
{
    [Fact]
    public void BuildList_DoesNotInventThreeRoleSlots()
    {
        var accounts = new[]
        {
            new EmailAccountSnapshot
            {
                Id = "kayleigh-gmail",
                DisplayName = "LinearThrone",
                Address = "a@example.com",
                Enabled = true,
                IsConfigured = true,
                HasPassword = true
            }
        };

        var list = EmailMailboxChoice.BuildList(accounts);
        Assert.Single(list);
        Assert.Equal("kayleigh-gmail", list[0].Id);
        Assert.DoesNotContain(list, c => c.Id is "victoria" or "personal" or "business");
    }

    [Fact]
    public void BuildList_EmptyHost_ReturnsEmpty()
    {
        Assert.Empty(EmailMailboxChoice.BuildList(Array.Empty<EmailAccountSnapshot>()));
    }

    [Fact]
    public void FormatSystemListLine_NeverMentionsPassword()
    {
        var line = EmailMailboxChoice.FormatSystemListLine(new EmailAccountSnapshot
        {
            Id = "work",
            DisplayName = "LinearThrone",
            Address = "w@example.com",
            Enabled = true,
            IsConfigured = true,
            HasPassword = true
        });
        Assert.Contains("LinearThrone", line);
        Assert.DoesNotContain("password", line, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("victoria", true)]
    [InlineData("my_box-2", true)]
    [InlineData("", false)]
    [InlineData("bad id", false)]
    [InlineData("a@b", false)]
    public void IsSafeMailboxId_MatchesHostLabelRules(string id, bool expected) =>
        Assert.Equal(expected, EmailMailboxChoice.IsSafeMailboxId(id));

    [Fact]
    public void IsSafeMailboxId_RejectsOver64() =>
        Assert.False(EmailMailboxChoice.IsSafeMailboxId(new string('a', 65)));
}
