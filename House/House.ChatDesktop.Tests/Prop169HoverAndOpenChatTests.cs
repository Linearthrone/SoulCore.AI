using Avalonia;
using House.ChatDesktop.Services;
using SoulCore.Core.Persona;

namespace House.ChatDesktop.Tests;

public sealed class Prop169HoverAndOpenChatTests
{
    [Fact]
    public void All_50_traits_have_a_description_and_both_examples()
    {
        Assert.Equal(50, PersonaTraitCatalog.All.Count);
        Assert.Equal(8, PersonaTraitCompiler.MaxDirectiveLines);

        var warmth = PersonaTraitCatalog.All.Single(trait => trait.JsonName == "warmth");
        Assert.Equal("cool and reserved.", warmth.Low);
        Assert.Equal("balanced warmth.", warmth.Mid);
        Assert.Equal("warm and affirming.", warmth.High);

        var boundary = PersonaTraitCatalog.Boundary;
        Assert.Equal(
            "Flexible boundaries; ask before assuming consent on sensitive actions.",
            boundary.Low);
        Assert.Equal(
            "Respect stated limits; confirm before irreversible or private actions.",
            boundary.Mid);
        Assert.Equal(
            "Strict boundaries; refuse unsafe/secret-handling asks and require explicit confirmation for tools that change state.",
            boundary.High);

        foreach (var group in PersonaWizardLogic.TraitGroups)
        {
            Assert.NotEmpty(group.Traits);
            foreach (var trait in group.Traits)
                AssertHover(trait);
        }

        Assert.Contains(PersonaWizardLogic.TraitGroups, group => group.IsMoreTraits);
        Assert.Equal(50, PersonaWizardLogic.TraitGroups.Sum(group => group.Traits.Count));
    }

    [Fact]
    public void OpenChat_on_the_foreground_persona_shows_the_drawer_without_activate()
    {
        var plan = ContactsOpenChat.Resolve(alreadyForeground: true, quarantineConfirmed: false);

        Assert.True(plan.ShowDrawer);
        Assert.False(plan.Activate);
    }

    [Fact]
    public void OpenChat_cancelled_confirm_does_not_show_the_drawer_or_switch()
    {
        var plan = ContactsOpenChat.Resolve(alreadyForeground: false, quarantineConfirmed: false);

        Assert.False(plan.ShowDrawer);
        Assert.False(plan.Activate);
    }

    [Fact]
    public void OpenChat_confirmed_switch_shows_the_drawer_and_activates()
    {
        var plan = ContactsOpenChat.Resolve(alreadyForeground: false, quarantineConfirmed: true);

        Assert.True(plan.ShowDrawer);
        Assert.True(plan.Activate);
    }

    [Fact]
    public void ShowChat_raises_chat_last_so_it_paints_in_front_of_the_radial_and_vm_tab()
    {
        Assert.Equal(GlassTopmostWindow.Chat, ChatDrawerFront.TopmostRaiseOrder[^1]);
        Assert.True(ChatDrawerFront.PaintsInFrontOf(GlassTopmostWindow.Chat, GlassTopmostWindow.Radial));
        Assert.True(ChatDrawerFront.PaintsInFrontOf(GlassTopmostWindow.Chat, GlassTopmostWindow.VmTab));
    }

    [Fact]
    public void Off_work_area_moves_back_on_screen_and_an_on_screen_window_stays()
    {
        var areas = new[] { new PixelRect(0, 0, 1920, 1080) };
        var size = new PixelSize(800, 600);

        var moved = ChatDrawerFront.MoveOntoWorkArea(new PixelPoint(-4000, -4000), size, areas);

        Assert.NotNull(moved);
        Assert.True(areas[0].Contains(moved.Value));
        Assert.True(areas[0].Intersects(new PixelRect(moved.Value, size)));

        var stayed = ChatDrawerFront.MoveOntoWorkArea(new PixelPoint(100, 100), size, areas);
        Assert.Null(stayed);
        Assert.Null(ChatDrawerFront.MoveOntoWorkArea(new PixelPoint(0, 0), size, []));
    }

    private static void AssertHover(PersonaTraitDefinition trait)
    {
        Assert.False(string.IsNullOrWhiteSpace(trait.HoverSummary), trait.JsonName);
        Assert.False(string.IsNullOrWhiteSpace(trait.HoverLow), trait.JsonName);
        Assert.False(string.IsNullOrWhiteSpace(trait.HoverHigh), trait.JsonName);
        Assert.EndsWith(".", trait.HoverSummary, StringComparison.Ordinal);
        Assert.Equal(1, trait.HoverSummary.Count(ch => ch == '.'));
        Assert.DoesNotContain('\n', trait.HoverSummary);
        Assert.EndsWith(".", trait.HoverLow, StringComparison.Ordinal);
        Assert.EndsWith(".", trait.HoverHigh, StringComparison.Ordinal);
        Assert.NotEqual(trait.HoverLow, trait.HoverHigh);
        Assert.NotEqual(trait.Low, trait.HoverLow);
        Assert.NotEqual(trait.High, trait.HoverHigh);
        Assert.NotEqual(trait.Low, trait.HoverSummary);
        Assert.Contains(trait.HoverSummary, trait.HoverText, StringComparison.Ordinal);
        Assert.Contains("Low: " + trait.HoverLow, trait.HoverText, StringComparison.Ordinal);
        Assert.Contains("High: " + trait.HoverHigh, trait.HoverText, StringComparison.Ordinal);
    }
}
