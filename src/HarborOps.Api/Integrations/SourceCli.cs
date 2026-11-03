using HarborOps.Infrastructure.Integrations;

namespace HarborOps.Api.Integrations;

public static class SourceCli
{
    public static async Task RunAsync(string[] args, IServiceProvider services)
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_ALLOW_SOURCE_CLI") != "true" ||
            args.Length < 4 || args[0] != "--source-cli" ||
            !Guid.TryParse(args[2], out var organizationId))
            throw new InvalidOperationException("Restricted source operation not enabled");
        var action = args[1];
        if (action is not ("create" or "rotate" or "revoke" or "disable" or "inspect"))
            throw new InvalidOperationException("Unsupported source operation");
        if (action == "create" && args.Length != 5 ||
            action == "revoke" && args.Length != 5 ||
            (action is "rotate" or "disable" or "inspect") && args.Length != 4)
            throw new InvalidOperationException("Invalid source operation arguments");
        if ((action is "create" or "rotate") && Console.IsOutputRedirected)
            throw new InvalidOperationException("Provisioning requires a protected interactive terminal");

        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IntegrationSourceStore>();
        switch (action)
        {
            case "create":
            {
                var issued = await store.CreateAsync(organizationId, args[3], args[4], CancellationToken.None);
                Console.WriteLine($"Source {issued.Id} credential {issued.CredentialId}");
                Console.WriteLine("Bearer (displayed once): " + issued.Bearer);
                break;
            }
            case "rotate":
            {
                var issued = await store.RotateAsync(organizationId, args[3], CancellationToken.None);
                Console.WriteLine($"Source {issued.Id} credential {issued.CredentialId}");
                Console.WriteLine("Bearer (displayed once): " + issued.Bearer);
                break;
            }
            case "revoke":
                if (!Guid.TryParse(args[4], out var credentialId))
                    throw new ArgumentException("Invalid credential ID");
                await store.RevokeAsync(organizationId, args[3], credentialId, CancellationToken.None);
                Console.WriteLine("Credential revoked");
                break;
            case "disable":
                await store.DisableAsync(organizationId, args[3], CancellationToken.None);
                Console.WriteLine("Source disabled");
                break;
            default:
                Console.WriteLine(await store.InspectAsync(organizationId, args[3], CancellationToken.None));
                break;
        }
    }
}
