using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace POS.Services.Credentials
{
    public static class CredentialStore
    {
        private static readonly string FilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "POS",
                "credentials.dat"
            );

        public static void SaveUsername(string username)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            byte[] plainBytes = Encoding.UTF8.GetBytes(username ?? string.Empty);

            byte[] encrypted = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser
            );

            File.WriteAllBytes(FilePath, encrypted);
        }

        public static string LoadUsername()
        {
            if (!File.Exists(FilePath))
                return null;

            try
            {
                byte[] encrypted = File.ReadAllBytes(FilePath);

                byte[] decrypted = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.CurrentUser
                );

                string data = Encoding.UTF8.GetString(decrypted);
                return data.Split(new[] { '\n' }, 2)[0];
            }
            catch
            {
                return null;
            }
        }

        public static void ClearCredentials()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
