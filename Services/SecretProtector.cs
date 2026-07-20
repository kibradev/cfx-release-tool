using System.Security.Cryptography;
using System.Text;

namespace ReleaseTool.Desktop.Services;

public static class SecretProtector
{
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return "";

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
            return "";

        try
        {
            var bytes = Convert.FromBase64String(protectedText);
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return protectedText;
        }
    }
}
