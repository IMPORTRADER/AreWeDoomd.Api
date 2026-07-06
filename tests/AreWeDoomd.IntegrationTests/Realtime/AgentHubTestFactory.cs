using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AreWeDoomd.IntegrationTests.Realtime;

public sealed class AgentHubTestFactory : WebApplicationFactory<Program>
{
    public const string SharedSecret = "integration-test-secret";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((_, loggerConfig) =>
            loggerConfig.WriteTo.Console());

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("AgentNotifications:SharedSecret", SharedSecret);
        builder.UseSetting(
            "ConnectionStrings:AreWeDoomdSql",
            "Server=localhost;Database=test;Trusted_Connection=True;TrustServerCertificate=True;");
        // neutralize any host-machine Admin:Usernames so the startup seeder stays inert against the fake test DB
        builder.UseSetting("Admin:Usernames:0", "");
        builder.UseSetting("Admin:SeedOnStartup", "false");
        builder.UseSetting("Preflight:Database:Enabled", "false");
    }
}
