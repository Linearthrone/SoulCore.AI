using System.Text.Json;
using SoulCore.Core.Persona;

namespace SoulCore.Host.Persona;

/// <summary>
/// PROP-15.19 — merge sparse PUT bodies onto an existing pack so omitted fields
/// are never replaced with C# defaults (IdentityBlurb / traits wipe hazard).
/// </summary>
public static class PersonaPackMerge
{
    /// <summary>
    /// Applies only properties present on <paramref name="patch"/> to a clone of
    /// <paramref name="existing"/>. Rejects null/non-object/empty patches.
    /// </summary>
    public static bool TryApplySparsePatch(
        PersonaPack existing,
        JsonElement patch,
        out PersonaPack merged,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(existing);
        merged = existing.Clone();
        error = null;

        if (patch.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            error = "Body required.";
            return false;
        }

        if (patch.ValueKind != JsonValueKind.Object)
        {
            error = "Body must be a JSON object.";
            return false;
        }

        var applied = 0;

        if (TryGetProperty(patch, "personaId", out var personaIdEl))
        {
            applied++;
            if (personaIdEl.ValueKind is JsonValueKind.String)
            {
                var id = personaIdEl.GetString();
                if (!string.IsNullOrWhiteSpace(id) &&
                    !string.Equals(id, existing.PersonaId, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"personaId in body ('{id}') must match path ('{existing.PersonaId}').";
                    return false;
                }
            }
            else if (personaIdEl.ValueKind is not JsonValueKind.Null)
            {
                error = "personaId must be a string when provided.";
                return false;
            }
        }

        if (TryGetProperty(patch, "displayName", out var displayNameEl))
        {
            applied++;
            if (!TryReadOptionalString(displayNameEl, "displayName", out var displayName, out error))
                return false;
            merged.DisplayName = displayName ?? string.Empty;
        }

        if (TryGetProperty(patch, "humanAddress", out var humanAddressEl))
        {
            applied++;
            if (!TryReadOptionalString(humanAddressEl, "humanAddress", out var humanAddress, out error))
                return false;
            merged.HumanAddress = humanAddress ?? string.Empty;
        }

        if (TryGetProperty(patch, "contactId", out var contactIdEl))
        {
            applied++;
            if (!TryReadOptionalString(contactIdEl, "contactId", out var contactId, out error))
                return false;
            merged.ContactId = contactId ?? string.Empty;
        }

        if (TryGetProperty(patch, "charterSeedPath", out var charterEl))
        {
            applied++;
            if (!TryReadOptionalString(charterEl, "charterSeedPath", out var charter, out error))
                return false;
            merged.CharterSeedPath = charter;
        }

        if (TryGetProperty(patch, "playwrightProfileDir", out var playwrightEl))
        {
            applied++;
            if (!TryReadOptionalString(playwrightEl, "playwrightProfileDir", out var playwright, out error))
                return false;
            merged.PlaywrightProfileDir = playwright;
        }

        if (TryGetProperty(patch, "vmWindowTitle", out var vmTitleEl))
        {
            applied++;
            if (!TryReadOptionalString(vmTitleEl, "vmWindowTitle", out var vmTitle, out error))
                return false;
            merged.VmWindowTitle = vmTitle;
        }

        if (TryGetProperty(patch, "inferenceModel", out var modelEl))
        {
            applied++;
            if (!TryReadOptionalString(modelEl, "inferenceModel", out var model, out error))
                return false;
            merged.InferenceModel = model;
        }

        if (TryGetProperty(patch, "identityBlurb", out var blurbEl))
        {
            applied++;
            if (!TryReadOptionalString(blurbEl, "identityBlurb", out var blurb, out error))
                return false;
            merged.IdentityBlurb = blurb;
        }

        if (TryGetProperty(patch, "physicalDescription", out var descriptionEl))
        {
            applied++;
            if (!TryReadOptionalString(descriptionEl, "physicalDescription", out var description, out error))
                return false;
            merged.PhysicalDescription = description ?? string.Empty;
        }

        if (TryGetProperty(patch, "toolPolicy", out var toolPolicyEl))
        {
            applied++;
            if (!TryMergeToolPolicy(merged.ToolPolicy, toolPolicyEl, out error))
                return false;
        }

        if (TryGetProperty(patch, "traits", out var traitsEl))
        {
            applied++;
            if (!TryMergeTraits(merged.Traits, traitsEl, out error))
                return false;
        }

        // updatedAtUtc is server-owned; ignore if present (still counts as a field so
        // callers sending only timestamps are rejected as empty of meaningful updates).
        if (TryGetProperty(patch, "updatedAtUtc", out _))
            applied++;

        if (applied == 0)
        {
            error = "Empty body: provide at least one field to update.";
            return false;
        }

        // Path-only / metadata-only bodies (personaId and/or updatedAtUtc) are not updates.
        var meaningful = applied;
        if (TryGetProperty(patch, "personaId", out _))
            meaningful--;
        if (TryGetProperty(patch, "updatedAtUtc", out _))
            meaningful--;
        if (meaningful <= 0)
        {
            error = "Empty body: provide at least one field to update.";
            return false;
        }

        merged.PersonaId = existing.PersonaId;
        return true;
    }

    private static bool TryMergeToolPolicy(PersonaToolPolicy target, JsonElement el, out string? error)
    {
        error = null;
        if (el.ValueKind is JsonValueKind.Null)
            return true;

        if (el.ValueKind != JsonValueKind.Object)
        {
            error = "toolPolicy must be an object when provided.";
            return false;
        }

        if (TryGetProperty(el, "allowDesktop", out var allowDesktop))
        {
            if (!TryReadBool(allowDesktop, "toolPolicy.allowDesktop", out var v, out error))
                return false;
            target.AllowDesktop = v;
        }

        if (TryGetProperty(el, "allowBrowser", out var allowBrowser))
        {
            if (!TryReadBool(allowBrowser, "toolPolicy.allowBrowser", out var v, out error))
                return false;
            target.AllowBrowser = v;
        }

        if (TryGetProperty(el, "allowEmail", out var allowEmail))
        {
            if (!TryReadBool(allowEmail, "toolPolicy.allowEmail", out var v, out error))
                return false;
            target.AllowEmail = v;
        }

        return true;
    }

    private static bool TryMergeTraits(PersonaTraitScales target, JsonElement el, out string? error)
    {
        error = null;
        if (el.ValueKind is JsonValueKind.Null)
            return true;

        if (el.ValueKind != JsonValueKind.Object)
        {
            error = "traits must be an object when provided.";
            return false;
        }

        foreach (var trait in PersonaTraitCatalog.All)
        {
            if (!TryGetProperty(el, trait.JsonName, out var prop))
                continue;
            if (!TryReadDouble(prop, "traits." + trait.JsonName, out var v, out error))
                return false;
            trait.Set(target, v);
        }

        return true;
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryReadOptionalString(
        JsonElement el,
        string fieldName,
        out string? value,
        out string? error)
    {
        error = null;
        value = null;
        if (el.ValueKind is JsonValueKind.Null)
            return true;
        if (el.ValueKind is JsonValueKind.String)
        {
            value = el.GetString();
            return true;
        }

        error = $"{fieldName} must be a string or null when provided.";
        return false;
    }

    private static bool TryReadBool(JsonElement el, string fieldName, out bool value, out string? error)
    {
        error = null;
        value = default;
        if (el.ValueKind is JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (el.ValueKind is JsonValueKind.False)
        {
            value = false;
            return true;
        }

        error = $"{fieldName} must be a boolean when provided.";
        return false;
    }

    private static bool TryReadDouble(JsonElement el, string fieldName, out double value, out string? error)
    {
        error = null;
        value = default;
        if (el.ValueKind is JsonValueKind.Number && el.TryGetDouble(out value))
            return true;

        error = $"{fieldName} must be a number when provided.";
        return false;
    }
}
