using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;

[SetUpFixture]
public class SetUpFixture
{
    [OneTimeSetUp]
    public void SetUp()
    {
#if NET452
        ObjectApproval.ObjectApprover.JsonSerializer.DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include;
#endif
        FixCurrentDirectory();
        using (var connection = MsSqlConnectionBuilder.Build())
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
if not exists (
    select  *
    from sys.schemas
    where name = 'schema_name')
exec('create schema schema_name');";
                command.ExecuteNonQuery();
            }
        }
        using (var connection = PostgreSqlConnectionBuilder.Build())
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"create schema if not exists ""SchemaName"";";
                command.ExecuteNonQuery();
            }
        }
    }

    void FixCurrentDirectory([CallerFilePath] string callerFilePath="")
    {
        Environment.CurrentDirectory = Directory.GetParent(callerFilePath).FullName;
    }
}