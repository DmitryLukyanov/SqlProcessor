namespace QueryReview.Mcp.Server.Configuration
{
    public record McpServerSettings(string Endpoint) { public static string SettingsKey = "McpServer"; }
}
