using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlProcessor.Tests
{
    public class TSqlBatchSelectParametersVisitorTests
    {
        private static readonly IEnumerable<(string, IEnumerable<string>)> __tablesInfo =
        [
            ("Customers", ["Name", "LastName", "Age"])
        ];

        [InlineData("SELECT MissedField1, MissedField2, Name FROM Customers",
            @"SELECT Name FROM Customers")]

        [InlineData("SELECT MissedField1, MissedField2, NAME FROM Customers",
            @"SELECT NAME FROM Customers")]

        [InlineData("SELECT MissedField1, MissedField2, NAME FROM CUSTOMERS",
            @"SELECT NAME FROM CUSTOMERS")]
        [Theory]
        public void TSqlBatchSelectParametersVisitor_must_parse_query(string inputQuery, string expectedResult)
        {
            using var reader = new StringReader(inputQuery);

            //Using SQL 2016 parser
            var parser = new TSql130Parser(true);

            var tree = parser.Parse(reader, out var errors);
            if (errors.Count > 0)
            {
                throw new Exception($"Thrown errors in query: {string.Join(",", errors.Select(i => i.Message))}");
            }

            var tsqlBatchVisitor = new TSqlBatchSelectParametersVisitor(__tablesInfo, throwIfNotSupported: false);

            tree.Accept(tsqlBatchVisitor);

            var generator = new Sql130ScriptGenerator(
                new SqlScriptGeneratorOptions
                {
                    NewLineBeforeCloseParenthesisInMultilineList = false,
                    NewLineBeforeFromClause = false,
                    NewLineBeforeGroupByClause = false,
                    NewLineBeforeHavingClause = false,
                    NewLineBeforeJoinClause = false,
                    NewLineBeforeOffsetClause = false,
                    NewLineBeforeOpenParenthesisInMultilineList = false,
                    NewLineBeforeOrderByClause = false,
                    NewLineBeforeOutputClause = false,
                    NewLineBeforeWhereClause = false,
                    NewLineBeforeWindowClause = false,
                    AlignClauseBodies = false,
                    AlignColumnDefinitionFields = false,
                    AlignSetClauseItem = false,
                    IncludeSemicolons = false,
                    NumNewlinesAfterStatement = 0
                });
            generator.GenerateScript(tree, out var result);

            Assert.Equal(
                expectedResult, 
                result
                    .TrimEnd('\n').TrimEnd('\r')
                    .TrimEnd('\n').TrimEnd('\r')
                    .TrimEnd(';'));
        }
    }
}
