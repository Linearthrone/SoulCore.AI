using System.Text.Json;
using House.ChatDesktop.Services;
using SoulCore.Core.Persona;

namespace House.ChatDesktop.Tests;

public sealed class PersonaCatalogWizardTests
{
    [Fact]
    public void TraitGroups_follow_catalog_order_then_more_traits()
    {
        var groups = PersonaWizardLogic.TraitGroups;

        Assert.Equal(
            new[] { "Voice", "Judgment", "Thinking and drive", "With people", "With other AIs", "More traits" },
            groups.Select(g => g.Title).ToArray());
        Assert.Equal(new[] { 6, 6, 9, 6, 3, 20 }, groups.Select(g => g.Traits.Count).ToArray());
        Assert.Equal(5, groups.Count(g => !g.IsMoreTraits));
        Assert.True(groups[^1].IsMoreTraits);

        var flat = groups.SelectMany(g => g.Traits).ToArray();
        Assert.Equal(PersonaTraitCatalog.All.Count, flat.Length);
        Assert.Equal(50, flat.Length);
        for (var i = 0; i < flat.Length; i++)
        {
            Assert.Equal(PersonaTraitCatalog.All[i].JsonName, flat[i].JsonName);
            Assert.Equal(i, flat[i].Order);
        }

        var voice = groups[0];
        var more = groups[^1];
        Assert.False(PersonaWizardLogic.ShowsTraitGroup(more, opened: null));
        Assert.False(PersonaWizardLogic.ShowsTraitGroup(more, voice));
        Assert.True(PersonaWizardLogic.ShowsTraitGroup(voice, voice));
        Assert.False(PersonaWizardLogic.ShowsTraitGroup(voice, more));
        Assert.True(PersonaWizardLogic.ShowsTraitGroup(more, more));
        Assert.Equal(20, more.Traits.Count);
        Assert.DoesNotContain(more.Traits, trait => voice.Traits.Any(v => v.JsonName == trait.JsonName));
    }

    [Fact]
    public void WizardSteps_walk_template_identity_model_groups_blurb_quarantine()
    {
        var create = PersonaWizardLogic.BuildWizardSteps(isCreate: true);
        Assert.Equal(PersonaWizardStepKind.Template, create[0].Kind);
        Assert.Equal(PersonaWizardStepKind.Identity, create[1].Kind);
        Assert.Equal(PersonaWizardStepKind.Model, create[2].Kind);

        var traitSteps = create.Where(s => s.Kind == PersonaWizardStepKind.Traits).ToArray();
        Assert.Equal(6, traitSteps.Length);
        Assert.All(traitSteps.Take(5), step => Assert.False(step.Group!.IsMoreTraits));
        Assert.True(traitSteps[^1].Group!.IsMoreTraits);
        Assert.Equal(20, traitSteps[^1].Group!.Traits.Count);
        Assert.Equal(
            new[] { "Voice", "Judgment", "Thinking and drive", "With people", "With other AIs", "More traits" },
            traitSteps.Select(s => s.Group!.Title).ToArray());

        Assert.Equal(PersonaWizardStepKind.Blurb, create[^2].Kind);
        Assert.Equal(PersonaWizardStepKind.Quarantine, create[^1].Kind);

        var edit = PersonaWizardLogic.BuildWizardSteps(isCreate: false);
        Assert.DoesNotContain(edit, step => step.Kind == PersonaWizardStepKind.Template);
        Assert.Equal(PersonaWizardStepKind.Identity, edit[0].Kind);
        Assert.Equal(PersonaWizardStepKind.Quarantine, edit[^1].Kind);
        Assert.Equal(6, edit.Count(step => step.Kind == PersonaWizardStepKind.Traits));
    }

    [Fact]
    public void SaveMap_includes_every_catalog_trait_at_its_default_when_untouched()
    {
        var map = PersonaWizardLogic.BuildTraitSaveMap(new PersonaTraitScalesInfo());

        Assert.Equal(PersonaTraitCatalog.All.Count, map.Count);
        foreach (var trait in PersonaTraitCatalog.All)
        {
            Assert.True(map.ContainsKey(trait.JsonName));
            Assert.Equal(trait.Default, map[trait.JsonName]);
            Assert.True(trait.IsDefault(map[trait.JsonName]));
        }
    }

    [Fact]
    public void SavePayload_sends_all_50_and_keeps_blank_starter_formality_and_playfulness()
    {
        var template = PersonaWizardLogic.BuildTemplates(new[] { HostBlank() })[0];
        Assert.Equal(0.4, template.Traits.Formality);
        Assert.Equal(0.4, template.Traits.Playfulness);

        var write = PersonaWizardLogic.BuildCreateWrite(
            template,
            "nova",
            "Nova",
            "Friend",
            template.IdentityBlurb,
            template.Traits,
            inferenceModel: null,
            physicalDescription: "silver hair, green eyes");

        var json = SoulCorePersonaClient.BuildPersonaWriteJson(write);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var traits = root.GetProperty("traits");
        Assert.Equal(50, traits.EnumerateObject().Count());
        foreach (var trait in PersonaTraitCatalog.All)
            Assert.True(traits.TryGetProperty(trait.JsonName, out _), trait.JsonName);

        Assert.Equal(0.4, traits.GetProperty("formality").GetDouble());
        Assert.Equal(0.4, traits.GetProperty("playfulness").GetDouble());

        var untouched = PersonaTraitCatalog.All.Single(t => t.JsonName == "metaphorUse");
        Assert.Equal(untouched.Default, traits.GetProperty(untouched.JsonName).GetDouble());
        Assert.NotEqual(0.5, untouched.Default);

        Assert.Equal("silver hair, green eyes", root.GetProperty("physicalDescription").GetString());
        Assert.Equal(template.IdentityBlurb, root.GetProperty("identityBlurb").GetString());
        Assert.NotEqual(
            root.GetProperty("physicalDescription").GetString(),
            root.GetProperty("identityBlurb").GetString());
    }

    [Fact]
    public void LoadTraits_missing_key_uses_catalog_default_and_keeps_stored_values()
    {
        var loaded = PersonaWizardLogic.LoadTraits("""{"warmth":0.2,"formality":0.4}""");

        Assert.Equal(0.2, loaded.Warmth);
        Assert.Equal(0.4, loaded.Formality);
        foreach (var trait in PersonaTraitCatalog.All)
        {
            if (trait.JsonName is "warmth" or "formality")
                continue;
            Assert.True(trait.IsDefault(loaded.Read(trait)), trait.JsonName);
        }

        var adaptability = PersonaTraitCatalog.All.Single(t => t.JsonName == "adaptability");
        var full = new PersonaTraitScalesInfo();
        full.Write(adaptability, 0.12);
        var roundTrip = PersonaWizardLogic.LoadTraits(JsonSerializer.Serialize(
            PersonaWizardLogic.BuildTraitSaveMap(full)));
        Assert.Equal(0.12, roundTrip.Read(adaptability));
        Assert.Equal(0.4, loaded.Formality);
    }

    [Fact]
    public void EditWrite_reloads_every_trait_and_the_description()
    {
        var stored = PersonaWizardLogic.LoadTraits("""{"warmth":0.81,"adaptability":0.12}""");
        var existing = new PersonaPackInfo
        {
            PersonaId = "nova",
            DisplayName = "Nova",
            IdentityBlurb = "charter ## LIMITS stay",
            PhysicalDescription = "saved face",
            Traits = stored,
            ToolPolicy = new PersonaToolPolicyInfo
            {
                AllowDesktop = false,
                AllowBrowser = true,
                AllowEmail = false
            }
        };

        var write = PersonaWizardLogic.BuildUpdateWrite(
            existing,
            new PersonaPackWrite
            {
                DisplayName = "Nova",
                HumanAddress = "Friend",
                IdentityBlurb = existing.IdentityBlurb,
                PhysicalDescription = existing.PhysicalDescription,
                Traits = stored
            });

        Assert.Equal("saved face", write.PhysicalDescription);
        Assert.Equal("charter ## LIMITS stay", write.IdentityBlurb);
        Assert.False(write.ToolPolicy!.AllowDesktop);
        Assert.True(write.ToolPolicy.AllowBrowser);
        Assert.False(write.ToolPolicy.AllowEmail);

        var map = PersonaWizardLogic.BuildTraitSaveMap(write.Traits);
        Assert.Equal(50, map.Count);
        Assert.Equal(0.81, map["warmth"]);
        Assert.Equal(0.12, map["adaptability"]);
        var caution = PersonaTraitCatalog.All.Single(t => t.JsonName == "caution");
        Assert.Equal(caution.Default, map[caution.JsonName]);
    }

    [Fact]
    public void DescriptionBox_caps_at_1000_and_draft_writes_only_that_box()
    {
        Assert.Equal(1000, PersonaPack.PhysicalDescriptionMaxLength);
        Assert.Equal(1000, PersonaWizardLogic.NormalizePhysicalDescription(new string('x', 1400)).Length);
        Assert.Equal("silver hair", PersonaWizardLogic.NormalizePhysicalDescription("  silver hair  "));

        var current = new DescriptionDraftFields
        {
            IdentityBlurb = "charter ## LIMITS stay",
            PhysicalDescription = "old face",
            AllowDesktop = false,
            AllowBrowser = true,
            AllowEmail = false
        };

        var failed = PersonaWizardLogic.ApplyDescriptionDraft(current, draftSucceeded: false, draftText: "should not apply");
        Assert.Equal("old face", failed.PhysicalDescription);
        Assert.Equal(current.IdentityBlurb, failed.IdentityBlurb);
        Assert.False(failed.AllowDesktop);
        Assert.True(failed.AllowBrowser);
        Assert.False(failed.AllowEmail);

        var applied = PersonaWizardLogic.ApplyDescriptionDraft(
            current,
            draftSucceeded: true,
            draftText: new string('y', 1400));
        Assert.Equal(1000, applied.PhysicalDescription.Length);
        Assert.Equal(current.IdentityBlurb, applied.IdentityBlurb);
        Assert.False(applied.AllowDesktop);
        Assert.True(applied.AllowBrowser);
        Assert.False(applied.AllowEmail);

        var blocked = PersonaWizardLogic.DecideDescriptionDraft(personaAlreadyOnHost: false);
        Assert.False(blocked.CallRoute);
        Assert.False(blocked.CreatePack);
        Assert.Contains("already be saved", blocked.Status, StringComparison.OrdinalIgnoreCase);

        var allowed = PersonaWizardLogic.DecideDescriptionDraft(personaAlreadyOnHost: true);
        Assert.True(allowed.CallRoute);
        Assert.False(allowed.CreatePack);

        Assert.Equal("{}", SoulCorePersonaClient.DescriptionDraftRequestJson);
        Assert.DoesNotContain("identityBlurb", SoulCorePersonaClient.DescriptionDraftRequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("toolPolicy", SoulCorePersonaClient.DescriptionDraftRequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("traits", SoulCorePersonaClient.DescriptionDraftRequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIMITS", SoulCorePersonaClient.DescriptionDraftRequestJson, StringComparison.Ordinal);
        Assert.EndsWith(
            "/api/personas/nova/description-draft",
            SoulCorePersonaClient.DescriptionDraftUri("nova").AbsolutePath,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Contacts_show_saved_physical_description_under_the_name()
    {
        Assert.Equal("", PersonaWizardLogic.ContactDescriptionText(null));
        Assert.Equal("", PersonaWizardLogic.ContactDescriptionText("   "));
        Assert.Equal("silver hair", PersonaWizardLogic.ContactDescriptionText("  silver hair  "));
    }

    private static PersonaPackInfo HostBlank() => new()
    {
        PersonaId = PersonaWizardLogic.BlankTemplateId,
        DisplayName = "Blank",
        HumanAddress = "Friend",
        IdentityBlurb =
            "You are a newly created companion with no fixed biography yet. Stay helpful and clear.",
        Traits = new PersonaTraitScalesInfo
        {
            Warmth = 0.5,
            Directness = 0.5,
            Formality = 0.4,
            Playfulness = 0.4,
            BoundaryStrictness = 0.5,
            RecallBias = 0.5
        }
    };
}
