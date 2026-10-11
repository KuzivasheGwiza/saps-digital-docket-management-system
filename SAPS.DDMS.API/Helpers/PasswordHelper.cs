using System.Security.Cryptography;
using System.Text;

namespace SAPS.DDMS.API.Helpers
{
    public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        public static bool MeetsComplexityRequirements(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        public static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string special = "!@#$%";

            var random = new Random();
            var password = new char[12];

            password[0] = upper[random.Next(upper.Length)];
            password[1] = lower[random.Next(lower.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            string allChars = upper + lower + digits + special;
            for (int i = 4; i < 12; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            return new string(password.OrderBy(x => random.Next()).ToArray());
        }

        public static string GenerateActivationToken()
        {
            // Method 1: Use Guid.ToString("N") - Returns 32 hex characters (safe and simple)
            return Guid.NewGuid().ToString("N");

            // Method 2: If you want to keep Base64 approach, generate enough bytes
            // using var rng = RandomNumberGenerator.Create();
            // var bytes = new byte[24]; // 24 bytes = 192 bits, produces 32 chars in Base64
            // rng.GetBytes(bytes);
            // return Convert.ToBase64String(bytes)
            //     .Replace("+", "")
            //     .Replace("/", "")
            //     .Replace("=", "")
            //     .Substring(0, 32);
        }

        public static string GenerateSixDigitOtp()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        public static string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
