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

        public static void SaveCredentials(string username, string password)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            string data = username + "\n" + password;
            byte[] plainBytes = Encoding.UTF8.GetBytes(data);

            byte[] encrypted = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser
            );

            File.WriteAllBytes(FilePath, encrypted);
        }

        public static (string Username, string Password)? LoadCredentials()
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
                string[] parts = data.Split(new[] { '\n' }, 2);

                if (parts.Length != 2)
                    return null;

                return (parts[0], parts[1]);
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
