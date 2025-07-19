using VerifyCS = QueryReview.Test.Helpers.CodeFixVerifier<
    QueryReview.QueryReviewAnalyzer,
    QueryReview.QueryReviewCodeFixProvider>;

namespace QueryReviewAnalyzer.Tests
{
    public class QueryReviewUnitTest
    {
        [Fact]
        public async Task No_diagnostics_expected_to_show_up()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Diagnostic_and_CodeFix_both_triggered_and_checked_for()
        {
            var incomeSource = """
                using Microsoft.Data.SqlClient;
                using System;

                internal class Program
                {
                    private static void Main(string[] args)
                    {
                        using var sqlConnection = new SqlConnection();
                        sqlConnection.Open();
                        using var command = sqlConnection.CreateCommand();
                        command.CommandText = {|#0:"SELECT UnexistedField, Name FROM CustomersTable"|};
                        var result = command.ExecuteScalar();
                        Console.WriteLine(result);
                    }
                }
                """;

            var fixedTest = """
                using Microsoft.Data.SqlClient;
                using System;
                
                internal class Program
                {
                    private static void Main(string[] args)
                    {
                        using var sqlConnection = new SqlConnection();
                        sqlConnection.Open();
                        using var command = sqlConnection.CreateCommand();
                        command.CommandText = "SELECT Name FROM CustomersTable";
                        var result = command.ExecuteScalar();
                        Console.WriteLine(result);
                    }
                }
                """;

            var expected = VerifyCS
                .Diagnostic("TRSP01")
                .WithLocation(0)
                .WithMessage("The CommandText SQL query contains unexisted fields; review the embedded SQL");
            await VerifyCS.VerifyCodeFixAsync(incomeSource, expected, fixedTest);
        }
    }
}
