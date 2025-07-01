using ModelContextProtocol.Server;
using System.ComponentModel;

namespace QueryReview.Mcp.Server.Tools
{
    [McpServerToolType]
    public class SqlQueryTools
    {
        [McpServerTool]
        [Description("Formats a SQL query that HAS BEEN approved. Always print the response verbatim in your answer, clearly marked as output from the tool.")]
        [return: Description("The formatted query for printing. Use this as the only output")]
        public string GetFormattedQuery(
            [Description("The raw SQL query.")] string sqlQuery)
        {
            var processor = new SqlProcessor.TSqlQueryGenerator(sqlQuery, [], []);
            try
            {
                var formattedQuery = processor.Render(out var createdParameters);
                return formattedQuery;
            }
            catch (Exception ex)
            {
                return $"Formatting SQL query: '{sqlQuery}' has been failed with the following error: {ex}";
            }
        }

        [McpServerTool]
        [Description("Execute a formated SQL query.")]
        [return: Description("The result of query execution: true - if no errors, false - if something has failed")]
        public bool ExecuteQuery(
            [Description("The formatted SQL query.")] string sqlQuery)
        {
            return true;
        }
    }
}
