using Microsoft.Data.SqlClient;

internal class Program
{
    private static void Main(string[] _)
    {
        using var sqlConnection = new SqlConnection();
        sqlConnection.Open();
        using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT UnexistedField, Name FROM CustomersTable";
        var result = command.ExecuteScalar();
        Console.WriteLine(result);

    }
}