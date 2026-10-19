using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sport.Models;

namespace Sport.Core
{
    public static class Ui
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public static readonly string[] DayShort = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public static readonly (string Key, string Name)[] MsgStatuses = { ("new", "New"), ("replied", "Replied"), ("closed", "Closed") };

        public static readonly (string Key, string Name)[] Topics = { ("classes", "Classes and registration"), ("membership", "Membership and points"), ("payments", "Payments and refunds"), ("passes", "Day passes"), ("facilities", "Facilities and hours"), ("other", "Something else") };

        public static string Enc(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Attr(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "&quot;").Replace("<", "\\x3c").Replace("\n", " ").Replace("\r", string.Empty);
        }

        public static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        public static string OneLine(string? value, int max)
        {
            return Regex.Replace(Clip(value, max), "\\s+", " ").Trim();
        }

        public static string Plural(int n, string one, string many)
        {
            return n.ToString("#,##0", Inv) + " " + (n == 1 ? one : many);
        }

        public static string Iso(DateTime d)
        {
            return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
        }

        public static string Ago(DateTime d)
        {
            TimeSpan t = DateTime.UtcNow - d.ToUniversalTime();
            if (t.TotalMinutes < 1)
            {
                return "Just now";
            }
            if (t.TotalMinutes < 60)
            {
                int m = (int)t.TotalMinutes;
                return m + (m == 1 ? " min ago" : " mins ago");
            }
            if (t.TotalHours < 24)
            {
                int h = (int)t.TotalHours;
                return h + (h == 1 ? " hour ago" : " hours ago");
            }
            return d.ToUniversalTime().ToString("d MMM yyyy", Inv);
        }

        public static string Time(DateTime d, string mode = "ago")
        {
            DateTime u = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return "<time datetime=\"" + Iso(u) + "\" data-t=\"" + mode + "\">" + (mode == "ago" ? Ago(u) : u.ToString("d MMMM yyyy, HH:mm 'UTC'", Inv)) + "</time>";
        }

        public static string Initials(string name)
        {
            string[] parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        public static string Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? string.Empty)
            {
                h = (h * 31 + c) % 360;
            }
            return h.ToString(Inv);
        }

        public static string Avatar(string p, Staff? u)
        {
            string name = u?.Name ?? "Staff";
            return "<span class=\"" + p + "-av\" style=\"--h:" + Hue(u?.Id ?? name) + "\" aria-hidden=\"true\">" + Enc(Initials(name)) + "</span>";
        }

        public static string MemberAv(string p, ClubMember m)
        {
            return "<span class=\"" + p + "-av\" style=\"--h:" + Hue(m.Id) + "\" aria-hidden=\"true\">" + Enc(Initials(Rules.FullName(m))) + "</span>";
        }

        public static string Pager(string p, int page, int size, int total, string one, string many, string js)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            int pages = Math.Max(1, (total + size - 1) / size);
            return "<div class=\"" + p + "-pager\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page - 1) + ")\"" + (page <= 1 ? " disabled" : string.Empty) + ">&larr; Previous</button><span>Page " + page + " of " + pages + " &middot; " + Plural(total, one, many) + "</span><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page + 1) + ")\"" + (page >= pages ? " disabled" : string.Empty) + ">Next &rarr;</button></div>";
        }

        public static int PageOf(string value, int size, int total)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            return Math.Clamp(int.TryParse(value, out int n) ? n : 1, 1, pages);
        }

        public static Dictionary<string, Staff> StaffMap(List<Staff> list)
        {
            Dictionary<string, Staff> d = new Dictionary<string, Staff>();
            foreach (Staff s in list)
            {
                d[s.Id] = s;
            }
            return d;
        }

        public static string Money(decimal v, bool cents = false)
        {
            bool c = cents || v != Math.Floor(v);
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(c ? "#,##0.00" : "#,##0", Inv);
        }

        public static string Plain(decimal v)
        {
            return "$" + v.ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", Inv);
        }

        public static string Whole(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Round(Math.Abs(v), 0).ToString("#,##0", Inv);
        }

        public static string Num(int v)
        {
            return v.ToString("#,##0", Inv);
        }

        public static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd, MMM d, yyyy" : "ddd, MMM d", Inv);
        }

        public static string Short(DateTime d)
        {
            return d.ToString("MMM d", Inv);
        }

        public static string LongDay(DateTime d)
        {
            return d.ToString("dddd, MMMM d, yyyy", Inv);
        }

        public static string Ymd(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", Inv);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out DateTime d) ? DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified) : null;
        }

        public static DateTime? ParseStart(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd'T'HH:mm", Inv, DateTimeStyles.None, out DateTime d) ? DateTime.SpecifyKind(d, DateTimeKind.Unspecified) : null;
        }

        public static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", Inv);
        }

        public static string Hm(SiteData s, DateTime d)
        {
            return d.ToString(s.Settings.Clock24 ? "HH:mm" : "h:mm tt", Inv);
        }

        public static string HmText(SiteData s, string hm)
        {
            TimeSpan? t = Store.Hm(hm);
            return t == null ? string.Empty : Hm(s, DateTime.Today.Add(t.Value));
        }

        public static string When(SiteData s, DateTime local)
        {
            return local.ToString("ddd, MMM d", Inv) + " at " + Hm(s, local);
        }

        public static string Days(IEnumerable<int> days)
        {
            List<int> d = days.Distinct().OrderBy(x => x == 0 ? 7 : x).ToList();
            if (d.Count == 0)
            {
                return string.Empty;
            }
            if (d.Count == 1)
            {
                return DayNames[d[0]] + "s";
            }
            return string.Join(" &amp; ", d.Select(x => DayShort[x]));
        }

        public static string ClassWhen(SiteData s, SportClass k)
        {
            return Days(k.Days) + " &middot; " + HmText(s, k.Start) + " &middot; " + k.Minutes + " min";
        }

        public static string Range(SportClass k)
        {
            DateTime a = Rules.Start(k);
            DateTime b = Rules.End(k);
            return a.Year == b.Year ? a.ToString("MMM d", Inv) + " &ndash; " + b.ToString("MMM d, yyyy", Inv) : a.ToString("MMM d, yyyy", Inv) + " &ndash; " + b.ToString("MMM d, yyyy", Inv);
        }

        public static string Thumb(string full)
        {
            return full.Contains("/full/", StringComparison.Ordinal) ? full.Replace("/full/", "/thumbs/") : full;
        }

        public static string Pic(string src, string alt, string cls, bool thumb = false, bool eager = false)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            return "<img class=\"" + cls + "\" src=\"" + Attr(thumb ? Thumb(src) : src) + "\" alt=\"" + Attr(alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + " width=\"" + (thumb ? "640" : "1600") + "\" height=\"" + (thumb ? "427" : "1067") + "\">";
        }

        public static string Face(string src, string alt, string cls)
        {
            return src == string.Empty ? string.Empty : "<img class=\"" + cls + "\" src=\"" + Attr(src) + "\" alt=\"" + Attr(alt) + "\" loading=\"lazy\" width=\"480\" height=\"480\">";
        }

        public static string CleanPhone(string? v)
        {
            return Regex.Replace(Clip(v, 30), "[^0-9+()\\-. ]", string.Empty).Trim();
        }

        public static string Digits(string? v)
        {
            return Regex.Replace(v ?? string.Empty, "[^0-9]", string.Empty);
        }

        public static bool Email(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
        }

        public static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public static string Opts(IEnumerable<(string Value, string Text)> items, string cur)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string v, string t) in items)
            {
                sb.Append("<option value=\"" + Attr(v) + "\"" + (v == cur ? " selected" : string.Empty) + ">" + Enc(t) + "</option>");
            }
            return sb.ToString();
        }

        public static string DataList(string id, IEnumerable<string> values)
        {
            StringBuilder sb = new StringBuilder("<datalist id=\"" + id + "\">");
            foreach (string v in values.Where(x => x != string.Empty).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("<option value=\"" + Attr(v) + "\"></option>");
            }
            sb.Append("</datalist>");
            return sb.ToString();
        }

        public static string NameOf((string Key, string Name)[] list, string key)
        {
            foreach ((string k, string n) in list)
            {
                if (k == key)
                {
                    return n;
                }
            }
            return key;
        }

        public static string Pill(string p, (string Key, string Name)[] list, string key)
        {
            return "<span class=\"" + p + "-st " + p + "-st-" + key + "\">" + Enc(NameOf(list, key)) + "</span>";
        }

        public static string StatePill(string p, string state)
        {
            return "<span class=\"" + p + "-st " + p + "-st-" + state + "\">" + Enc(Rules.StateName(state)) + "</span>";
        }

        public static string TierPill(string p, string tier)
        {
            return "<span class=\"" + p + "-tier " + p + "-t-" + tier + "\">" + Enc(Rules.TierName(tier)) + "</span>";
        }

        public static string StaffName(Dictionary<string, Staff> staff, string id)
        {
            return id == "web" ? "Website" : id == "system" ? "System" : staff.TryGetValue(id, out Staff? s) ? s.Name : id == string.Empty ? "Nobody" : id;
        }

        public static (DateTime? From, DateTime? To) Period(DateTime today, string period, string from, string to, bool future = false)
        {
            return period switch
            {
                "today" => (today, today),
                "7" => future ? (today, today.AddDays(6)) : (today.AddDays(-6), today),
                "30" => future ? (today, today.AddDays(29)) : (today.AddDays(-29), today),
                "range" => (ParseDay(from), ParseDay(to)),
                _ => (null, null)
            };
        }

        public static bool InPeriod(DateTime d, DateTime? from, DateTime? to)
        {
            return (from == null || d.Date >= from.Value) && (to == null || d.Date <= to.Value);
        }

        public static string PeriodBar(string p, string js, string cur, string from, string to, bool future = false)
        {
            (string k, string n)[] chips = { ("today", "Today"), ("7", future ? "Next 7 days" : "7 days"), ("30", future ? "Next 30 days" : "30 days"), ("all", "All") };
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-period\"><div class=\"" + p + "-chips\">");
            foreach ((string k, string n) in chips)
            {
                sb.Append("<button type=\"button\" class=\"" + p + "-chip" + (k == cur ? " " + p + "-act" : string.Empty) + "\" onclick=\"" + js + "('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div><label class=\"" + p + "-pf\"><span>From</span><input type=\"date\" id=\"" + p + "-from\" value=\"" + Attr(cur == "range" ? from : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><label class=\"" + p + "-pf\"><span>To</span><input type=\"date\" id=\"" + p + "-to\" value=\"" + Attr(cur == "range" ? to : string.Empty) + "\" onchange=\"" + js + "('range')\"></label><input type=\"hidden\" id=\"" + p + "-per\" value=\"" + Attr(cur) + "\"></div>");
            return sb.ToString();
        }

        public static bool IsVideo(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "\\.(mp4|webm)$", RegexOptions.IgnoreCase);
        }

        public static bool IsDoc(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "\\.pdf$", RegexOptions.IgnoreCase);
        }

        public static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..") && File.Exists(Path.Combine(Store.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static bool SafeVideo(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(mp4|webm)$") && !src!.Contains("..") && File.Exists(Path.Combine(Store.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static bool SafeDoc(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.pdf$") && !src!.Contains("..") && File.Exists(Path.Combine(Store.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static bool SafeMedia(string src)
        {
            return SafeImage(src) || SafeVideo(src) || SafeDoc(src);
        }

        public static string Poster(SiteData site, string src)
        {
            if (!IsVideo(src) && !IsDoc(src))
            {
                return Thumb(src);
            }
            return site.Media.FirstOrDefault(m => m.Full == src)?.Thumb ?? string.Empty;
        }

        public static string Size(long bytes)
        {
            return bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.0", Inv) + " MB" : Math.Max(1, bytes / 1024).ToString(Inv) + " KB";
        }

        public static string Secs(int s)
        {
            return (s / 60).ToString(Inv) + ":" + (s % 60).ToString("00", Inv);
        }

        public static List<string> SplitLines(string? v, int max, int each)
        {
            return (v ?? string.Empty).Split('\n').Select(x => Clip(x, each)).Where(x => x != string.Empty).Take(max).ToList();
        }

        public static string Csv(string v)
        {
            string t = (v ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            if (Regex.IsMatch(t, "^[=+\\-@]"))
            {
                t = "'" + t;
            }
            return t.Contains(',') || t.Contains('"') || t.Contains('\'') ? "\"" + t.Replace("\"", "\"\"") + "\"" : t;
        }

        public static SportType? SportOf(SiteData s, string id)
        {
            return s.Sports.FirstOrDefault(x => x.Id == id);
        }

        public static string SportName(SiteData s, string id)
        {
            return SportOf(s, id)?.Name ?? id;
        }

        public static Coach? CoachOf(SiteData s, string id)
        {
            return s.Instructors.FirstOrDefault(x => x.Id == id);
        }

        public static string CoachName(SiteData s, string id)
        {
            return CoachOf(s, id)?.Name ?? "To be announced";
        }

        public static string ClassPic(SiteData s, SportClass k)
        {
            if (k.Image != string.Empty)
            {
                return k.Image;
            }
            return SportOf(s, k.Sport)?.Image ?? string.Empty;
        }

        public static bool Visible(SportClass k)
        {
            return k.Published;
        }
    }
}
