using House.ChatDesktop.Services;
using Xunit;

namespace House.ChatDesktop.Tests;

public class ActiveModelLabelTests
{
    [Fact]
    public void ActiveModelLabel_ShowsTheLiveChatModel()
    {
        var snap = new SoulCoreHealthSnapshot
        {
            InferenceEnabled = true,
            InferenceModel = "gemma4:latest"
        };

        Assert.Equal("gemma4:latest", snap.ActiveModelLabel());
    }

    [Fact]
    public void ActiveModelLabel_MarksCloudModels()
    {
        var snap = new SoulCoreHealthSnapshot
        {
            InferenceEnabled = true,
            InferenceModel = "deepseek-v4.1-flash:cloud",
            InferenceCloud = true
        };

        Assert.Equal("deepseek-v4.1-flash:cloud · cloud", snap.ActiveModelLabel());
    }

    [Fact]
    public void ActiveModelLabel_SaysDisabledWhenInferenceIsOff()
    {
        var snap = new SoulCoreHealthSnapshot { InferenceEnabled = false, InferenceModel = "gemma4:latest" };
        Assert.Equal("disabled", snap.ActiveModelLabel());
    }
}
