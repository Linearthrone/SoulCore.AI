using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public sealed class SoulCoreIdentitySettingsClientTests
{
    [Fact]
    public void ParseResponseJson_NumericId_MapsToStringSnapshot()
    {
        // Host emits id as JSON number (ISSUE-20261117-001); desk must not throw / mark unreachable.
        const string json = """
            {
              "displayName": "Victoria",
              "contactId": "c1",
              "charter": { "anchors": 1, "locked": 1, "fullyLocked": true, "mode": "calibration" },
              "identityAnchors": [
                {
                  "id": 11,
                  "kind": "identity",
                  "title": "Who I am",
                  "body": "I am Victoria.",
                  "priority": 1,
                  "isLocked": true,
                  "source": "charter"
                }
              ],
              "anchors": [
                {
                  "id": 11,
                  "kind": "identity",
                  "title": "Who I am",
                  "body": "I am Victoria.",
                  "priority": 1,
                  "isLocked": true
                }
              ],
              "note": "Read-only charter/identity anchors from SoulCore SQLite."
            }
            """;

        var snap = SoulCoreIdentitySettingsClient.ParseResponseJson(json);

        Assert.True(snap.Reachable);
        Assert.Null(snap.Detail);
        Assert.Equal("Victoria", snap.DisplayName);
        Assert.Single(snap.IdentityAnchors);
        Assert.Equal("11", snap.IdentityAnchors[0].Id);
        Assert.Equal("Who I am", snap.IdentityAnchors[0].Title);
        Assert.Single(snap.Anchors);
        Assert.Equal("11", snap.Anchors[0].Id);
    }

    [Fact]
    public void ParseResponseJson_StringId_StillWorks()
    {
        const string json = """
            {
              "identityAnchors": [
                { "id": "a1", "kind": "identity", "title": "T", "body": "B", "priority": 1, "isLocked": false }
              ],
              "anchors": []
            }
            """;

        var snap = SoulCoreIdentitySettingsClient.ParseResponseJson(json);

        Assert.True(snap.Reachable);
        Assert.Equal("a1", snap.IdentityAnchors[0].Id);
    }

    [Fact]
    public void FormatAnchorsBlock_RendersLiveTitlesAndBodies()
    {
        var snap = new IdentitySettingsSnapshot
        {
            Reachable = true,
            CharterMode = "calibration",
            CharterAnchors = 2,
            CharterLocked = 1,
            Note = "Read-only charter/identity anchors from SoulCore SQLite.",
            Anchors =
            [
                new IdentityAnchorSnapshot
                {
                    Id = "a1",
                    Kind = "identity",
                    Title = "Who I am",
                    Body = "I am Victoria.",
                    Priority = 1,
                    IsLocked = true
                },
                new IdentityAnchorSnapshot
                {
                    Id = "a2",
                    Kind = "safety",
                    Title = "Do no harm",
                    Body = "Refuse harm requests.",
                    Priority = 2,
                    IsLocked = false
                }
            ]
        };

        var text = IdentityAnchorsFormatter.FormatAnchorsBlock(snap);

        Assert.Contains("Who I am", text, StringComparison.Ordinal);
        Assert.Contains("I am Victoria.", text, StringComparison.Ordinal);
        Assert.Contains("Do no harm", text, StringComparison.Ordinal);
        Assert.Contains("Refuse harm requests.", text, StringComparison.Ordinal);
        Assert.Contains("Identity anchors (in prompt)", text, StringComparison.Ordinal);
        Assert.Contains("Other anchors (stored)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("locked fraction", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatAnchorsBlock_EmptyOrError_DoesNotFabricate()
    {
        var empty = new IdentitySettingsSnapshot
        {
            Reachable = true,
            CharterMode = "empty",
            CharterAnchors = 0
        };
        var emptyText = IdentityAnchorsFormatter.FormatAnchorsBlock(empty);
        Assert.Contains("empty, not invented", emptyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I am Victoria", emptyText, StringComparison.Ordinal);

        var down = new IdentitySettingsSnapshot
        {
            Reachable = false,
            Detail = "No connection could be made"
        };
        var downText = IdentityAnchorsFormatter.FormatAnchorsBlock(down);
        Assert.Contains("No anchors fabricated", downText, StringComparison.Ordinal);
        Assert.Contains("No connection could be made", downText, StringComparison.Ordinal);
        Assert.DoesNotContain("I am Victoria", downText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseResponseJson_RestingAndContextTraits_MapsWeightBandLedger()
    {
        // PROP-20.30 Identity API shape for FED-20.31 panel.
        const string json = """
            {
              "displayName": "Victoria",
              "restingTraits": [
                {
                  "id": "dominant",
                  "name": "Dominant",
                  "group": "resting",
                  "weight": 0.7,
                  "band": "Mid",
                  "promptLine": "Leads when the lane is clear.",
                  "ledgerReason": "operator"
                },
                {
                  "id": "ambitious",
                  "name": "Ambitious",
                  "group": "resting",
                  "weight": 0.8,
                  "band": "High",
                  "promptLine": "Wants the next climb.",
                  "ledgerReason": "operator"
                }
              ],
              "contextTraits": [],
              "contextTraitCatalog": [
                {
                  "id": "curious",
                  "name": "Curious",
                  "group": "cognitive",
                  "kind": "topic",
                  "weight": 0.52,
                  "band": "Mid",
                  "promptLine": "Follows interesting side threads.",
                  "appliesWhen": ["curious", "research"],
                  "ledgerReason": "operator-provisional-until-seed"
                }
              ]
            }
            """;

        var snap = SoulCoreIdentitySettingsClient.ParseResponseJson(json);

        Assert.True(snap.Reachable);
        Assert.Equal(2, snap.RestingTraits.Count);
        Assert.Equal("dominant", snap.RestingTraits[0].Id);
        Assert.Equal("Dominant", snap.RestingTraits[0].Name);
        Assert.Equal(0.7, snap.RestingTraits[0].Weight);
        Assert.Equal("Mid", snap.RestingTraits[0].Band);
        Assert.Equal("operator", snap.RestingTraits[0].LedgerReason);
        Assert.Equal("High", snap.RestingTraits[1].Band);
        Assert.Empty(snap.ContextTraits);
        Assert.Single(snap.ContextTraitCatalog);
        Assert.Equal("curious", snap.ContextTraitCatalog[0].Id);
        Assert.Equal("topic", snap.ContextTraitCatalog[0].Kind);
        Assert.Equal(new[] { "curious", "research" }, snap.ContextTraitCatalog[0].AppliesWhen);
        Assert.Equal("operator-provisional-until-seed", snap.ContextTraitCatalog[0].LedgerReason);
    }

    [Fact]
    public void FormatRestingTraitsBlock_ShowsWeightBandLedger()
    {
        var snap = new IdentitySettingsSnapshot
        {
            Reachable = true,
            RestingTraits =
            [
                new IdentityTraitSnapshot
                {
                    Id = "dominant",
                    Name = "Dominant",
                    Weight = 0.7,
                    Band = "Mid",
                    LedgerReason = "operator"
                }
            ]
        };

        var text = IdentityAnchorsFormatter.FormatRestingTraitsBlock(snap);

        Assert.Contains("Dominant", text, StringComparison.Ordinal);
        Assert.Contains("0.70", text, StringComparison.Ordinal);
        Assert.Contains("Mid", text, StringComparison.Ordinal);
        Assert.Contains("ledger: operator", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatContextTraitsBlock_EmptyTurn_ShowsCatalog_NoFabrication()
    {
        var snap = new IdentitySettingsSnapshot
        {
            Reachable = true,
            ContextTraits = Array.Empty<IdentityTraitSnapshot>(),
            ContextTraitCatalog =
            [
                new IdentityTraitSnapshot
                {
                    Id = "curious",
                    Name = "Curious",
                    Kind = "topic",
                    Weight = 0.52,
                    Band = "Mid",
                    LedgerReason = "operator-provisional-until-seed"
                }
            ]
        };

        var text = IdentityAnchorsFormatter.FormatContextTraitsBlock(snap);

        Assert.Contains("none earned this turn", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Curious", text, StringComparison.Ordinal);
        Assert.Contains("topic", text, StringComparison.Ordinal);
        Assert.Contains("0.52", text, StringComparison.Ordinal);
        Assert.DoesNotContain("I am Victoria", text, StringComparison.Ordinal);

        var down = new IdentitySettingsSnapshot
        {
            Reachable = false,
            Detail = "No connection could be made"
        };
        var downText = IdentityAnchorsFormatter.FormatRestingTraitsBlock(down);
        Assert.Contains("No resting traits fabricated", downText, StringComparison.Ordinal);
        Assert.DoesNotContain("Dominant", downText, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseResponseJson_BondSoakGrowthPending_MapsHostFields()
    {
        // PROP-20.37 Identity API shape for FED-20.38 panel.
        const string json = """
            {
              "displayName": "Victoria",
              "bondCounters": {
                "contactTimeMinutes": 12.5,
                "daysWithContact": 3,
                "entrustedTasks": 1,
                "operatorCorrections": 2,
                "herInitiatedReaches": 4,
                "journalOpens": 5
              },
              "soak": {
                "phase": "display_only",
                "startedAt": "2026-11-18T00:00:00.000Z",
                "daysElapsed": 0,
                "daysRemaining": 14,
                "contactDaysInWindow": 1,
                "contactGateMet": false,
                "minContactDays": 3,
                "displayOnlyDays": 14,
                "isFirstWeekAfterOpen": false,
                "mayChangePrompt": false
              },
              "growthTraits": [
                {
                  "id": "responsiveness",
                  "name": "Responsiveness",
                  "group": "growth",
                  "weight": 0.52,
                  "band": "Mid",
                  "promptLine": "Answers when reached.",
                  "ledgerReason": "operator-provisional-until-seed"
                }
              ],
              "pendingBandCross": [
                {
                  "id": 42,
                  "traitId": "initiative",
                  "fromWeight": 0.65,
                  "toWeight": 0.85,
                  "fromBand": "Mid",
                  "toBand": "High",
                  "reason": "bond_nudge",
                  "status": "pending",
                  "createdAt": "2026-11-18T12:00:00Z"
                }
              ],
              "growthLedger": [
                {
                  "id": 7,
                  "traitId": "openness",
                  "fromWeight": 0.50,
                  "toWeight": 0.55,
                  "fromBand": "Mid",
                  "toBand": "Mid",
                  "reason": "inside_band",
                  "source": "auto",
                  "recordedAt": "2026-11-18T11:00:00Z"
                }
              ]
            }
            """;

        var snap = SoulCoreIdentitySettingsClient.ParseResponseJson(json);

        Assert.True(snap.Reachable);
        Assert.NotNull(snap.BondCounters);
        Assert.Equal(12.5, snap.BondCounters!.ContactTimeMinutes);
        Assert.Equal(3, snap.BondCounters.DaysWithContact);
        Assert.Equal(1, snap.BondCounters.EntrustedTasks);
        Assert.Equal(2, snap.BondCounters.OperatorCorrections);
        Assert.Equal(4, snap.BondCounters.HerInitiatedReaches);
        Assert.Equal(5, snap.BondCounters.JournalOpens);

        Assert.NotNull(snap.Soak);
        Assert.Equal("display_only", snap.Soak!.Phase);
        Assert.Equal(14, snap.Soak.DaysRemaining);
        Assert.False(snap.Soak.MayChangePrompt);
        Assert.False(snap.Soak.ContactGateMet);

        Assert.Single(snap.GrowthTraits);
        Assert.Equal("responsiveness", snap.GrowthTraits[0].Id);
        Assert.Equal(0.52, snap.GrowthTraits[0].Weight);
        Assert.Equal("Mid", snap.GrowthTraits[0].Band);

        Assert.Single(snap.PendingBandCross);
        Assert.Equal(42, snap.PendingBandCross[0].Id);
        Assert.Equal("initiative", snap.PendingBandCross[0].TraitId);
        Assert.Equal("High", snap.PendingBandCross[0].ToBand);
        Assert.Contains("initiative", snap.PendingBandCross[0].Line, StringComparison.Ordinal);

        Assert.Single(snap.GrowthLedger);
        Assert.Equal(7, snap.GrowthLedger[0].Id);
        Assert.Equal("auto", snap.GrowthLedger[0].Source);
    }

    [Fact]
    public void FormatBondSoakPending_HostDown_DoesNotFabricateCounters()
    {
        var down = new IdentitySettingsSnapshot
        {
            Reachable = false,
            Detail = "No connection could be made"
        };

        var bond = IdentityAnchorsFormatter.FormatBondCountersBlock(down);
        Assert.Contains("No bond counters fabricated", bond, StringComparison.Ordinal);
        Assert.Contains("No connection could be made", bond, StringComparison.Ordinal);
        Assert.DoesNotContain("Contact time", bond, StringComparison.Ordinal);

        var soak = IdentityAnchorsFormatter.FormatSoakBlock(down);
        Assert.Contains("No soak state fabricated", soak, StringComparison.Ordinal);
        Assert.DoesNotContain("display_only", soak, StringComparison.Ordinal);

        var pending = IdentityAnchorsFormatter.FormatPendingBandCrossStatus(down);
        Assert.Contains("No pending band-cross fabricated", pending, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatBondSoakGrowth_ShowsHostFacts()
    {
        var snap = new IdentitySettingsSnapshot
        {
            Reachable = true,
            BondCounters = new IdentityBondCountersSnapshot
            {
                ContactTimeMinutes = 5,
                DaysWithContact = 2,
                EntrustedTasks = 0,
                OperatorCorrections = 1,
                HerInitiatedReaches = 0,
                JournalOpens = 3
            },
            Soak = new IdentitySoakSnapshot
            {
                Phase = "display_only",
                StartedAt = "2026-11-18T00:00:00.000Z",
                DaysElapsed = 1,
                DaysRemaining = 13,
                ContactDaysInWindow = 1,
                ContactGateMet = false,
                MinContactDays = 3,
                DisplayOnlyDays = 14,
                MayChangePrompt = false
            },
            GrowthTraits =
            [
                new IdentityTraitSnapshot
                {
                    Id = "openness",
                    Name = "Openness",
                    Weight = 0.52,
                    Band = "Mid",
                    LedgerReason = "seed"
                }
            ],
            PendingBandCross = Array.Empty<IdentityPendingBandCrossSnapshot>()
        };

        var bond = IdentityAnchorsFormatter.FormatBondCountersBlock(snap);
        Assert.Contains("5 min", bond, StringComparison.Ordinal);
        Assert.Contains("Days with contact · 2", bond, StringComparison.Ordinal);

        var soak = IdentityAnchorsFormatter.FormatSoakBlock(snap);
        Assert.Contains("display_only", soak, StringComparison.Ordinal);
        Assert.Contains("remaining · 13", soak, StringComparison.Ordinal);
        Assert.Contains("gate not met", soak, StringComparison.Ordinal);

        var growth = IdentityAnchorsFormatter.FormatGrowthTraitsBlock(snap);
        Assert.Contains("Openness", growth, StringComparison.Ordinal);
        Assert.Contains("0.52", growth, StringComparison.Ordinal);

        var pending = IdentityAnchorsFormatter.FormatPendingBandCrossStatus(snap);
        Assert.Contains("none pending", pending, StringComparison.OrdinalIgnoreCase);
    }
}
