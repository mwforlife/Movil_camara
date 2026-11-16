using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace MovilCamara.Services;

internal sealed class ReceiverSessionStore(X509Certificate2 identity, string path)
{
    private readonly object gate = new();
    public Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return result;
            using var key = identity.GetRSAPrivateKey();
            if (key == null) return result;
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            foreach (var item in (data ?? new()).Take(128))
            {
                try
                {
                    var secret = Encoding.UTF8.GetString(key.Decrypt(Convert.FromBase64String(item.Value), RSAEncryptionPadding.OaepSHA256));
                    if (item.Key.Length is >= 8 and <= 80 && secret.Length == 64 && secret.All(Uri.IsHexDigit))
                        result[item.Key] = secret;
                }
                catch (Exception ex) when (ex is CryptographicException or FormatException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
        { /* Invalid or inaccessible state requires fresh pairing, never implicit authorization. */ }
        return result;
    }
    public void Save(IEnumerable<KeyValuePair<string, string>> sessions)
    {
        lock (gate)
        {
            using var key = identity.GetRSAPublicKey() ?? throw new CryptographicException("Identidad RSA no disponible");
            var data = sessions.Take(128).ToDictionary(p => p.Key,
                p => Convert.ToBase64String(key.Encrypt(Encoding.UTF8.GetBytes(p.Value), RSAEncryptionPadding.OaepSHA256)));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data));
            File.Move(temp, path, true);
        }
    }
}
