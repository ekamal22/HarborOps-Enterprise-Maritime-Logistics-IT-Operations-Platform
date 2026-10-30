using HarborOps.Infrastructure.Identity;

namespace HarborOps.Api.Authentication;

public static class AccountCli
{
    public static async Task RunAsync(string[] args, IServiceProvider services)
    {
        if (Environment.GetEnvironmentVariable("HARBOROPS_ALLOW_ACCOUNT_CLI") != "true" ||
            args.Length < 3 || args[0] != "--account-cli")
            throw new InvalidOperationException("Restricted account operation not enabled");
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IdentityStore>();
        var mode = args[1];
        if (mode is not ("provision" or "reset") || (mode == "provision" && args.Length != 4) ||
            (mode == "reset" && args.Length != 3))
            throw new ArgumentException("Usage: --account-cli provision EMAIL DISPLAY_NAME or --account-cli reset EMAIL");
        Console.Error.Write("Password: ");
        string password;
        if (Console.IsInputRedirected)
            password = await Console.In.ReadLineAsync() ?? "";
        else
        {
            var chars = new List<char>();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
                else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
            }
            password = new string(chars.ToArray());
            chars.Clear();
        }
        Console.Error.WriteLine();
        if (mode == "provision") await store.ProvisionAsync(args[2], args[3], password, CancellationToken.None);
        else await store.ResetPasswordAsync(args[2], password, CancellationToken.None);
        Console.WriteLine("Account operation completed");
    }
}
