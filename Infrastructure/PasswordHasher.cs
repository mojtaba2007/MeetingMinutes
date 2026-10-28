using System;
using System.Configuration;
using System.Security.Cryptography;

namespace MeetingMinutes.Infrastructure
{
    /// <summary>
    /// PBKDF2-SHA256 با salt تصادفی. نیازمند .NET Framework 4.7.2 یا بالاتر.
    /// رمز عبور هرگز به‌صورت متن ساده ذخیره یا لاگ نمی‌شود.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int MinIterations = 100000;

        public static int DefaultIterations
        {
            get
            {
                int n;
                return int.TryParse(ConfigurationManager.AppSettings["PasswordIterations"], out n) && n >= MinIterations
                    ? n : MinIterations;
            }
        }

        public static void Create(string password, out byte[] hash, out byte[] salt, out int iterations)
        {
            salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            iterations = DefaultIterations;
            hash = Derive(password, salt, iterations);
        }

        public static bool Verify(string password, byte[] hash, byte[] salt, int iterations)
        {
            var test = Derive(password, salt, iterations);
            if (hash == null || hash.Length != test.Length) return false;
            int diff = 0;
            for (int i = 0; i < test.Length; i++) diff |= test[i] ^ hash[i];
            return diff == 0;
        }

        /// <summary>سیاست ساده‌ی رمز عبور: حداقل ۸ نویسه و برابر نبودن با نام کاربری.</summary>
        public static bool TryValidate(string username, string password, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                error = "رمز عبور باید حداقل ۸ نویسه باشد.";
            else if (string.Equals(username, password, StringComparison.OrdinalIgnoreCase))
                error = "رمز عبور نباید با نام کاربری یکی باشد.";
            return error == null;
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password ?? string.Empty, salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(HashSize);
        }
    }
}
