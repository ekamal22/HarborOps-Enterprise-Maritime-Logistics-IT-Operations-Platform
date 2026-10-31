using HarborOps.Infrastructure.Organizations;

namespace HarborOps.Api.Organizations;

public static class OrganizationCli
{
    public static async Task RunAsync(string[] args, IServiceProvider services)
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_ALLOW_ORGANIZATION_CLI") != "true" ||
            args.Length != 5 || args[0] != "--organization-cli" || args[1] != "bootstrap")
            throw new InvalidOperationException("Restricted organization operation not enabled. Usage: --organization-cli bootstrap SLUG DISPLAY_NAME ADMIN_EMAIL");
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<OrganizationStore>();
        var org = await store.BootstrapAsync(args[2], args[3], args[4], CancellationToken.None);
        Console.WriteLine($"Organization created: {org.Slug} ({org.Id})");
    }
}
