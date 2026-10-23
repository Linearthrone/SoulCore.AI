using System.Net.Http;
using System.Text;

namespace House.ChatDesktop.Services;

/// <summary>
/// Best-effort clear of the process-wide CUA lamp. Exit, crash, and update-restart call this.
/// One attempt per process so a dead Host cannot stall shutdown twice.
/// </summary>
public static class CuaLampClear
{
    private static int _attempted;

    public static void TryClearNow()
    {
        if (Interlocked.Exchange(ref _attempted, 1) == 1)
            return;
        if (!ConnectionDefaults.IsLocalLoopback(ConnectionDefaults.Host))
            return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(800) };
            using var request = new HttpRequestMessage(HttpMethod.Post, SoulCoreToolsSettingsClient.SettingsUri)
            {
                Content = new StringContent(
                    "{\"allowComputerControl\":false}",
                    Encoding.UTF8,
                    "application/json")
            };
            using var _ = http.Send(request);
        }
        catch
        {
            // The lamp clear must not block process death.
        }
    }
}
