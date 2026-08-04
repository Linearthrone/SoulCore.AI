using SoulCore.Host.Companion;

namespace SoulCore.Protocol.Tests;

public class CompanionOutboundMessengerTests
{
    [Fact]
    public void ExtractPhrase_strips_want_prefix_and_emotion_suffix()
    {
        var want = "want[engage]: reach out and share a thought (emotion=excited v=0.80 a=0.75 d=0.60 f=0.50); recent=(none)";
        var phrase = CompanionOutboundMessenger.ExtractPhrase(want);
        Assert.Equal("reach out and share a thought", phrase);
    }

    [Fact]
    public void ComposeProactiveText_engage_is_user_facing()
    {
        var text = CompanionOutboundMessenger.ComposeProactiveText(
            "engage",
            "excited",
            "want[engage]: say hello (emotion=excited v=0.8 a=0.7 d=0.5 f=0.5); recent=(none)");
        Assert.StartsWith("Hey — I wanted to reach out.", text);
        Assert.Contains("say hello", text);
        Assert.DoesNotContain("want[", text);
    }
}
