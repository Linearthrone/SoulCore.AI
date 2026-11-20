using SoulCore.Host.Voice;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class SonioxSpeechTextTests
{
    [Fact]
    public void StripAudioTagsForDisplay_RemovesBracketTags_KeepsWords()
    {
        var raw = "[warm] Hey. [chuckles] *That* was something...";
        var display = SonioxSpeechText.StripAudioTagsForDisplay(raw);
        Assert.DoesNotContain("[warm]", display);
        Assert.DoesNotContain("[chuckles]", display);
        Assert.Contains("Hey", display);
        Assert.Contains("*That*", display);
    }

    [Fact]
    public void NormalizeForSpeak_DropsMarkdownBoldAndStageParens()
    {
        var raw = "[softly] Hello **dear** (smiles) friend";
        var speak = SonioxSpeechText.NormalizeForSpeak(raw);
        Assert.Contains("[softly]", speak);
        Assert.DoesNotContain("**", speak);
        Assert.DoesNotContain("(smiles)", speak);
        Assert.Contains("dear", speak);
    }
}
