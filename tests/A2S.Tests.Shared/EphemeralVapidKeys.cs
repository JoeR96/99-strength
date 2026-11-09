using System.Buffers.Text;
using System.Security.Cryptography;

namespace A2S.Tests.Shared;

/// <summary>
/// A throwaway VAPID (P-256) key pair for tests. Generated on demand and never written anywhere, so
/// no real key is ever committed; the format matches what <c>web-push generate-vapid-keys</c> emits.
/// </summary>
public sealed record EphemeralVapidKeys(string PublicKey, string PrivateKey)
{
    public const string Subject = "mailto:tests@example.com";

    public static EphemeralVapidKeys Generate()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: true);
        var uncompressed = new byte[65];
        uncompressed[0] = 0x04;
        p.Q.X!.CopyTo(uncompressed, 1);
        p.Q.Y!.CopyTo(uncompressed, 33);
        return new EphemeralVapidKeys(Base64Url.EncodeToString(uncompressed), Base64Url.EncodeToString(p.D!));
    }

    /// <summary>A browser-side key pair as a push subscription would carry: 65-byte p256dh point and 16-byte auth secret.</summary>
    public static (string P256dh, string Auth) GenerateClientKeys()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdh.ExportParameters(includePrivateParameters: false);
        var point = new byte[65];
        point[0] = 0x04;
        p.Q.X!.CopyTo(point, 1);
        p.Q.Y!.CopyTo(point, 33);
        return (Base64Url.EncodeToString(point), Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)));
    }
}
