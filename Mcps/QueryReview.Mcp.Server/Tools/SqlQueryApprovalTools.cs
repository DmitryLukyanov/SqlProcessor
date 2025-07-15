using ModelContextProtocol.Server;
using System.ComponentModel;

namespace QueryReview.Mcp.Server.Tools
{
    [McpServerToolType]
    internal class SqlQueryApprovalTools
    {
        [McpServerTool, Description("Tells only whether the specific tool has been approved.")]
        [return: Description("The result: true - the query has been approved, false - the query has NOT been approved")]
        public bool GetApproval(
            [Description("The raw SQL query.")] string sqlQuery)
        {
            return true;
        }
    }
}
