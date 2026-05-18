using Backend.Shared;

namespace Backend.TestAppHost;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        builder
            .AddPostgres("postgres")
            .AddDatabase(Services.Database, databaseName: "carenestai");

        builder.Build().Run();
    }
}
