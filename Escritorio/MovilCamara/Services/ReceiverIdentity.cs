using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;

namespace MovilCamara.Services;

[SupportedOSPlatform("windows")]
public static class ReceiverIdentity
{
    private const string FriendlyName = "WT MultiCam Local Receiver";
    public static X509Certificate2 LoadOrCreate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var existing = store.Certificates.Cast<X509Certificate2>()
            .FirstOrDefault(c => c.FriendlyName == FriendlyName && c.Subject == "CN=WT MultiCam" &&
                c.HasPrivateKey && c.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(1));
        if (existing != null) return existing;
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=WT MultiCam", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
        var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        certificate.FriendlyName = FriendlyName;
        store.Add(certificate);
        return certificate;
    }
}
