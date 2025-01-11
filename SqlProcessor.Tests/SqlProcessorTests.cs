using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text.Json;

namespace SqlProcessor.Tests
{
    public class SqlProcessorTests
    {
        private static readonly IEnumerable<(string, string)> __requiredTablesDefinition =
        [
            ("RequireWHERETable", "MandatoryId")
        ];

        private static readonly IEnumerable<RestrictedQuery> __restrictedQueries = Enum.GetValues<RestrictedQuery>();

        #region Common cases

        [InlineData(
            // input
            "select top 10 * from customers",
            // expected
            $@"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK);

")]

        [InlineData(
            // input
            "select top 10 * from customers where 1=1 and 2=3 and Field=3",
            // expected
            $@"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  1 = 1
       AND 2 = 3
       AND Field = @n_Field_0;

")]

        [InlineData(
            // input
            "select top 10 * from customers where 3 = Field",
            // expected
            $@"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  @n_Field_0 = Field;

")]

        [InlineData(
            // input
            "select top 10 * from customers c where 3 = c.Field",
            // expected
            $@"SELECT TOP @n_TOP_0 *
FROM   customers AS c WITH (NOLOCK)
WHERE  @n_c_Field_0 = c.Field;

")]

        [InlineData(
            // input
            "select top 10 PERCENT * from customers WITH (NOLOCK)",
            // expected
            $@"SELECT TOP @n_TOP_0 PERCENT *
FROM   customers WITH (NOLOCK);

")]

        [InlineData(
            // input
            "select top 10 c.* from customers c where c.Test = 1 AND c.Test2 = 2 AND 3 = c.Test3",
            // expected
            @$"SELECT TOP @n_TOP_0 c.*
FROM   customers AS c WITH (NOLOCK)
WHERE  c.Test = @n_c_Test_0
       AND c.Test2 = @n_c_Test2_0
       AND @n_c_Test3_0 = c.Test3;

")]

        [InlineData(
            // input
            "select top 10 c.* from customers c where ISNULL(c.Test, 1) = ISNULL(c.Test2,1)",
            // expected
            @$"SELECT TOP @n_TOP_0 c.*
FROM   customers AS c WITH (NOLOCK)
WHERE  ISNULL(c.Test, 1) = ISNULL(c.Test2, 1);

")]

        [InlineData(
            // input
            "select top 10 c.* from customers c where ISNULL(1, 1) = ISNULL(1,1)",
            // expected
            @$"SELECT TOP @n_TOP_0 c.*
FROM   customers AS c WITH (NOLOCK)
WHERE  ISNULL(1, 1) = ISNULL(1, 1);

")]

        [InlineData(
            // input
            "select top 10 c.* from customers c where c.Test <> 1 AND c.Test <> 2 AND 3 = c.Test",
            // expected
            @$"SELECT TOP @n_TOP_0 c.*
FROM   customers AS c WITH (NOLOCK)
WHERE  c.Test <> @n_c_Test_0
       AND c.Test <> @n_c_Test_1
       AND @n_c_Test_2 = c.Test;

")]

        [InlineData(
            // input
            "select top 10 * from customers where Test = 1 AND Test2 = 2",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  Test = @n_Test_0
       AND Test2 = @n_Test2_0;

")]

        [InlineData(
            // input
            "select top 10 * from customers where Test = 1 AND Not Test2 = 2",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  Test = @n_Test_0
       AND NOT Test2 = @n_Test2_0;

")]

        [InlineData(
            // input
            "select top 10 * from customers where (Test = 1) AND (Not Test2 = 2)",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  (Test = @n_Test_0)
       AND (NOT Test2 = @n_Test2_0);

")]

        [InlineData(
            // input
            "select top 10 * from customers where Test = 1 OR Test2 = 2",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  Test = @n_Test_0
       OR Test2 = @n_Test2_0;

")]

        [InlineData(
            // input
            "select top 10 * from customers where Test between 1 and 10",
            // no changes expected
            $@"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK)
WHERE  Test BETWEEN @n_Test_0 AND @n_Test_1;

")]

        [InlineData(
            // input
            "select top  10 * from customers order by id",
            // expected
            @$"SELECT   TOP @n_TOP_0 *
FROM     customers WITH (NOLOCK)
ORDER BY id;

")]

        [InlineData(
            // input
            "select top  10     * from customers",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   customers WITH (NOLOCK);

")]

        [InlineData(
            // input
            "select count(1) from customers group by customerId having count(customerId) > 5",
            // expected
            $@"SELECT   count(1)
FROM     customers WITH (NOLOCK)
GROUP BY customerId
HAVING   count(customerId) > @n_count_customerId_0;

")]

        [InlineData(
            // input
            "select count(1) from customers c group by c.customerId having count(c.customerId) > 5",
            // expected
            @$"SELECT   count(1)
FROM     customers AS c WITH (NOLOCK)
GROUP BY c.customerId
HAVING   count(c.customerId) > @n_count_c_customerId_0;

")]

        [InlineData(
            // input
            "select count(1) from customers group by customerId having count(customerId) > 5 AND max(customerId) > 2",
            // expected
            $@"SELECT   count(1)
FROM     customers WITH (NOLOCK)
GROUP BY customerId
HAVING   count(customerId) > @n_count_customerId_0
         AND max(customerId) > @n_max_customerId_0;

")]
        #endregion


        [InlineData(
            // input
            "select* from customers c inner join clients cl on c.Id=cl.CustomerId",
            // no changes expected
            $@"SELECT *
FROM   customers AS c WITH (NOLOCK)
       INNER JOIN
       clients AS cl WITH (NOLOCK)
       ON c.Id = cl.CustomerId;

")]

        [InlineData(
            // input
            "select top 10 * from customers c inner join clients cc on c.Id=cc.Id AND c.Id=5 where c.Test = 1 OR cc.Test2 = 2",
            // no changes expected
            $@"SELECT TOP @n_TOP_0 *
FROM   customers AS c WITH (NOLOCK)
       INNER JOIN
       clients AS cc WITH (NOLOCK)
       ON c.Id = cc.Id
          AND c.Id = @n_c_Id_0
WHERE  c.Test = @n_c_Test_0
       OR cc.Test2 = @n_cc_Test2_0;

")]

        [InlineData(
            // input
            "select top 10 * from (select top 20 * from clients) as t",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   (SELECT TOP @n_TOP_1 *
        FROM   clients WITH (NOLOCK)) AS t;

")]

        [InlineData(
            // input
            "select top  10 * from customers order by id; select top  10 * from clients order by id; ",
            // expected
            @$"SELECT   TOP @n_TOP_0 *
FROM     customers WITH (NOLOCK)
ORDER BY id;

SELECT   TOP @n_TOP_1 *
FROM     clients WITH (NOLOCK)
ORDER BY id;

")]

        [InlineData(
            // input
            "select count(1) from customers where test<>(select 1 from clients where innerFiled=2)",
            // expected
            @$"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test <> (SELECT 1
                FROM   clients WITH (NOLOCK)
                WHERE  innerFiled = @n_innerFiled_0);

")]

        [InlineData(
            // input
            "select count(1) from customers where test=(select 1 from clients where innerFiled=2)",
            // expected
            $@"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test = (SELECT 1
               FROM   clients WITH (NOLOCK)
               WHERE  innerFiled = @n_innerFiled_0);

")]

        [InlineData(
            // input
            "select count(1) from customers where test>=(select 1 from clients where innerFiled=2)",
            // expected
            @$"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test >= (SELECT 1
                FROM   clients WITH (NOLOCK)
                WHERE  innerFiled = @n_innerFiled_0);

")]

        [InlineData(
            // input
            "select count(1) from dbo.RequireWHERETable with(nolock) where MandatoryId >= 1",
            // expected
            @$"SELECT count(1)
FROM   dbo.RequireWHERETable WITH (NOLOCK)
WHERE  MandatoryId >= @n_MandatoryId_0;

")]

        [InlineData(
            // input
            "select MandatoryId from dbo.RequireWHERETable with(nolock) where anothercolumn = 1",
            // expected
            @$"SELECT MandatoryId
FROM   dbo.RequireWHERETable WITH (NOLOCK)
WHERE  dbo.RequireWHERETable.MandatoryId = @s_dbo_RequireWHERETable_MandatoryId_0
       AND anothercolumn = @n_anothercolumn_0;

")]

        [InlineData(
            // input
            "select MandatoryId from [dbo].[RequireWHERETable] with(nolock) where anothercolumn = 1",
            // expected
            @$"SELECT MandatoryId
FROM   [dbo].[RequireWHERETable] WITH (NOLOCK)
WHERE  [dbo].[RequireWHERETable].MandatoryId = @s_dbo_RequireWHERETable_MandatoryId_0
       AND anothercolumn = @n_anothercolumn_0;

")]

        [InlineData(
            // input
            "select * from RequireWHERETable w",
            // expected
            @$"SELECT *
FROM   RequireWHERETable AS w WITH (NOLOCK)
WHERE  w.MandatoryId = @s_w_MandatoryId_0;

")]

        [InlineData(
            // input
            "select count(1) from RequireWHERETable where RequireWHERETable.MandatoryId >= 1",
            // expected
            @$"SELECT count(1)
FROM   RequireWHERETable WITH (NOLOCK)
WHERE  RequireWHERETable.MandatoryId >= @n_RequireWHERETable_MandatoryId_0;

")]

        [InlineData(
            // input
            "select count(1) from RequireWHERETable with(nolock) where MandatoryId >=(select 1 from clients where innerFiled=2)",
            // expected
            @$"SELECT count(1)
FROM   RequireWHERETable WITH (NOLOCK)
WHERE  MandatoryId >= (SELECT 1
                       FROM   clients WITH (NOLOCK)
                       WHERE  innerFiled = @n_innerFiled_0);

")]

        [InlineData(
            // input
            "select count(1) from RequireWHERETable with(nolock) where MandatoryId >= 1",
            // expected
            @$"SELECT count(1)
FROM   RequireWHERETable WITH (NOLOCK)
WHERE  MandatoryId >= @n_MandatoryId_0;

")]

        [InlineData(
            // input
            "select count(1) from RequireWHERETable with(nolock) group by employeeId having count(employeeId) > 5",
            // expected
            @$"SELECT   count(1)
FROM     RequireWHERETable WITH (NOLOCK)
WHERE    RequireWHERETable.MandatoryId = @s_RequireWHERETable_MandatoryId_0
GROUP BY employeeId
HAVING   count(employeeId) > @n_count_employeeId_0;

")]

        [InlineData(
            // input
            "select top 10 w.* from RequireWHERETable",
            // expected
            $@"SELECT TOP @n_TOP_0 w.*
FROM   RequireWHERETable WITH (NOLOCK)
WHERE  RequireWHERETable.MandatoryId = @s_RequireWHERETable_MandatoryId_0;

")]

        [InlineData(
            // input
            "select top 10 w.* from [dbo].[RequireWHERETable] w",
            // expected
            @$"SELECT TOP @n_TOP_0 w.*
FROM   [dbo].[RequireWHERETable] AS w WITH (NOLOCK)
WHERE  w.MandatoryId = @s_w_MandatoryId_0;

")]

        [InlineData(
            // input
            "select * from [dbo].[RequireWHERETable]",

            // expected
            @$"SELECT *
FROM   [dbo].[RequireWHERETable] WITH (NOLOCK)
WHERE  [dbo].[RequireWHERETable].MandatoryId = @s_dbo_RequireWHERETable_MandatoryId_0;

")]

        [InlineData(
            // input
            "select top 10 * from RequireWHERETable w inner join clients cc on w.Id=cc.Id",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   RequireWHERETable AS w WITH (NOLOCK)
       INNER JOIN
       clients AS cc WITH (NOLOCK)
       ON w.Id = cc.Id
WHERE  w.MandatoryId = @s_w_MandatoryId_0;

")]

        [InlineData(
            // input
            "select MandatoryId from RequireWHERETable w",
            // expected
            @$"SELECT MandatoryId
FROM   RequireWHERETable AS w WITH (NOLOCK)
WHERE  w.MandatoryId = @s_w_MandatoryId_0;

")]

        [InlineData(
            // input
            "select top 10 * from RequireWHERETable w inner join clients cc on w.MandatoryId=cc.Id where w.MandatoryId = 2",
            // expected
            @$"SELECT TOP @n_TOP_0 *
FROM   RequireWHERETable AS w WITH (NOLOCK)
       INNER JOIN
       clients AS cc WITH (NOLOCK)
       ON w.MandatoryId = cc.Id
WHERE  w.MandatoryId = @n_w_MandatoryId_0;

")]

        [InlineData(
            // input
            "select count(1) from customers c where c.test>=(select 1 from clients cc where cc.innerFiled=2)",
            // expected
            @$"SELECT count(1)
FROM   customers AS c WITH (NOLOCK)
WHERE  c.test >= (SELECT 1
                  FROM   clients AS cc WITH (NOLOCK)
                  WHERE  cc.innerFiled = @n_cc_innerFiled_0);

")]

        [InlineData(
            // input
            "select count(1) from customers where test<=(select 1 from clients where innerFiled=2)",
            // expected
            $@"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test <= (SELECT 1
                FROM   clients WITH (NOLOCK)
                WHERE  innerFiled = @n_innerFiled_0);

")]

        // TODO (NOT READY)!
        [InlineData(
            // input
            "select count(1) from customers where test<=(select 1 from clients where innerFiled is not null)",
            // is null logic is a bit different then regular equaility, so this case should be a bit updated to trigger expected query
            @$"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test <= (SELECT 1
                FROM   clients WITH (NOLOCK)
                WHERE  innerFiled IS NOT NULL);

")]

        [InlineData(
            // input
            "select count(1) from customers where test<=(select 1 from anothertable where ISNULL(innerFiled, '3') = '3')",
            // expected
            @$"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test <= (SELECT 1
                FROM   anothertable WITH (NOLOCK)
                WHERE  ISNULL(innerFiled, '3') = @s_ISNULL_innerFiled_0);

")]

        [InlineData(
            // input
            "select count(1) from customers with (nolock) where test<=(select 1 from clients where ISNULL(innerFiled, '3') = '3')",
            // expected
            @$"SELECT count(1)
FROM   customers WITH (NOLOCK)
WHERE  test <= (SELECT 1
                FROM   clients WITH (NOLOCK)
                WHERE  ISNULL(innerFiled, '3') = @s_ISNULL_innerFiled_0);

")]

        [InlineData(
            // input
            "select * from customers with (ReadCommitted)",
            @$"SELECT *
FROM   customers WITH (READCOMMITTED);

")]

        [InlineData(
            // input
            "select count(1) from customers with (ReadCommitted) where test<=(select 1 from clients where ISNULL(innerFiled, '3') = '3')",
            // expected
            @$"SELECT count(1)
FROM   customers WITH (READCOMMITTED)
WHERE  test <= (SELECT 1
                FROM   clients WITH (NOLOCK)
                WHERE  ISNULL(innerFiled, '3') = @s_ISNULL_innerFiled_0);

")]
        [Theory]
        public void TsqlBatchVisitor_must_parse_query(string inputQuery, string expectedResult, string? expectedParameters = null /* TODO: make it nullable */)
        {
            using var reader = new StringReader(inputQuery);

            //Using SQL 2016 parser
            var parser = new TSql130Parser(true);

            var tree = parser.Parse(reader, out var errors);
            if (errors.Count > 0)
            {
                throw new Exception($"Thrown errors in query: {string.Join(",", errors.Select(i => i.Message))}");
            }

            var tsqlBatchVisitor = new TSqlBatchVisitor(__requiredTablesDefinition, __restrictedQueries);
            tree.Accept(tsqlBatchVisitor);

            var generator = new Sql130ScriptGenerator();
            generator.GenerateScript(tree, out var result);

            Assert.Equal(expectedResult, result);

            if (expectedParameters != null)
            {
                var actualParameters = tsqlBatchVisitor.CreatedParameters;
                var actualFormattedParameters = actualParameters
                    .SelectMany(i => i.Value.Select(ii => new Dictionary<string, object?> { [ii.KeyWithPosition] = ii.DefaultValue }))
                    .SelectMany(d => d)
                    .ToDictionary(d => d.Key, v => v.Value);

                var actualJsonParameters = JsonSerializer.Serialize(actualFormattedParameters);

                Assert.Equal(expectedParameters, actualJsonParameters);
            }
        }

        // Negative tests

        [InlineData(
            // input
            "select top 10 * from customers where 1=1 and 2=3",
            // expected
            "At least one filter condition must consist healthy query fragment, please validate it")]

        [InlineData(
            // input
            "select top 10 * from customers where Test=(select 1 from innertable where 1=1 and 2=3)",
            // expected
            "At least one filter condition must consist healthy query fragment, please validate it")]

        [InlineData(
            // input
            "select top 10 * from customers c inner join cc on 1=1",
            // expected
            "At least one filter condition must consist healthy query fragment, please validate it")]

        [InlineData(
            // input
            "select count(1) from customers group by customerId having 2 > 1",
            // expected
            "At least one filter condition must consist healthy query fragment, please validate it")]

        [InlineData(
            // input
            "delete from customer",
            // expected
            $"DELETE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "update customers set name='Name' where custId=1",
            // expected
            "UPDATE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "insert into customers(customerID,Name,dept) values(1,'smith','IT')",
            // expected
            "INSERT is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "truncate table customers",
            // expected
            "TRUNCATE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "create table customers(Id int,Name nvarchar(10));",
            // expected
            "CREATE_TABLE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "CREATE DATABASE databasename;",
            // expected
            "CREATE_DATABASE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "CREATE PROCEDURE SelectAllCustomers AS  SELECT * FROM Customers  GO;",
            // expected
            "CREATE_PROCEDURE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "CREATE FUNCTION CalculateSquare(@input_number INT)  RETURNS INT  AS  BEGIN  RETURN @input_number * @input_number;  END;",
            // expected
            "CREATE_FUNCTION is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "drop table customers",
            // expected
            "DROP_TABLE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "DROP DATABASE databasename;",
            // expected
            "DROP_DATABASE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "DROP INDEX table_name.index_name;",
            // expected
            "DROP_INDEX is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "ALTER TABLE Customers ALTER COLUMN Email varchar(255);",
            // expected
            "ALTER_TABLE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "ALTER PROCEDURE GetClients @Id INT,@DepartmentID INT AS  SELECT * FROM Clients WHERE ClientId = @Id AND DepartmentID = @DepartmentID;",
            // expected
            "ALTER_PROCEDURE is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "ALTER FUNCTION CalculateTotal(@value1 INT, @value2 INT)  RETURNS INT  AS  BEGIN  DECLARE @total INT  SET @total = 1  RETURN @total  END;",
            // expected
            "ALTER_FUNCTION is not allowed in the query in this tool. Please contact the DBA Team.")]

        [InlineData(
            // input
            "BEGIN TRAN select * from customers COMMIT TRAN",
            // expected
            "TRANSACTION_BEGIN is not allowed in the query in this tool. Please contact the DBA Team.")]


        [InlineData(
            // input
            "select * from customers COMMIT TRAN",
            // expected
            "TRANSACTION_COMMIT is not allowed in the query in this tool. Please contact the DBA Team.")]

        [Theory]
        public void TsqlBatchVisitor_must_fail_when_query_does_not_meet_requirements(string inputQuery, string errorMessage)
        {
            using var reader = new StringReader(inputQuery);
            //Using SQL 2016 parser
            var parser = new TSql130Parser(true);

            var tree = parser.Parse(reader, out var errors);
            if (errors.Count > 0)
            {
                throw new Exception($"Thrown errors in query: {string.Join(",", errors.Select(i => i.Message))}");
            }

            var tsqlBatchVisitor = new TSqlBatchVisitor(__requiredTablesDefinition, __restrictedQueries);
            Assert.Equal(Record.Exception(() => tree.Accept(tsqlBatchVisitor)).Message, errorMessage);
        }
    }
}