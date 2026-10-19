using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Models;

namespace Sport.Core
{
    public sealed class Ctx
    {
        public Session S { get; set; } = new Session();
        public Staff U { get; set; } = new Staff();
        public string Sbx => S.Sbx;
    }

    public sealed class MCtx
    {
        public Session S { get; set; } = new Session();
        public string Sbx => S.Sbx;
        public string Id => S.User;
    }

    public sealed class CardIn
    {
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Brand { get; set; } = "Card";
        public string Last4 { get; set; } = string.Empty;
        public string ErrName { get; set; } = string.Empty;
        public string ErrNumber { get; set; } = string.Empty;
        public string ErrExp { get; set; } = string.Empty;
        public string ErrCvc { get; set; } = string.Empty;
        public bool Declined { get; set; }
        public bool Ok => ErrName + ErrNumber + ErrExp + ErrCvc == string.Empty;
    }

    public static class Auth
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim SessGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();

        private static string SessionFile => Path.Combine(Store.Root, "temp", "admin", "sessions.json");

        private static async Task<List<Session>> ReadSessions()
        {
            if (!File.Exists(SessionFile))
            {
                return new List<Session>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<Session>>(await File.ReadAllTextAsync(SessionFile), ReadOpts) ?? new List<Session>();
            }
            catch (JsonException)
            {
                return new List<Session>();
            }
        }

        private static async Task WriteSessions(List<Session> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFile) ?? Store.Root);
            string tmp = SessionFile + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, WriteOpts));
            File.Move(tmp, SessionFile, true);
        }

        public static bool Locked(string login)
        {
            lock (Fails)
            {
                string k = login.ToLowerInvariant();
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(f => f < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Fail(string login)
        {
            lock (Fails)
            {
                string k = login.ToLowerInvariant();
                if (Fails.Count > 2000)
                {
                    Fails.Clear();
                }
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Fails[k] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public static DateTime Expiry(string sbx, int hours)
        {
            return sbx != string.Empty ? Store.Made(sbx).AddMinutes(Store.SandboxMinutes) : DateTime.UtcNow.AddHours(hours);
        }

        public static async Task<string> Open(string sbx, string user, DateTime exp, string kind)
        {
            string tok = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                list.RemoveAll(x => x.Exp < DateTime.UtcNow);
                list.Add(new Session { Tok = tok, Sbx = sbx, User = user, Exp = exp, Kind = kind });
                await WriteSessions(list.OrderByDescending(x => x.Exp).Take(2000).ToList());
            }
            finally
            {
                SessGate.Release();
            }
            return tok;
        }

        public static async Task Close(string tok)
        {
            string t = Store.CleanId(tok);
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.Tok == t) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task CloseUser(string sbx, string user, string keep, string kind = "staff")
        {
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.Sbx == sbx && x.User == user && x.Kind == kind && x.Tok != keep) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
        }

        private static async Task<Session?> Find(string? tokValue, string kind)
        {
            string tok = Store.CleanId(tokValue);
            if (tok == string.Empty)
            {
                return null;
            }
            Session? s;
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                int before = list.Count;
                list.RemoveAll(x => x.Exp < DateTime.UtcNow || (x.Sbx != string.Empty && Store.Sandbox(x.Sbx) == string.Empty) || (x.Sbx == string.Empty && Store.Demo));
                s = list.FirstOrDefault(x => x.Tok == tok && x.Kind == kind);
                bool slide = s != null && s.Sbx == string.Empty && s.Exp < DateTime.UtcNow.AddHours(7);
                if (s != null && slide)
                {
                    s.Exp = DateTime.UtcNow.AddHours(8);
                }
                if (list.Count != before || slide)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
            return s;
        }

        public static async Task<Ctx?> Check(string? tokValue)
        {
            Session? s = await Find(tokValue, "staff");
            if (s == null)
            {
                return null;
            }
            List<Staff> staff = await Store.LoadStaff(s.Sbx);
            Staff? u = staff.FirstOrDefault(x => x.Id == s.User && x.Active && (Store.Demo || !x.Demo));
            return u == null ? null : new Ctx { S = s, U = u };
        }

        public static async Task<MCtx?> Member(string? tokValue)
        {
            Session? s = await Find(tokValue, "member");
            return s == null ? null : new MCtx { S = s };
        }

        public static string Menu(string p, string page, Ctx c, SiteData site)
        {
            string cur = page == "AdminAlbums" ? "AdminMedia" : page;
            List<(string Key, string Name, int Count)> items = new List<(string, string, int)> { ("Admin", "Dashboard", 0) };
            foreach (Right a in Access.Rights)
            {
                if (!Access.Can(c.U, a.Key, 'V'))
                {
                    continue;
                }
                int cnt = a.Key switch
                {
                    "payments" => site.Registrations.Count(r => r.Ask != null && r.Ask.Status == "open"),
                    "registrations" => site.Registrations.Count(r => r.Status == "pending"),
                    "messages" => site.Messages.Count(x => x.Status == "new"),
                    _ => 0
                };
                string name = a.Key == "board" && Access.OwnOnly(c.U) ? "My classes" : a.Name;
                items.Add((a.Page, name, cnt));
            }
            if (!Access.Can(c.U, "activity", 'V'))
            {
                items.Add(("AdminActivity", "My activity", 0));
            }
            items.Add(("AdminAccount", "My account", 0));
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string n, int cnt) in items)
            {
                sb.Append("<li><a class=\"" + p + "-al" + (k == cur ? " " + p + "-on" : string.Empty) + "\" href=\"" + k + "\">" + Ui.Enc(n) + (cnt > 0 ? "<i class=\"" + p + "-cnt\">" + cnt.ToString(CultureInfo.InvariantCulture) + "</i>" : string.Empty) + "</a></li>");
            }
            return sb.ToString();
        }

        public static string Who(string p, Ctx c)
        {
            return Ui.Avatar(p, c.U) + "<span class=\"" + p + "-who-t\"><b>" + Ui.Enc(c.U.Name) + "</b><small>" + Ui.Enc(c.U.Title) + "</small></span>";
        }

        public static string DemoPill(string p, Ctx c)
        {
            return c.Sbx != string.Empty ? "<span class=\"" + p + "-dlong\">Demo copy &middot; resets in&nbsp;</span><span class=\"" + p + "-dshort\">Demo &middot;&nbsp;</span>" + Store.MinutesLeft(c.Sbx) + "&nbsp;min" : string.Empty;
        }

        public static CardIn ReadCard(string name, string number, string exp, string cvc)
        {
            CardIn c = new CardIn { Name = Ui.Clip(name, 60), Number = Regex.Replace(number ?? string.Empty, "[\\s-]", string.Empty) };
            c.ErrName = c.Name.Length < 2 ? "Enter the name on the card." : string.Empty;
            c.ErrNumber = !Regex.IsMatch(c.Number, "^[0-9]{13,19}$") || !Access.Luhn(c.Number) ? "That card number isn't valid. Use the test card 4242 4242 4242 4242." : string.Empty;
            Match em = Regex.Match(Ui.Clip(exp, 5), "^(0[1-9]|1[0-2])/([0-9]{2})$");
            if (!em.Success)
            {
                c.ErrExp = "Use MM/YY.";
            }
            else
            {
                DateTime last = new DateTime(2000 + int.Parse(em.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(em.Groups[1].Value, CultureInfo.InvariantCulture), 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                if (last <= DateTime.UtcNow)
                {
                    c.ErrExp = "This card has expired.";
                }
            }
            c.Brand = c.Number.Length > 0 ? Access.Brand(c.Number) : "Card";
            c.ErrCvc = !Regex.IsMatch(Ui.Clip(cvc, 4), c.Brand == "Amex" ? "^[0-9]{4}$" : "^[0-9]{3}$") ? (c.Brand == "Amex" ? "Enter the 4-digit code." : "Enter the 3-digit code.") : string.Empty;
            c.Last4 = c.Number.Length >= 4 ? c.Number.Substring(c.Number.Length - 4) : string.Empty;
            c.Declined = c.Number == "4000000000000002";
            return c;
        }
    }
}
