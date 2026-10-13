using System.Security.Cryptography;
using System.Text;

namespace BusinessLogic.Idempotency;

public static class IdempotencyHash
{
    public static string Of(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
