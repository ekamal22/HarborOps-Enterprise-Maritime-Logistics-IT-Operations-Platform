using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace HarborOps.Infrastructure.Identity;

public sealed class PasswordHasher
{
    private static readonly SemaphoreSlim HashSlots = new(2, 2);
    private const int MemoryKib = 65536;
    private const int Iterations = 3;
    private const int Lanes = 1;
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123456789", "123456789012345", "correcthorsebatterystaple",
        "qwerty123456789", "admin1234567890", "letmein123456789"
    };

    public static void ValidateNewPassword(string password)
    {
        var size = Encoding.UTF8.GetByteCount(password);
        if (password.Length < 15 || size > 1024 || Blocked.Contains(password) ||
            password.All(char.IsWhiteSpace))
            throw new ArgumentException("Password does not meet local account policy");
    }

    public async Task<string> HashAsync(string password, CancellationToken ct = default)
    {
        ValidateNewPassword(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var digest = await DeriveAsync(password, salt, ct);
        return $"argon2id$v=19$m={MemoryKib},t={Iterations},p={Lanes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(digest)}";
    }

    public async Task<bool> VerifyAsync(string password, string encoded, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 1024)
            return false;
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19" ||
                parts[2] != $"m={MemoryKib},t={Iterations},p={Lanes}")
                return false;
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var actual = await DeriveAsync(password, salt, ct);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    private static async Task<byte[]> DeriveAsync(string password, byte[] salt, CancellationToken ct)
    {
        await HashSlots.WaitAsync(ct);
        try
        {
            using var algorithm = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt, DegreeOfParallelism = Lanes,
                MemorySize = MemoryKib, Iterations = Iterations
            };
            return await algorithm.GetBytesAsync(32);
        }
        finally { HashSlots.Release(); }
    }
}
