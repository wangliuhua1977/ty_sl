namespace TylinkInspection.Core.Models;

public static class AiAlertWorkflowStatus
{
    public const string PendingConfirm = "\u5f85\u786e\u8ba4";
    public const string Confirmed = "\u5df2\u786e\u8ba4";
    public const string Ignored = "\u5df2\u5ffd\u7565";
    public const string Dispatched = "\u5df2\u6d3e\u5355";
    public const string Recovered = "\u5df2\u6062\u590d";
}
