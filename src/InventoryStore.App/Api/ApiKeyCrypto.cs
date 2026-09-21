using System.Security.Cryptography;
using System.Text;

namespace InventoryStore.App.Api;

// API keys are 256 bits of randomness, so a plain SHA-256 is enough to store them safely (there
// is nothing to brute-force, unlike a human password). Only the hash is persisted.
internal static class ApiKeyCrypto
{
    internal const string KeyPrefix = "inv_";
    private const int DisplayPrefixLength = 8;

    // Returns the raw key (shown once), its hash, and the short display prefix.
    internal static (string Key, string Hash, string Prefix) Generate()
    {
        var key = KeyPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        return (key, Hash(key), key[..DisplayPrefixLength]);
    }

    internal static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
