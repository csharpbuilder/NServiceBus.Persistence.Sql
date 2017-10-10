using System;
using System.Data.SqlClient;

public static class MsSqlConnectionBuilder
{
    const string ConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;";

    public static SqlConnection Build()
    {
        var connection = Environment.GetEnvironmentVariable("SQLServerConnectionString");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return new SqlConnection(ConnectionString);
        }
        return new SqlConnection(connection);
    }

    public static bool IsSql2016OrHigher()
    {
        using (var connection = Build())
        {
            connection.Open();
            return Version.Parse(connection.ServerVersion).Major >= 13;
        }
    }
}