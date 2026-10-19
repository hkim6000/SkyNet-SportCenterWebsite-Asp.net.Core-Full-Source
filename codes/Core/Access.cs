using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Sport.Models;

namespace Sport.Core
{
    public sealed class Right
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public string Hint { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
    }

    public static class Access
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static readonly Right[] Rights =
        {
            new Right { Key = "sports", Name = "Sport types", Page = "AdminSports", Hint = "Sports shown on the website" },
            new Right { Key = "classes", Name = "Classes", Page = "AdminClasses", Extra = "Cancel class", Hint = "Schedules, prices, seats. Cancelling refunds everyone" },
            new Right { Key = "board", Name = "Class board", Page = "AdminBoard", Hint = "Rosters, check-in and check-out, waitlist" },
            new Right { Key = "instructors", Name = "Instructors", Page = "AdminInstructors", Hint = "Coach profiles" },
            new Right { Key = "registrations", Name = "Registrations", Page = "AdminRegistrations", Extra = "Override limits", Hint = "Sign people up, cancel, front desk payments" },
            new Right { Key = "payments", Name = "Payments & refunds", Page = "AdminPayments", Extra = "Refund", Hint = "Charges, refund requests, CSV export" },
            new Right { Key = "members", Name = "Members", Page = "AdminMembers", Extra = "Adjust points", Hint = "Accounts, day passes, suspend" },
            new Right { Key = "messages", Name = "Messages", Page = "AdminMessages", Hint = "Contact form" },
            new Right { Key = "media", Name = "Media", Page = "AdminMedia", Extra = "Delete", Hint = "Photos, videos, PDFs and albums" },
            new Right { Key = "pages", Name = "Pages", Page = "AdminPages", Hint = "Website text and pictures" },
            new Right { Key = "activity", Name = "Activity log", Page = "AdminActivity", NoEdit = true, Hint = "What everyone did" },
            new Right { Key = "settings", Name = "Settings", Page = "AdminSettings", AdminOnly = true },
            new Right { Key = "users", Name = "Users", Page = "AdminUsers", AdminOnly = true }
        };

        public static Right? RightOf(string key)
        {
            return Rights.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(Staff u, string area)
        {
            if (u.Admin)
            {
                return "VEX";
            }
            Right? a = RightOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string p = u.Perms.TryGetValue(area, out string? v) ? v ?? string.Empty : string.Empty;
            string r = string.Empty;
            if (p.Contains('V') || p.Contains('E') || p.Contains('X'))
            {
                r += "V";
            }
            if (p.Contains('E') && !a.NoEdit)
            {
                r += "E";
            }
            if (p.Contains('X') && p.Contains('E') && a.Extra != string.Empty)
            {
                r += "X";
            }
            return r;
        }

        public static bool Can(Staff u, string area, char letter)
        {
            return Level(u, area).Contains(letter);
        }

        public static bool OwnOnly(Staff u)
        {
            return !u.Admin && u.Instructor != string.Empty;
        }

        public static bool Mine(Staff u, SportClass k)
        {
            return !OwnOnly(u) || k.Instructor == u.Instructor;
        }

        public static string Clean(string area, string value)
        {
            Right? a = RightOf(area);
            if (a == null || a.AdminOnly)
            {
                return string.Empty;
            }
            string v = value ?? string.Empty;
            string r = string.Empty;
            if (v.Contains('V') || v.Contains('E'))
            {
                r = "V";
            }
            if (v.Contains('E') && !a.NoEdit)
            {
                r += "E";
                if (v.Contains('X') && a.Extra != string.Empty)
                {
                    r += "X";
                }
            }
            return r;
        }

        public static string NewPassword()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    sb.Append('-');
                }
                sb.Append(Letters[RandomNumberGenerator.GetInt32(Letters.Length)]);
            }
            return sb.ToString();
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(dk);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? string.Empty).Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out int iter) || iter < 10000 || iter > 1000000)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] want = Convert.FromBase64String(parts[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool VerifySha(string password, string hex)
        {
            string hash = (hex ?? string.Empty).ToLowerInvariant();
            if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$"))
            {
                return false;
            }
            string given = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(hash));
        }

        public static bool Luhn(string digits)
        {
            int sum = 0;
            bool dbl = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (d < 0 || d > 9)
                {
                    return false;
                }
                if (dbl)
                {
                    d *= 2;
                    if (d > 9)
                    {
                        d -= 9;
                    }
                }
                sum += d;
                dbl = !dbl;
            }
            return sum % 10 == 0;
        }

        public static string Brand(string digits)
        {
            if (digits.StartsWith('4'))
            {
                return "Visa";
            }
            if (Regex.IsMatch(digits, "^(5[1-5]|2[2-7])"))
            {
                return "Mastercard";
            }
            if (Regex.IsMatch(digits, "^3[47]"))
            {
                return "Amex";
            }
            if (digits.StartsWith("6011") || digits.StartsWith("65"))
            {
                return "Discover";
            }
            return "Card";
        }
    }
}
