using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Models;

namespace Sport.Core
{
    public static class Store
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new ConcurrentDictionary<string, TimeZoneInfo>();

        public const int SandboxMinutes = 60;

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string Cfg(string key)
        {
            string file = Path.Combine(Root, "appConfig", "application.cfg");
            if (!File.Exists(file))
            {
                return string.Empty;
            }
            foreach (string line in File.ReadAllLines(file))
            {
                int i = line.IndexOf('|');
                if (i > 0 && line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(i + 1).Trim();
                }
            }
            return string.Empty;
        }

        public static bool Demo => Cfg("app.app.runmode") != "1";

        public static string CleanId(string? value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(v, "^[a-f0-9]{32}$") ? v : string.Empty;
        }

        public static string SandboxDir(string sbx)
        {
            return Path.Combine(Root, "temp", "sandbox", sbx);
        }

        public static string Sandbox(string? value)
        {
            string id = CleanId(value);
            return id != string.Empty && Demo && File.Exists(Path.Combine(SandboxDir(id), "site.json")) && Made(id) > DateTime.UtcNow.AddMinutes(-SandboxMinutes) ? id : string.Empty;
        }

        public static DateTime Made(string sbx)
        {
            string file = Path.Combine(SandboxDir(sbx), "made");
            return File.Exists(file) && long.TryParse(File.ReadAllText(file), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) ? new DateTime(t, DateTimeKind.Utc) : Directory.GetCreationTimeUtc(SandboxDir(sbx));
        }

        public static int MinutesLeft(string sbx)
        {
            return Math.Max(1, (int)Math.Ceiling((Made(sbx).AddMinutes(SandboxMinutes) - DateTime.UtcNow).TotalMinutes));
        }

        public static string Up(string sbx)
        {
            return sbx == string.Empty ? "images/uploads" : "images/uploads/sandbox/" + sbx;
        }

        public static string UpDir(string sbx, string sub)
        {
            string d = Path.Combine(Root, Up(sbx).Replace('/', Path.DirectorySeparatorChar), sub);
            Directory.CreateDirectory(d);
            return d;
        }

        private static string FileOf(string name, string sbx)
        {
            return Path.Combine(sbx == string.Empty ? DataDir : SandboxDir(sbx), name + ".json");
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Release(Gate);
        }

        private sealed class Release : IDisposable
        {
            private SemaphoreSlim? gate;

            public Release(SemaphoreSlim g)
            {
                gate = g;
            }

            public void Dispose()
            {
                gate?.Release();
                gate = null;
            }
        }

        private static async Task<T> Read<T>(string name, string sbx) where T : new()
        {
            string file = FileOf(name, sbx);
            if (!File.Exists(file))
            {
                file = FileOf(name, string.Empty);
            }
            if (!File.Exists(file))
            {
                return new T();
            }
            try
            {
                return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file), ReadOpts) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        private static async Task Write<T>(string name, string sbx, T data)
        {
            string file = FileOf(name, sbx);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            string tmp = file + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(data, WriteOpts));
            File.Move(tmp, file, true);
        }

        public static async Task<SiteData> LoadSite(string sbx)
        {
            SiteData site = await Read<SiteData>("site", sbx);
            if (site.Anchor != default)
            {
                if (Demo && sbx == string.Empty)
                {
                    Shift(site, Weeks(site));
                    Fresh(site);
                }
                else if (!Demo && sbx == string.Empty)
                {
                    using (await Hold())
                    {
                        site = await Read<SiteData>("site", string.Empty);
                        if (site.Anchor != default)
                        {
                            Shift(site, Weeks(site));
                            site.Anchor = default;
                            await Write("site", string.Empty, site);
                        }
                    }
                }
            }
            Fill(site);
            Rules.Tidy(site, DateTime.UtcNow);
            return site;
        }

        public static Task SaveSite(SiteData site, string sbx)
        {
            return Write("site", sbx, site);
        }

        private static void Fill(SiteData site)
        {
            for (int d = 0; d < 7; d++)
            {
                if (!site.Settings.Hours.Any(h => h.Day == d))
                {
                    site.Settings.Hours.Add(new OpenDay { Day = d, Open = true });
                }
            }
            site.Settings.Hours = site.Settings.Hours.OrderBy(h => h.Day).ToList();
            Blocks.Ensure(site);
        }

        private static TimeSpan Weeks(SiteData site)
        {
            if (site.Anchor == default)
            {
                return TimeSpan.Zero;
            }
            double days = Math.Floor((Today(site) - site.Anchor.Date).TotalDays);
            return TimeSpan.FromDays(Math.Floor(days / 7) * 7);
        }

        private static DateTime? Opt(DateTime? v, TimeSpan d)
        {
            return v?.Add(d);
        }

        private static void Fresh(SiteData site)
        {
            DateTime nowU = DateTime.UtcNow;
            foreach (Registration r in site.Registrations)
            {
                if (r.Status == "offered")
                {
                    r.Offered = nowU.AddHours(-3);
                    r.OfferExp = r.Offered.Value.AddHours(Math.Max(1, site.Settings.OfferHours));
                }
                else if (r.Status == "pending")
                {
                    r.Created = nowU.AddHours(-6);
                    r.HoldExp = r.Created.AddHours(Math.Max(1, site.Settings.DeskHours));
                }
            }
        }

        private static void Shift(SiteData site, TimeSpan d)
        {
            if (site.Anchor == default || d == TimeSpan.Zero)
            {
                return;
            }
            foreach (SportClass k in site.Classes)
            {
                k.First = k.First.Add(d);
                k.Dates = k.Dates.Select(x => x.Add(d)).ToList();
                k.Opens = Opt(k.Opens, d);
                k.Cancelled = Opt(k.Cancelled, d);
                k.Created = k.Created.Add(d);
            }
            foreach (ClubMember m in site.Members)
            {
                m.Created = m.Created.Add(d);
                m.Joined = Opt(m.Joined, d);
                m.Suspended = Opt(m.Suspended, d);
                m.ResetExp = Opt(m.ResetExp, d);
            }
            foreach (Registration r in site.Registrations)
            {
                r.Created = r.Created.Add(d);
                r.Confirmed = Opt(r.Confirmed, d);
                r.Offered = Opt(r.Offered, d);
                r.OfferExp = Opt(r.OfferExp, d);
                r.HoldExp = Opt(r.HoldExp, d);
                r.Ended = Opt(r.Ended, d);
                if (r.Ask != null)
                {
                    r.Ask.When = r.Ask.When.Add(d);
                    r.Ask.Decided = Opt(r.Ask.Decided, d);
                }
            }
            foreach (Payment p in site.Payments)
            {
                p.When = p.When.Add(d);
            }
            foreach (DayPass p in site.Passes)
            {
                p.Day = p.Day.Add(d);
                p.Bought = p.Bought.Add(d);
            }
            foreach (Visit v in site.Visits)
            {
                v.In = v.In.Add(d);
                v.Out = Opt(v.Out, d);
            }
            foreach (PointEntry p in site.Points)
            {
                p.When = p.When.Add(d);
            }
            foreach (Message m in site.Messages)
            {
                m.Created = m.Created.Add(d);
                foreach (Note n in m.Notes)
                {
                    n.When = n.When.Add(d);
                }
            }
            foreach (MediaItem m in site.Media)
            {
                m.When = m.When.Add(d);
            }
            foreach (Album a in site.Albums)
            {
                a.Date = a.Date.Add(d);
            }
            foreach (PageDoc p in site.Pages.Values)
            {
                if (p.Saved != default)
                {
                    p.Saved = p.Saved.Add(d);
                }
                foreach (PageVersion v in p.Versions)
                {
                    v.When = v.When.Add(d);
                }
            }
            site.Anchor = site.Anchor.Add(d);
        }

        public static Task<List<Staff>> LoadStaff(string sbx)
        {
            return Read<List<Staff>>("staff", sbx);
        }

        public static Task SaveStaff(List<Staff> staff, string sbx)
        {
            return Write("staff", sbx, staff);
        }

        public static async Task<List<Activity>> LoadActivity(string sbx)
        {
            List<Activity> list = await Read<List<Activity>>("activity", sbx);
            if (Demo && sbx == string.Empty)
            {
                SiteData raw = await Read<SiteData>("site", string.Empty);
                TimeSpan d = Weeks(raw);
                foreach (Activity a in list)
                {
                    a.When = a.When.Add(d);
                }
            }
            return list;
        }

        public static async Task Log(string sbx, string user, string action, string target, bool ok)
        {
            if (Demo && sbx == string.Empty)
            {
                return;
            }
            await LogGate.WaitAsync();
            try
            {
                List<Activity> list = await Read<List<Activity>>("activity", sbx);
                list.Insert(0, new Activity { When = DateTime.UtcNow, User = user, Action = action, Target = target.Length > 120 ? target.Substring(0, 120) : target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write("activity", sbx, list);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static async Task<string> NewSandbox()
        {
            Sweep();
            string id = Guid.NewGuid().ToString("N");
            string dir = SandboxDir(id);
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "made"), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            SiteData site = await Read<SiteData>("site", string.Empty);
            TimeSpan d = Weeks(site);
            Shift(site, d);
            Fresh(site);
            await Write("site", id, site);
            List<Activity> acts = await Read<List<Activity>>("activity", string.Empty);
            foreach (Activity a in acts)
            {
                a.When = a.When.Add(d);
            }
            await Write("activity", id, acts);
            string staff = FileOf("staff", string.Empty);
            if (File.Exists(staff))
            {
                File.Copy(staff, FileOf("staff", id), true);
            }
            return id;
        }

        public static void Sweep()
        {
            DateTime limit = DateTime.UtcNow.AddMinutes(-SandboxMinutes);
            string boxes = Path.Combine(Root, "temp", "sandbox");
            string uploads = Path.Combine(Root, "images", "uploads", "sandbox");
            try
            {
                if (Directory.Exists(boxes))
                {
                    foreach (string dir in Directory.GetDirectories(boxes))
                    {
                        if (Made(Path.GetFileName(dir)) < limit)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
                if (Directory.Exists(uploads))
                {
                    foreach (string dir in Directory.GetDirectories(uploads))
                    {
                        if (!Directory.Exists(SandboxDir(Path.GetFileName(dir))))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static TimeZoneInfo Zone(string id)
        {
            return Zones.GetOrAdd(id ?? string.Empty, k =>
            {
                try
                {
                    return k == string.Empty ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(k);
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.Utc;
                }
                catch (InvalidTimeZoneException)
                {
                    return TimeZoneInfo.Utc;
                }
            });
        }

        public static bool KnownZone(string id)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        public static DateTime Now(SiteData s)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone(s.Settings.TimeZone)), DateTimeKind.Unspecified);
        }

        public static DateTime Today(SiteData s)
        {
            return Now(s).Date;
        }

        public static DateTime Local(SiteData s, DateTime utc)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(s.Settings.TimeZone)), DateTimeKind.Unspecified);
        }

        public static DateTime Utc(SiteData s, DateTime local)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone(s.Settings.TimeZone));
            }
            catch (ArgumentException)
            {
                return DateTime.SpecifyKind(local.AddHours(1), DateTimeKind.Utc);
            }
        }

        public static TimeSpan? Hm(string? v)
        {
            return TimeSpan.TryParseExact((v ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan t) && t >= TimeSpan.Zero && t <= TimeSpan.FromHours(24) ? t : null;
        }

        public static string NextId(IEnumerable<string> ids, string prefix, int start)
        {
            int max = start;
            foreach (string id in ids)
            {
                if (id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > max)
                {
                    max = n;
                }
            }
            return prefix + (max + 1).ToString(CultureInfo.InvariantCulture);
        }
    }
}
