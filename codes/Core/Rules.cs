using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sport.Models;

namespace Sport.Core
{
    public sealed class Gate
    {
        public bool Ok { get; set; }
        public string Mode { get; set; } = "register";
        public string Why { get; set; } = string.Empty;
        public bool EarlyNow { get; set; }
    }

    public sealed class Quote
    {
        public decimal Price { get; set; }
        public string Tier { get; set; } = "bronze";
        public decimal OffPct { get; set; }
        public decimal Discount { get; set; }
        public int MaxPoints { get; set; }
        public int Points { get; set; }
        public decimal PointsOff { get; set; }
        public decimal Total { get; set; }
    }

    public static class Rules
    {
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static readonly (string Key, string Name)[] Tiers = { ("bronze", "Bronze"), ("silver", "Silver"), ("gold", "Gold"), ("diamond", "Diamond") };

        public static readonly (string Key, string Name)[] Levels = { ("all", "All levels"), ("beginner", "Beginner"), ("intermediate", "Intermediate"), ("advanced", "Advanced") };

        public static readonly (string Key, string Name)[] States = { ("open", "Open"), ("few", "Few seats"), ("full", "Full, waitlist"), ("progress", "In progress"), ("completed", "Completed"), ("cancelled", "Cancelled"), ("draft", "Draft") };

        public static readonly (string Key, string Name)[] RegStatuses = { ("confirmed", "Confirmed"), ("pending", "Pay at desk"), ("offered", "Seat offered"), ("waitlist", "Waitlist"), ("cancelled", "Cancelled"), ("expired", "Expired") };

        public static readonly (string Key, string Name)[] Relations = { ("child", "Child"), ("partner", "Partner"), ("parent", "Parent"), ("other", "Other") };

        public static string TierOf(Settings s, int lifetime)
        {
            return lifetime >= s.DiamondAt ? "diamond" : lifetime >= s.GoldAt ? "gold" : lifetime >= s.SilverAt ? "silver" : "bronze";
        }

        public static int Rank(string tier)
        {
            return tier switch { "silver" => 1, "gold" => 2, "diamond" => 3, _ => 0 };
        }

        public static string TierName(string tier)
        {
            return Ui.NameOf(Tiers, tier);
        }

        public static int TierAt(Settings s, string tier)
        {
            return tier switch { "silver" => s.SilverAt, "gold" => s.GoldAt, "diamond" => s.DiamondAt, _ => 0 };
        }

        public static decimal TierOff(Settings s, string tier)
        {
            return tier switch { "silver" => s.OffSilver, "gold" => s.OffGold, "diamond" => s.OffDiamond, _ => s.OffBronze };
        }

        public static bool Early(Settings s, string tier)
        {
            return s.EarlyHours > 0 && Rank(tier) >= Rank(s.EarlyFrom) && Rank(s.EarlyFrom) > 0;
        }

        public static (string Tier, int At) NextTier(Settings s, int lifetime)
        {
            string cur = TierOf(s, lifetime);
            return cur switch
            {
                "bronze" => ("silver", s.SilverAt),
                "silver" => ("gold", s.GoldAt),
                "gold" => ("diamond", s.DiamondAt),
                _ => (string.Empty, 0)
            };
        }

        public static bool IsMember(ClubMember? m)
        {
            return m != null && m.Type == "member";
        }

        public static string FullName(ClubMember m)
        {
            return (m.First + " " + m.Last).Trim();
        }

        public static DateTime Start(SportClass k)
        {
            return k.Dates.Count > 0 ? k.Dates[0] : k.First;
        }

        public static DateTime End(SportClass k)
        {
            return (k.Dates.Count > 0 ? k.Dates[^1] : k.First).AddMinutes(k.Minutes);
        }

        public static List<DateTime> Schedule(DateTime first, IEnumerable<int> days, string start, int count)
        {
            List<DateTime> list = new List<DateTime>();
            HashSet<int> set = days.Where(d => d >= 0 && d <= 6).ToHashSet();
            TimeSpan t = Store.Hm(start) ?? TimeSpan.FromHours(18);
            if (set.Count == 0 || count <= 0)
            {
                return list;
            }
            DateTime d0 = first.Date;
            for (int i = 0; i < 800 && list.Count < Math.Min(count, 60); i++)
            {
                DateTime d = d0.AddDays(i);
                if (set.Contains((int)d.DayOfWeek))
                {
                    list.Add(d.Add(t));
                }
            }
            return list;
        }

        public static bool Holds(Registration r)
        {
            return r.Status is "confirmed" or "pending" or "offered";
        }

        public static int Taken(SiteData site, SportClass k)
        {
            return site.Registrations.Count(r => r.Class == k.Id && Holds(r));
        }

        public static int Waiting(SiteData site, SportClass k)
        {
            return site.Registrations.Count(r => r.Class == k.Id && r.Status == "waitlist");
        }

        public static int Left(SiteData site, SportClass k)
        {
            return Math.Max(0, k.Capacity - Taken(site, k));
        }

        public static string State(SiteData site, SportClass k, DateTime now)
        {
            if (k.Status == "cancelled")
            {
                return "cancelled";
            }
            if (now >= End(k))
            {
                return "completed";
            }
            if (now >= Start(k))
            {
                return "progress";
            }
            if (!k.Published)
            {
                return "draft";
            }
            int left = Left(site, k);
            if (left <= 0)
            {
                return "full";
            }
            return left <= Math.Max(1, site.Settings.FewSeats) ? "few" : "open";
        }

        public static string StateName(string key)
        {
            return Ui.NameOf(States, key);
        }

        public static int Age(DateTime born, DateTime at)
        {
            int a = at.Year - born.Year;
            if (at.Date < born.Date.AddYears(a))
            {
                a--;
            }
            return Math.Max(0, a);
        }

        public static string AgeRule(SportClass k)
        {
            if (k.AgeMin <= 0 && k.AgeMax >= 99)
            {
                return "All ages";
            }
            if (k.AgeMax >= 99)
            {
                return (k.AgeMin >= 18 ? "Adults " : "Ages ") + k.AgeMin + "+";
            }
            return "Ages " + k.AgeMin + "&ndash;" + k.AgeMax;
        }

        public static bool AgeFits(SportClass k, DateTime born)
        {
            int a = Age(born, Start(k));
            return a >= k.AgeMin && a <= k.AgeMax;
        }

        public static Gate CanJoin(SiteData site, SportClass k, ClubMember? m, DateTime now)
        {
            string st = State(site, k, now);
            Settings s = site.Settings;
            if (st == "cancelled")
            {
                return new Gate { Why = "This class was cancelled." };
            }
            if (st == "completed")
            {
                return new Gate { Why = "This class has finished." };
            }
            if (st == "progress")
            {
                return new Gate { Why = "This class has already started. Registration is closed." };
            }
            if (st == "draft")
            {
                return new Gate { Why = "This class is not open yet." };
            }
            Gate g = new Gate { Ok = true, Mode = st == "full" ? "waitlist" : "register" };
            if (k.Opens != null && now < k.Opens.Value)
            {
                string tier = IsMember(m) ? TierOf(s, m!.Lifetime) : string.Empty;
                if (tier != string.Empty && Early(s, tier) && now >= k.Opens.Value.AddHours(-s.EarlyHours))
                {
                    g.EarlyNow = true;
                    return g;
                }
                return new Gate { Why = "Registration opens " + Ui.When(site, k.Opens.Value) + "." + (s.EarlyHours > 0 && Rank(s.EarlyFrom) > 0 ? " " + TierName(s.EarlyFrom) + (s.EarlyFrom == "diamond" ? string.Empty : " and higher") + " members can register " + s.EarlyHours + " hours early." : string.Empty) };
            }
            return g;
        }

        public static Quote Price(SiteData site, SportClass k, ClubMember m, int points)
        {
            Settings s = site.Settings;
            Quote q = new Quote { Price = k.Price };
            q.Tier = IsMember(m) ? TierOf(s, m.Lifetime) : "bronze";
            q.OffPct = IsMember(m) ? TierOff(s, q.Tier) : 0m;
            q.Discount = Math.Round(k.Price * q.OffPct / 100m, 2, MidpointRounding.AwayFromZero);
            decimal after = k.Price - q.Discount;
            int ppd = Math.Max(1, s.PointsPerDollar);
            int capDollars = (int)Math.Floor(after * Math.Clamp(s.PointsMaxPct, 0, 100) / 100m);
            q.MaxPoints = IsMember(m) ? Math.Min(m.Balance / ppd, capDollars) * ppd : 0;
            q.Points = Math.Clamp(points / ppd * ppd, 0, q.MaxPoints);
            q.PointsOff = q.Points / ppd;
            q.Total = Math.Max(0m, after - q.PointsOff);
            return q;
        }

        public static int EarnFor(Settings s, decimal amount)
        {
            return (int)Math.Floor(Math.Max(0m, amount)) * Math.Max(0, s.PerDollar);
        }

        public static (int Pct, bool Self, string Rule) Policy(SiteData site, SportClass k, DateTime now)
        {
            Settings s = site.Settings;
            if (k.Status == "cancelled")
            {
                return (100, true, "The class was cancelled.");
            }
            double hours = (Start(k) - now).TotalHours;
            if (hours >= s.FullRefundHours)
            {
                return (100, true, "More than " + s.FullRefundHours + " hours before the first session: full refund.");
            }
            if (hours >= s.CancelHours)
            {
                return (s.LatePct, true, "Within " + s.FullRefundHours + " hours of the first session: " + s.LatePct + "% refund.");
            }
            if (hours > 0)
            {
                return (s.LatePct, false, "Less than " + s.CancelHours + " hours before the first session: online cancellation is closed. Ask for a refund and the front desk will decide.");
            }
            return (s.StartedPct, false, "The class has started: " + (s.StartedPct > 0 ? s.StartedPct + "% refund" : "no automatic refund") + ". You can still ask the front desk.");
        }

        public static string NewCode(SiteData site)
        {
            HashSet<string> used = site.Registrations.Select(r => r.Code).Concat(site.Passes.Select(p => p.Code)).ToHashSet(StringComparer.Ordinal);
            while (true)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 6; i++)
                {
                    sb.Append(CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]);
                }
                string c = sb.ToString();
                if (!used.Contains(c))
                {
                    return c;
                }
            }
        }

        public static string CleanCode(string? v)
        {
            return new string((v ?? string.Empty).ToUpperInvariant().Where(ch => CodeChars.Contains(ch)).Take(6).ToArray());
        }

        public static void AddPoints(SiteData site, ClubMember m, int pts, string kind, string reg, string note, string by, DateTime when)
        {
            if (pts == 0 || !IsMember(m))
            {
                return;
            }
            site.Points.Add(new PointEntry { Id = Store.NextId(site.Points.Select(x => x.Id), "T-", 9000), Member = m.Id, When = when, Points = pts, Kind = kind, Reg = reg, Note = note, By = by });
            if (kind is "pay" or "session" or "refund" or "adjust" or "bonus")
            {
                m.Lifetime = Math.Max(0, m.Lifetime + pts);
            }
            m.Balance = Math.Max(0, m.Balance + pts);
        }

        public static Payment Charge(SiteData site, Registration? r, DayPass? pass, string member, decimal amount, string method, string brand, string last4, string by, DateTime when, string note)
        {
            Payment p = new Payment { Id = Store.NextId(site.Payments.Select(x => x.Id), "P-", 5000), Kind = "charge", Reg = r?.Id ?? string.Empty, Pass = pass?.Id ?? string.Empty, Member = member, Amount = amount, Method = method, Brand = brand, Last4 = last4, Status = "paid", When = when, By = by, Note = note };
            site.Payments.Add(p);
            return p;
        }

        public static void Confirm(SiteData site, Registration r, string method, string brand, string last4, string by, DateTime when)
        {
            ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
            if (r.Total > 0)
            {
                Charge(site, r, null, r.Member, r.Total, method, brand, last4, by, when, string.Empty);
            }
            r.Paid = r.Total;
            r.Method = method;
            r.Status = "confirmed";
            r.Confirmed = when;
            r.HoldExp = null;
            r.OfferExp = null;
            if (m != null)
            {
                AddPoints(site, m, EarnFor(site.Settings, r.Total), "pay", r.Id, "Paid " + r.Code, by, when);
            }
        }

        public static decimal Refundable(Registration r)
        {
            return Math.Max(0m, r.Paid - r.Refunded);
        }

        public static void Refund(SiteData site, Registration r, decimal amount, string by, DateTime when, string note)
        {
            decimal amt = Math.Round(Math.Clamp(amount, 0m, Refundable(r)), 2);
            ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
            if (amt > 0)
            {
                Payment? ch = site.Payments.Where(p => p.Reg == r.Id && p.Kind == "charge" && p.Status == "paid").OrderByDescending(p => p.When).FirstOrDefault();
                site.Payments.Add(new Payment { Id = Store.NextId(site.Payments.Select(x => x.Id), "P-", 5000), Kind = "refund", Reg = r.Id, Member = r.Member, Amount = amt, Method = ch?.Method ?? r.Method, Brand = ch?.Brand ?? string.Empty, Last4 = ch?.Last4 ?? string.Empty, Status = "paid", When = when, By = by, Note = note, Of = ch?.Id ?? string.Empty });
                r.Refunded += amt;
                if (m != null)
                {
                    int earned = site.Points.Where(x => x.Reg == r.Id && x.Kind is "pay" or "refund").Sum(x => x.Points);
                    int back = Math.Min(EarnFor(site.Settings, amt), Math.Max(0, earned));
                    AddPoints(site, m, -back, "refund", r.Id, "Refund " + r.Code, by, when);
                }
            }
            if (m != null && r.Points > 0)
            {
                decimal share = r.Total > 0 ? (r.Paid > 0 ? amt / r.Paid : 1m) : 1m;
                int returned = site.Points.Where(x => x.Reg == r.Id && x.Kind == "return").Sum(x => x.Points);
                int give = Math.Min(r.Points - returned, (int)Math.Round(r.Points * share, MidpointRounding.AwayFromZero));
                if (give > 0)
                {
                    AddPoints(site, m, give, "return", r.Id, "Points back " + r.Code, by, when);
                }
            }
        }

        public static void EndReg(SiteData site, Registration r, string status, decimal refund, string by, DateTime when, string note)
        {
            if (r.Paid > 0)
            {
                Refund(site, r, refund, by, when, note);
            }
            else if (r.Points > 0)
            {
                ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
                int returned = site.Points.Where(x => x.Reg == r.Id && x.Kind == "return").Sum(x => x.Points);
                if (m != null && r.Points - returned > 0)
                {
                    AddPoints(site, m, r.Points - returned, "return", r.Id, "Points back " + r.Code, by, when);
                }
            }
            r.Status = status;
            r.Ended = when;
            r.EndedBy = by;
            r.EndNote = note;
            r.HoldExp = null;
            r.OfferExp = null;
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            if (k != null)
            {
                OfferNext(site, k, when);
            }
        }

        public static List<Registration> OfferNext(SiteData site, SportClass k, DateTime when)
        {
            List<Registration> made = new List<Registration>();
            if (k.Status == "cancelled")
            {
                return made;
            }
            DateTime startUtc = Store.Utc(site, Start(k));
            if (when >= startUtc)
            {
                return made;
            }
            while (Left(site, k) > 0)
            {
                Registration? next = site.Registrations.Where(r => r.Class == k.Id && r.Status == "waitlist").OrderBy(r => r.Created).FirstOrDefault();
                if (next == null)
                {
                    break;
                }
                next.Status = "offered";
                next.Offered = when;
                DateTime exp = when.AddHours(Math.Max(1, site.Settings.OfferHours));
                next.OfferExp = exp > startUtc ? startUtc : exp;
                made.Add(next);
            }
            return made;
        }

        public static void Tidy(SiteData site, DateTime nowUtc)
        {
            for (int guard = 0; guard < 1000; guard++)
            {
                Registration? x = site.Registrations.Where(r => (r.Status == "offered" && r.OfferExp != null && r.OfferExp <= nowUtc) || (r.Status == "pending" && r.HoldExp != null && r.HoldExp <= nowUtc)).OrderBy(r => r.Status == "offered" ? r.OfferExp : r.HoldExp).FirstOrDefault();
                if (x == null)
                {
                    return;
                }
                bool offer = x.Status == "offered";
                DateTime t = (offer ? x.OfferExp : x.HoldExp) ?? nowUtc;
                EndReg(site, x, "expired", 0m, "system", t, offer ? "The seat offer was not paid in time." : "Not paid at the front desk in time.");
            }
        }

        public static (bool Cancel, bool Ask, string Text, decimal Amount) Options(SiteData site, Registration r, DateTime nowUtc)
        {
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            if (k == null)
            {
                return (false, false, string.Empty, 0m);
            }
            DateTime now = Store.Local(site, nowUtc);
            bool started = now >= Start(k);
            switch (r.Status)
            {
                case "waitlist":
                    return (true, false, "You can leave the waitlist at any time.", 0m);
                case "offered":
                    return (true, false, "If you don't want the seat, decline it and it goes to the next person.", 0m);
                case "pending":
                    return started ? (false, false, "The class has started. Talk to the front desk.", 0m) : (true, false, "Nothing has been charged yet, so you can cancel for free.", 0m);
                case "confirmed":
                    (int pct, bool self, string rule) = Policy(site, k, now);
                    decimal amt = Math.Round(Refundable(r) * pct / 100m, 2);
                    bool open = r.Ask != null && r.Ask.Status == "open";
                    if (open)
                    {
                        return (false, false, "Your refund request is with the front desk.", 0m);
                    }
                    if (self)
                    {
                        return (true, false, rule + (amt > 0 ? " You would get " + Ui.Plain(amt) + " back." : string.Empty), amt);
                    }
                    return (false, Refundable(r) > 0 && (r.Ask == null || r.Ask.Status != "declined"), rule, amt);
            }
            return (false, false, string.Empty, 0m);
        }

        public static (bool Ok, string Msg) CancelOnline(SiteData site, Registration r, DateTime nowUtc)
        {
            (bool cancel, bool ask, string text, decimal amt) = Options(site, r, nowUtc);
            if (!cancel)
            {
                return (false, text == string.Empty ? "This registration can't be cancelled online." : text);
            }
            switch (r.Status)
            {
                case "waitlist":
                    EndReg(site, r, "cancelled", 0m, "web", nowUtc, "Left the waitlist.");
                    return (true, "You left the waitlist.");
                case "offered":
                    EndReg(site, r, "cancelled", 0m, "web", nowUtc, "Declined the seat offer.");
                    return (true, "You declined the seat. It goes to the next person on the list.");
                case "pending":
                    EndReg(site, r, "cancelled", 0m, "web", nowUtc, "Cancelled before paying.");
                    return (true, "Cancelled. Nothing was charged.");
            }
            int pct = Refundable(r) > 0 ? (int)Math.Round(amt * 100m / Refundable(r)) : 0;
            EndReg(site, r, "cancelled", amt, "web", nowUtc, "Cancelled online. " + pct + "% refund.");
            return (true, amt > 0 ? "Cancelled. " + Ui.Plain(amt) + (r.Method == "desk" ? " is ready for you at the front desk." : " goes back to your card.") : "Cancelled.");
        }

        public static (bool Ok, string Msg) AskRefund(SiteData site, Registration r, string reason, DateTime nowUtc)
        {
            (bool cancel, bool ask, string text, decimal amt) = Options(site, r, nowUtc);
            if (!ask)
            {
                return (false, r.Ask != null && r.Ask.Status == "declined" ? "The front desk already answered a request for this registration." : "A refund request isn't needed here.");
            }
            if (reason.Length < 5)
            {
                return (false, "Tell us briefly why you need a refund.");
            }
            r.Ask = new RefundAsk { When = nowUtc, Reason = reason, Status = "open", Amount = amt };
            return (true, "Sent. The front desk will answer within one working day.");
        }

        public static string Receipt(SiteData site, Registration r, string p)
        {
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
            Settings s = site.Settings;
            List<Payment> pays = site.Payments.Where(x => x.Reg == r.Id && x.Status == "paid").OrderBy(x => x.When).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-receipt\" id=\"" + p + "-rcpt-" + Ui.Attr(r.Code) + "\"><div class=\"" + p + "-rhead\"><div><b>" + Ui.Enc(s.Name) + "</b><small>" + Ui.Enc(s.Address) + "<br>" + Ui.Enc(s.Phone) + " &middot; " + Ui.Enc(s.Email) + "</small></div><div class=\"" + p + "-rno\"><span>Receipt</span><b>" + Ui.Enc(r.Code) + "</b><small>" + Ui.Day(Store.Local(site, r.Created)) + "</small></div></div>");
            sb.Append("<dl class=\"" + p + "-rdl\"><dt>Account</dt><dd>" + Ui.Enc(m != null ? FullName(m) : "&ndash;") + (m != null ? " &middot; " + Ui.Enc(m.Email) : string.Empty) + "</dd><dt>Participant</dt><dd>" + Ui.Enc(r.Name) + "</dd><dt>Class</dt><dd>" + Ui.Enc(k?.Title ?? r.Class) + "</dd>");
            if (k != null)
            {
                sb.Append("<dt>Schedule</dt><dd>" + Ui.ClassWhen(site, k) + "<br>" + Ui.Range(k) + " &middot; " + Ui.Plural(k.Dates.Count, "session", "sessions") + "</dd>");
            }
            sb.Append("<dt>Status</dt><dd>" + Ui.Enc(Ui.NameOf(RegStatuses, r.Status)) + "</dd></dl><div class=\"" + p + "-rlines\"><div><span>Class fee</span><b>" + Ui.Money(r.Price, true) + "</b></div>");
            if (r.Discount > 0)
            {
                sb.Append("<div><span>" + Ui.Enc(TierName(r.Tier)) + " member discount</span><b>&minus;" + Ui.Money(r.Discount, true) + "</b></div>");
            }
            if (r.PointsOff > 0)
            {
                sb.Append("<div><span>" + Ui.Num(r.Points) + " points</span><b>&minus;" + Ui.Money(r.PointsOff, true) + "</b></div>");
            }
            sb.Append("<div class=\"" + p + "-rtot\"><span>Total</span><b>" + Ui.Money(r.Total, true) + "</b></div>");
            foreach (Payment x in pays)
            {
                string how = x.Method == "card" ? Ui.Enc(x.Brand) + " ending " + Ui.Enc(x.Last4) : "Front desk";
                sb.Append("<div class=\"" + p + "-r" + (x.Kind == "refund" ? "ref" : "pay") + "\"><span>" + (x.Kind == "refund" ? "Refund" : "Paid") + " &middot; " + Ui.Day(Store.Local(site, x.When)) + " &middot; " + how + "</span><b>" + (x.Kind == "refund" ? "&minus;" : string.Empty) + Ui.Money(x.Amount, true) + "</b></div>");
            }
            decimal due = r.Status is "pending" or "offered" ? r.Total - r.Paid : 0m;
            sb.Append("<div class=\"" + p + "-rtot\"><span>" + (due > 0 ? "Due at the front desk" : "Net paid") + "</span><b>" + Ui.Money(due > 0 ? due : r.Paid - r.Refunded, true) + "</b></div></div><p class=\"" + p + "-rfoot\">Demo receipt. No real payment was taken. Keep the code " + Ui.Enc(r.Code) + " to look up this registration.</p></div>");
            return sb.ToString();
        }

        public static string PassReceipt(SiteData site, DayPass d, string p)
        {
            ClubMember? m = site.Members.FirstOrDefault(x => x.Id == d.Member);
            Settings s = site.Settings;
            Payment? pay = site.Payments.FirstOrDefault(x => x.Pass == d.Id && x.Kind == "charge" && x.Status == "paid");
            Payment? refund = site.Payments.FirstOrDefault(x => x.Pass == d.Id && x.Kind == "refund");
            string how = pay == null ? "Front desk" : pay.Method == "card" ? Ui.Enc(pay.Brand) + " ending " + Ui.Enc(pay.Last4) : "Front desk";
            return "<div class=\"" + p + "-receipt\" id=\"" + p + "-rcpt-" + Ui.Attr(d.Code) + "\"><div class=\"" + p + "-rhead\"><div><b>" + Ui.Enc(s.Name) + "</b><small>" + Ui.Enc(s.Address) + "<br>" + Ui.Enc(s.Phone) + " &middot; " + Ui.Enc(s.Email) + "</small></div><div class=\"" + p + "-rno\"><span>" + Ui.Enc(s.PassName) + "</span><b>" + Ui.Enc(d.Code) + "</b><small>" + Ui.Day(Store.Local(site, d.Bought)) + "</small></div></div><dl class=\"" + p + "-rdl\"><dt>Name</dt><dd>" + Ui.Enc(m != null ? FullName(m) : "&ndash;") + "</dd><dt>Valid on</dt><dd>" + Ui.LongDay(d.Day) + "</dd><dt>Status</dt><dd>" + Ui.Enc(d.Status == "used" ? "Used" : d.Status == "refunded" ? "Refunded" : "Valid") + "</dd></dl><div class=\"" + p + "-rlines\"><div class=\"" + p + "-rtot\"><span>" + Ui.Enc(s.PassName) + "</span><b>" + Ui.Money(d.Price, true) + "</b></div><div class=\"" + p + "-rpay\"><span>Paid &middot; " + how + "</span><b>" + Ui.Money(pay?.Amount ?? 0m, true) + "</b></div>" + (refund != null ? "<div class=\"" + p + "-rref\"><span>Refund &middot; " + Ui.Day(Store.Local(site, refund.When)) + "</span><b>&minus;" + Ui.Money(refund.Amount, true) + "</b></div>" : string.Empty) + "</div><p class=\"" + p + "-rfoot\">Demo receipt. No real payment was taken. Show the code " + Ui.Enc(d.Code) + " at the front desk.</p></div>";
        }
    }
}
