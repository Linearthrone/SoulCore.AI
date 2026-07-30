namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Shared gate refusal messages for desktop tools (BED-135). Exact wording is
/// part of the acceptance contract so the model can ask the user to opt in.
/// </summary>
public static class DesktopToolGate
{
    /// <summary>
    /// Returned when a control tool (click/type/key) runs with the session gate closed.
    /// </summary>
    public const string ControlRequiresAuthorization =
        "desktop control requires user authorization — enable AllowComputerControl in Settings → Tools & Access";

    /// <summary>
    /// Returned when a capture/window tool runs with <c>AllowDesktopCapture=false</c>.
    /// </summary>
    public const string CaptureDisabled =
        "desktop capture requires AllowDesktopCapture — enable it in Settings → Tools & Access";

    public static ToolResult RefuseControl()
        => new(Success: false, Content: ControlRequiresAuthorization, Data: null);

    public static ToolResult RefuseCapture()
        => new(Success: false, Content: CaptureDisabled, Data: null);

    public static ToolResult FromBackend(DesktopOpResult result)
        => new(result.Success, result.Content, result.Data);
}
