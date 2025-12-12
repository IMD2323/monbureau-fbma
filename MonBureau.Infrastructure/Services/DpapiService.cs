using System;
using System.Security.Cryptography;
using System.Text;

namespace MonBureau.Infrastructure.Services
{
    public class DpapiService
    {
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null, // entropy parameter (optional additional data)
                DataProtectionScope.LocalMachine
            );
            return Convert.ToBase64String(encryptedBytes);
        }

        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null, // entropy parameter (must match Protect call)
                    DataProtectionScope.LocalMachine
                );
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}