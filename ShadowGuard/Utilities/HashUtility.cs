using System.Security.Cryptography;
using System.Text;

namespace ShadowGuard;

public static class HashUtility
{
    public static string CreateBomReference(string name, string version, string ecosystem)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{ecosystem}:{name}:{version}");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }
}
