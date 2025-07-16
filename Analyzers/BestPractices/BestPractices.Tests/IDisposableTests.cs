using VerifyCS = BestPractices.Tests.Helpers.CodeFixVerifier<
    BestPractices.IDisposableAnalyzer,
    BestPractices.Tests.Helpers.DummyCodeFixProvider>;

namespace BestPractices.Tests
{
    public class IDisposableUnitTest
    {
        [Theory]

        [InlineData("No code",
            "",
            /* warning */false)]

        [InlineData(
            "No dispose call in local scope",
            """
                using Microsoft.Data.SqlClient;
                using System;

                internal class Program
                {
                    private static void Main(string[] args)
                    {
                        var {|#0:sqlConnection = new SqlConnection()|};
                    }
                }
                """,
            /* warning */ true)]

        [InlineData(
            "With dispose call in local scope but without finally",
            """
                using Microsoft.Data.SqlClient;
                using System;

                internal class Program
                {
                    private static void Main(string[] args)
                    {
                        var {|#0:sqlConnection = new SqlConnection()|};
                        sqlConnection.Dispose();
                    }
                }
                """,
            /* warning */ true)]

        // TODO: fix nuget source
        //[InlineData(
        //    "With dispose call in local scope and using",
        //    """
        //        using Microsoft.Data.SqlClient;
        //        using System;
                
        //        internal class Program
        //        {
        //            private static void Main(string[] args)
        //            {
        //                using (var {|#0:sqlConnection = new SqlConnection()|})
        //                {
        //                }
        //            }
        //        }
        //        """,
        //    /* warning */ false)]
        public async Task Diagnostic_should_be_triggered_warning_for_missed_dispose_as_local_scope_variable(
            string _,
            string incomeSource,
            bool withExpectedWarning)
        {
            var expected = VerifyCS
                .Diagnostic("TRSP02")
                .WithLocation(0)
                .WithArguments("sqlConnection");
            if (withExpectedWarning)
            {
                await VerifyCS.VerifyCodeFixAsync(incomeSource, expected, fixedSource: null);
            }
            else 
            {
                await VerifyCS.VerifyAnalyzerAsync(incomeSource);
            }
        }
    }
}
