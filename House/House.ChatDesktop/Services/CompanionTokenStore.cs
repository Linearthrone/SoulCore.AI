namespace House.ChatDesktop.Services;

/// <summary>
/// Local companion token for Presence when SoulCore/.env is not on the BaseDirectory path
/// (Velopack Setup.exe installs). Never log the value.
/// </summary>
public static class CompanionTokenStore
{
    public static string StorePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HouseVictoria",
            "companion-api-token.txt");

    public static bool HasSavedToken()
    {
        var t = Read();
        return !string.IsNullOrWhiteSpace(t);
    }

    public static string? Read()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path))
                return null;
            var raw = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string? token)
    {
        var dir = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (string.IsNullOrWhiteSpace(token))
        {
            Clear();
            return;
        }

        // A previous Save marked the file Hidden; Windows refuses to overwrite a hidden file.
        try
        {
            if (File.Exists(StorePath))
                File.SetAttributes(StorePath, FileAttributes.Normal);
        }
        catch
        {
            // ignore — the write below reports a real failure
        }

        File.WriteAllText(StorePath, token.Trim());
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var info = new FileInfo(StorePath);
                // Best-effort: hide from casual directory browsing.
                info.Attributes |= FileAttributes.Hidden;
            }
        }
        catch
        {
            // ignore
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(StorePath))
                File.Delete(StorePath);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>Safe for UI — never includes the secret.</summary>
    public static string Describe()
    {
        var t = Read();
        return t is null
            ? "settingsToken=false"
            : $"settingsToken=true tokenLen={t.Length}";
    }
}
