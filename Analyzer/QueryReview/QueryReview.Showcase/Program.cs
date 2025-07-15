using Microsoft.Data.SqlClient;

internal class Program
{
    private static void Main(string[] args)
    {
        using var sqlConnection = new SqlConnection();
        sqlConnection.Open();
        using var command = sqlConnection.CreateCommand();
#pragma warning disable TRSP01 // Review CommandText assignment
        command.CommandText = "SELECT UnexistedField, Name FROM CustomersTable";
#pragma warning restore TRSP01 // Review CommandText assignment
        var result = command.ExecuteScalar();
        Console.WriteLine(result);
    }
}