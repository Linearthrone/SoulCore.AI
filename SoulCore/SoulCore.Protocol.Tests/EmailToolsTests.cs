using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference;
using SoulCore.Inference.Tools.Desktop;
using SoulCore.Inference.Tools.Email;

namespace SoulCore.Protocol.Tests;

public class EmailToolsTests
{
    private static readonly string[] AllToolNames =
    {
        "email_accounts",
        "email_inbox",
        "email_read",
        "email_search",
        "email_file",
        "email_mark",
        "email_delete",
        "email_send"
    };

    [Fact]
    public void AllEightTools_AppearInToolRegistry()
    {
        var (registry, _) = BuildRegistry(allowRead: true, allowSend: true, allowDelete: true);
        var names = registry.GetDefinitions().Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in AllToolNames)
            Assert.Contains(expected, names);
    }

    [Fact]
    public void ToolsOptions_Defaults_EmailGatesClosed()
    {
        var opts = new ToolsOptions();
        Assert.False(opts.AllowEmailRead);
        Assert.False(opts.AllowEmailSend);
        Assert.False(opts.AllowEmailDelete);
    }

    [Fact]
    public async Task EmailAccounts_WorksWithoutReadGate()
    {
        var tool = CreateTool("email_accounts", allowRead: false, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse("{}"));
        Assert.True(result.Success);
        Assert.Contains("victoria", result.Content, StringComparison.Ordinal);
        Assert.Contains("personal", result.Content, StringComparison.Ordinal);
        Assert.Contains("business", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("list_accounts", bridge.Calls);
    }

    [Theory]
    [InlineData("email_inbox", "{}")]
    [InlineData("email_read", """{"uid":"1"}""")]
    [InlineData("email_search", """{"query":"hello"}""")]
    [InlineData("email_file", """{"uid":"1","dest":"Archive"}""")]
    [InlineData("email_mark", """{"uid":"1","seen":true}""")]
    public async Task ReadFamily_AllowEmailReadFalse_RefusesAndDoesNotTouchBridge(
        string toolName,
        string argsJson)
    {
        var tool = CreateTool(toolName, allowRead: false, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse(argsJson));
        Assert.False(result.Success);
        Assert.Contains("AllowEmailRead", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task Inbox_AllowEmailReadTrue_ListsUnreadOnRequestedAccount()
    {
        var tool = CreateTool("email_inbox", allowRead: true, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse("""{"account":"personal","unread_only":true}"""));
        Assert.True(result.Success);
        Assert.Contains("Rent invoice", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("old newsletter", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("list:personal:INBOX:True", bridge.Calls);
    }

    [Fact]
    public async Task Read_ReturnsBodyForUid()
    {
        var tool = CreateTool("email_read", allowRead: true, allowSend: false, allowDelete: false, out _);
        var result = await tool.ExecuteAsync(Parse("""{"account":"victoria","uid":"v1"}"""));
        Assert.True(result.Success);
        Assert.Contains("welcome to your mailbox", result.Content, StringComparison.Ordinal);
        Assert.Contains("from: Kurt", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_FindsBySubject()
    {
        var tool = CreateTool("email_search", allowRead: true, allowSend: false, allowDelete: false, out _);
        var result = await tool.ExecuteAsync(Parse("""{"account":"business","query":"invoice"}"""));
        Assert.True(result.Success);
        Assert.Contains("Q3 invoice", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_MovesToArchiveAlias()
    {
        var tool = CreateTool("email_file", allowRead: true, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse("""{"account":"personal","uid":"p1","dest":"Archive"}"""));
        Assert.True(result.Success);
        Assert.Contains("filed", result.Content, StringComparison.Ordinal);
        var moved = await bridge.GetAsync("personal", "p1", "[Gmail]/All Mail");
        Assert.NotNull(moved);
        Assert.Equal("[Gmail]/All Mail", moved!.Folder);
    }

    [Fact]
    public async Task Mark_SetsSeen()
    {
        var tool = CreateTool("email_mark", allowRead: true, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse("""{"account":"personal","uid":"p1","seen":true}"""));
        Assert.True(result.Success);
        var msg = await bridge.GetAsync("personal", "p1", "INBOX");
        Assert.False(msg!.Unread);
    }

    [Fact]
    public async Task Delete_AllowEmailDeleteFalse_EvenConfirmed_Refuses()
    {
        var tool = CreateTool("email_delete", allowRead: true, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse("""{"account":"personal","uid":"p1","confirmed":true}"""));
        Assert.False(result.Success);
        Assert.Contains("AllowEmailDelete", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task Delete_Unconfirmed_ReturnsPrompt_NoBridgeCall()
    {
        var tool = CreateTool("email_delete", allowRead: true, allowSend: false, allowDelete: true, out var bridge);
        var result = await tool.ExecuteAsync(Parse("""{"account":"personal","uid":"p1"}"""));
        Assert.False(result.Success);
        Assert.Contains("confirm delete", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesMessage()
    {
        var tool = CreateTool("email_delete", allowRead: true, allowSend: false, allowDelete: true, out var bridge);
        var result = await tool.ExecuteAsync(Parse("""{"account":"personal","uid":"p1","confirmed":true}"""));
        Assert.True(result.Success);
        Assert.Contains("deleted", result.Content, StringComparison.Ordinal);
        Assert.Null(await bridge.GetAsync("personal", "p1", "INBOX"));
    }

    [Fact]
    public async Task Send_AllowEmailSendFalse_EvenConfirmed_Refuses()
    {
        var tool = CreateTool("email_send", allowRead: true, allowSend: false, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse(
            """{"account":"victoria","to":"kurt@example.com","subject":"hi","body":"yo","confirmed":true}"""));
        Assert.False(result.Success);
        Assert.Contains("AllowEmailSend", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task Send_Unconfirmed_ReturnsPrompt_NoBridgeCall()
    {
        var tool = CreateTool("email_send", allowRead: true, allowSend: true, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse(
            """{"account":"victoria","to":"kurt@example.com","subject":"hi","body":"yo"}"""));
        Assert.False(result.Success);
        Assert.Contains("confirm send", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kurt@example.com", result.Content, StringComparison.Ordinal);
        Assert.Empty(bridge.Calls);
    }

    [Fact]
    public async Task Send_Confirmed_Dispatches()
    {
        var tool = CreateTool("email_send", allowRead: true, allowSend: true, allowDelete: false, out var bridge);
        var result = await tool.ExecuteAsync(Parse(
            """{"account":"victoria","to":"kurt@example.com","subject":"hi","body":"yo","confirmed":true}"""));
        Assert.True(result.Success);
        Assert.Contains("sent from victoria", result.Content, StringComparison.Ordinal);
        Assert.Contains(bridge.Calls, c => c.StartsWith("send:victoria:", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizeFolder_MapsAliases()
    {
        Assert.Equal("INBOX", EmailToolSupport.NormalizeFolderName("inbox"));
        Assert.Equal("[Gmail]/All Mail", EmailToolSupport.NormalizeFolderName("Archive"));
        Assert.Equal("[Gmail]/Trash", EmailToolSupport.NormalizeFolderName("trash"));
    }

    [Fact]
    public void EmailGuidance_Append_IsIdempotent()
    {
        var once = EmailGuidance.AppendToPreamble("hello");
        Assert.Contains(EmailGuidance.Marker, once, StringComparison.Ordinal);
        Assert.Contains("email_inbox", once, StringComparison.Ordinal);
        Assert.Equal(once, EmailGuidance.AppendToPreamble(once));
    }

    private static (IToolRegistry registry, InMemoryEmailBridge bridge) BuildRegistry(
        bool allowRead,
        bool allowSend,
        bool allowDelete)
    {
        var bridge = SeededBridge();
        var options = Options.Create(new ToolsOptions
        {
            AllowEmailRead = allowRead,
            AllowEmailSend = allowSend,
            AllowEmailDelete = allowDelete
        });
        var access = new ComputerControlGate(
            allowDesktopCapture: true,
            allowBrowserCapture: true,
            allowComputerControl: false,
            allowMt4Read: false,
            allowMt4Trade: false,
            allowEmailRead: allowRead,
            allowEmailSend: allowSend,
            allowEmailDelete: allowDelete);

        var services = new ServiceCollection();
        services.AddSingleton<IEmailBridge>(bridge);
        services.AddSingleton(options);
        foreach (var t in CreateAllTools(bridge, options, access))
            services.AddSingleton<ITool>(t);
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IToolRegistry>(), bridge);
    }

    private static ITool CreateTool(
        string name,
        bool allowRead,
        bool allowSend,
        bool allowDelete,
        out InMemoryEmailBridge bridge)
    {
        bridge = SeededBridge();
        var options = Options.Create(new ToolsOptions
        {
            AllowEmailRead = allowRead,
            AllowEmailSend = allowSend,
            AllowEmailDelete = allowDelete
        });
        var access = new ComputerControlGate(
            allowDesktopCapture: true,
            allowBrowserCapture: true,
            allowComputerControl: false,
            allowMt4Read: false,
            allowMt4Trade: false,
            allowEmailRead: allowRead,
            allowEmailSend: allowSend,
            allowEmailDelete: allowDelete);
        return CreateAllTools(bridge, options, access).First(t => t.Definition.Name == name);
    }

    private static IEnumerable<ITool> CreateAllTools(
        IEmailBridge bridge,
        IOptions<ToolsOptions> options,
        IToolsAccessSettings access) =>
        new ITool[]
        {
            new EmailAccountsTool(bridge, options, access),
            new EmailInboxTool(bridge, options, access),
            new EmailReadTool(bridge, options, access),
            new EmailSearchTool(bridge, options, access),
            new EmailFileTool(bridge, options, access),
            new EmailMarkTool(bridge, options, access),
            new EmailDeleteTool(bridge, options, access),
            new EmailSendTool(bridge, options, access)
        };

    private static InMemoryEmailBridge SeededBridge()
    {
        var bridge = new InMemoryEmailBridge();
        var victoria = bridge.SeedAccount("victoria", "victoria", "victoria@example.com", "Victoria");
        victoria.Seed("v1", "Kurt <kurt@example.com>", "Welcome", "welcome to your mailbox");
        var personal = bridge.SeedAccount("personal", "personal", "kurt.personal@example.com", "Kurt personal");
        personal.Seed("p1", "Landlord <rent@example.com>", "Rent invoice", "due Friday");
        personal.Seed("p2", "News <news@example.com>", "old newsletter", "already seen", unread: false);
        var business = bridge.SeedAccount("business", "business", "kurt.biz@example.com", "Kurt business");
        business.Seed("b1", "AP <ap@client.com>", "Q3 invoice", "please pay");
        return bridge;
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}
