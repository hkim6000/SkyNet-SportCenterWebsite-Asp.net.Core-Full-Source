using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class Class : WebPage
    {
        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
            public string Nav { get; set; } = string.Empty;
        }

        private Dictionary<string, string>? qd;

        private string Q(string key, int max = 80)
        {
            if (qd != null)
            {
                return Ui.Clip(qd.TryGetValue(key, out string? v) ? v : string.Empty, max);
            }
            return Ui.Clip(QueryValue(key), max);
        }

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            View v = await Render(site);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_town}", Ui.Enc(site.Settings.Town))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_addr}", Ui.Enc(site.Settings.Address))
                .Replace("{plhd_phone}", Ui.Enc(site.Settings.Phone))
                .Replace("{plhd_tel}", Ui.Attr(Regex.Replace(site.Settings.Phone, "[^0-9+]", string.Empty)))
                .Replace("{plhd_email}", Ui.Enc(site.Settings.Email))
                .Replace("{plhd_today}", TodayLine(site))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--cd-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("cd", v.Nav, false))
                .Replace("{plhd_drawer}", Nav("cd", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("ClassJs.hydrated(false);");
                return response;
            }
            qd = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Ui.Clip(GetDataValue("qstr"), 600)))
            {
                qd[kv.Key] = kv.Value.ToString();
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = await Render(site);
            response.SetElementContents("cd-main", v.Main);
            response.SetElementContents("cd-bname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("cd-btown", Ui.Enc(site.Settings.Town));
            response.SetElementContents("cd-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("cd-copyname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("cd-ftag", Ui.Enc(site.Settings.Tagline));
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ClassJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "'," + Store.MinutesLeft(sbx) + ");");
            return response;
        }

        private static async Task<string> Writable(ApiResponse response, string requested)
        {
            if (!Store.Demo)
            {
                return string.Empty;
            }
            string sbx = Store.Sandbox(requested);
            if (sbx == string.Empty)
            {
                sbx = await Store.NewSandbox();
                response.ExecuteScript("ClassJs.sandboxed('" + sbx + "');");
            }
            return sbx;
        }

        private static string Readable(string requested)
        {
            return Store.Demo ? Store.Sandbox(requested) : string.Empty;
        }

        private async Task<(SiteData Site, string Sbx, global::Sport.Models.ClubMember? Me)> Context()
        {
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            string sbx = mc != null ? mc.Sbx : Readable(GetDataValue("sbx"));
            SiteData site = await Store.LoadSite(sbx);
            global::Sport.Models.ClubMember? me = mc == null ? null : site.Members.FirstOrDefault(x => x.Id == mc.Id && x.Type == "member" && x.Status == "active" && (Store.Demo || !x.Sample));
            return (site, sbx, me);
        }

        public async Task<ApiResponse> Who()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Context();
            if (me == null)
            {
                response.ExecuteScript("ClassJs.who('', '', '');");
                return response;
            }
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            response.ExecuteScript("ClassJs.who('" + Ui.Js(me.First) + "','" + tier + "','" + Rules.TierName(tier) + "');");
            return response;
        }

        private static readonly (string Href, string Key, string Name)[] Links = { ("Sports", "sports", "Sports"), ("Classes", "classes", "Classes"), ("Instructors", "coaches", "Instructors"), ("Media", "media", "Media Center"), ("About", "about", "About"), ("Contact", "contact", "Contact") };

        private static string Nav(string p, string on, bool drawer)
        {
            StringBuilder sb = new StringBuilder();
            if (drawer)
            {
                sb.Append("<a href=\"Home\"" + (on == "home" ? " class=\"" + p + "-on\"" : string.Empty) + ">Home</a>");
            }
            foreach ((string href, string k, string n) in Links)
            {
                sb.Append("<a href=\"" + href + "\"" + (k == on ? " class=\"" + p + "-on\" aria-current=\"page\"" : string.Empty) + ">" + Ui.Enc(n) + "</a>");
            }
            if (drawer)
            {
                sb.Append("<a href=\"Pass\"" + (on == "pass" ? " class=\"" + p + "-on\"" : string.Empty) + ">Day pass</a><a href=\"Member\"" + (on == "member" ? " class=\"" + p + "-on\"" : string.Empty) + ">Member area</a><a href=\"MyRegistration\"" + (on == "lookup" ? " class=\"" + p + "-on\"" : string.Empty) + ">Find my registration</a>");
            }
            return sb.ToString();
        }

        private static string TodayLine(SiteData site)
        {
            DateTime now = Store.Now(site);
            OpenDay? d = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)now.DayOfWeek);
            TimeSpan? from = d != null && d.Open ? Store.Hm(d.Start) : null;
            TimeSpan? to = d != null && d.Open ? Store.Hm(d.End) : null;
            if (from != null && to != null && now.TimeOfDay >= from.Value && now.TimeOfDay < to.Value)
            {
                return "<i></i>Open today until " + Ui.HmText(site, d!.End);
            }
            if (from != null && now.TimeOfDay < from.Value)
            {
                return "<i class=\"cd-shut\"></i>Opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"cd-shut\"></i>Closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"cd-shut\"></i>Closed";
        }

        private static string HoursRows(SiteData site, string p)
        {
            int today = (int)Store.Today(site).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? Ui.HmText(site, o.Start) + " &ndash; " + Ui.HmText(site, o.End) : "Closed";
                sb.Append("<span class=\"" + p + "-hrow" + (d == today ? " " + p + "-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        private static string ClassCard(SiteData site, SportClass k, DateTime now)
        {
            string st = Rules.State(site, k, now);
            int left = Rules.Left(site, k);
            string href = "Class?id=" + Uri.EscapeDataString(k.Id);
            string seats = st switch
            {
                "full" => Rules.Waiting(site, k) > 0 ? Rules.Waiting(site, k) + " on the waitlist" : "Join the waitlist",
                "completed" => "Finished",
                "cancelled" => "Cancelled",
                "progress" => "Started " + Ui.Short(Rules.Start(k)),
                _ => k.Opens != null && k.Opens.Value > now ? "Opens " + Ui.Short(k.Opens.Value) : Ui.Plural(left, "seat", "seats") + " left"
            };
            return "<article class=\"cd-kc\"><a class=\"cd-kc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) + "<span class=\"cd-kc-tag\">" + Ui.Enc(Ui.SportName(site, k.Sport)) + "</span><span class=\"cd-kc-st\">" + Ui.StatePill("cd", st) + "</span></a><div class=\"cd-kc-b\"><span class=\"cd-kc-t\"><a href=\"" + href + "\">" + Ui.Enc(k.Title) + "</a></span><span class=\"cd-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> " + Ui.ClassWhen(site, k) + "</span><span class=\"cd-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> " + Ui.Range(k) + "</span><span class=\"cd-meta\"><span class=\"cd-chipx\">" + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><span class=\"cd-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"cd-chipx\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span></span><span class=\"cd-kc-f\"><b>" + Ui.Money(k.Price) + "</b><small>" + Ui.Plural(k.Dates.Count, "session", "sessions") + " &middot; " + seats + "</small></span></div></article>";
        }

        private static int OpenCount(SiteData site, string sport, DateTime now)
        {
            return site.Classes.Count(k => k.Sport == sport && Rules.State(site, k, now) is "open" or "few" or "full");
        }

        private async Task<View> Render(SiteData site)
        {
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == Q("id", 20) && x.Published);
            if (k == null)
            {
                return new View { Title = "Class not found", Main = "<section class=\"cd-wrap cd-phead\"><div class=\"cd-crumb\"><a href=\"Classes\">Classes</a></div><h1>Class not found</h1><p class=\"cd-lead\">It may have been removed. <a class=\"cd-more\" href=\"Classes\">See the schedule</a></p></section>", Nav = "classes" };
            }
            DateTime now = Store.Now(site);
            Settings s = site.Settings;
            string st = Rules.State(site, k, now);
            SportType? sp = Ui.SportOf(site, k.Sport);
            global::Sport.Models.Coach? coach = Ui.CoachOf(site, k.Instructor);
            int taken = Rules.Taken(site, k);
            StringBuilder sb = new StringBuilder("<section class=\"cd-wrap cd-chead\"><div class=\"cd-crumb\"><a href=\"Classes\">Classes</a><span>/</span>" + (sp != null ? "<a href=\"Sport?id=" + Uri.EscapeDataString(sp.Id) + "\">" + Ui.Enc(sp.Name) + "</a><span>/</span>" : string.Empty) + "<span>" + Ui.Enc(k.Title) + "</span></div><div class=\"cd-ctop\"><div><span class=\"cd-kick\">" + Ui.Enc(sp?.Name ?? string.Empty) + " &middot; " + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><h1>" + Ui.Enc(k.Title) + "</h1><div class=\"cd-meta\">" + Ui.StatePill("cd", st) + "<span class=\"cd-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"cd-chipx\">" + Ui.Plural(k.Dates.Count, "session", "sessions") + "</span><span class=\"cd-chipx\">" + k.Minutes + " min each</span></div></div></div></section>");
            sb.Append("<section class=\"cd-wrap cd-cgrid\"><div class=\"cd-cmain\"><div class=\"cd-cpic\">" + Ui.Pic(Ui.ClassPic(site, k), k.Title, string.Empty, false, true) + "</div>");
            if (k.Status == "cancelled")
            {
                sb.Append("<div class=\"cd-bad-box\"><b>This class was cancelled.</b>" + Ui.Enc(k.CancelReason) + "</div>");
            }
            sb.Append("<div class=\"cd-facts\"><div><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg><span><b>" + Ui.ClassWhen(site, k) + "</b><small>Time and length</small></span></div><div><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg><span><b>" + Ui.Range(k) + "</b><small>First to last session</small></span></div><div><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg><span><b>" + Ui.Enc(k.Place) + "</b><small>" + Ui.Enc(s.Name) + "</small></span></div><div><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg><span><b>" + taken + " of " + k.Capacity + " seats taken</b><small>" + (Rules.Waiting(site, k) > 0 ? Rules.Waiting(site, k) + " on the waitlist" : "Small group") + "</small></span></div></div>");
            sb.Append("<div class=\"cd-ctext\"><h2>About this class</h2><div class=\"cd-rich\">" + k.Text + "</div>" + (k.Bring != string.Empty ? "<h3>What to bring</h3><p>" + Ui.Enc(k.Bring) + "</p>" : string.Empty) + "</div>");
            sb.Append("<div class=\"cd-sessions\"><h2>Sessions</h2><ol>");
            for (int i = 0; i < k.Dates.Count; i++)
            {
                DateTime d = k.Dates[i];
                bool done = d.AddMinutes(k.Minutes) <= now;
                sb.Append("<li class=\"" + (done ? "cd-done" : string.Empty) + "\"><span>" + (i + 1) + "</span><b>" + d.ToString("ddd, MMM d", Ui.Inv) + "</b><small>" + Ui.Hm(site, d) + " &ndash; " + Ui.Hm(site, d.AddMinutes(k.Minutes)) + "</small></li>");
            }
            sb.Append("</ol></div>");
            if (coach != null)
            {
                sb.Append("<a class=\"cd-coachrow\" href=\"Instructor?id=" + Uri.EscapeDataString(coach.Id) + "\">" + Ui.Face(coach.Photo, coach.Name, string.Empty) + "<span><small>Your coach</small><b>" + Ui.Enc(coach.Name) + "</b><em>" + Ui.Enc(coach.Title) + "</em><p>" + Ui.Enc(coach.Bio) + "</p></span></a>");
            }
            sb.Append("<div class=\"cd-policy\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 3l7 3v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6z\"/><path d=\"M9 12l2 2 4-4\"/></svg><div><h3>Cancellation and refunds</h3><ul><li>More than " + s.FullRefundHours + " hours before the first session: full refund.</li><li>Between " + s.FullRefundHours + " and " + s.CancelHours + " hours before: " + s.LatePct + "% refund.</li><li>Less than " + s.CancelHours + " hours before, or after the start: ask the front desk" + (s.StartedPct > 0 ? " (" + s.StartedPct + "% after the start)" : string.Empty) + ".</li><li>Waitlist offers must be paid within " + s.OfferHours + " hours.</li></ul></div></div>");
            sb.Append("</div><aside class=\"cd-side\"><div class=\"cd-pbox\" id=\"cd-panel\">" + Panel(site, k, null, now) + "</div></aside></section>");
            return new View { Title = k.Title, Desc = Ui.SportName(site, k.Sport) + " class: " + k.Title + ", " + Ui.NameOf(Rules.Levels, k.Level).ToLowerInvariant() + ", " + Rules.AgeRule(k).Replace("&ndash;", "-") + ".", Main = sb.ToString(), Nav = "classes" };
        }

        private static string PriceHead(SiteData site, SportClass k, DateTime now)
        {
            string st = Rules.State(site, k, now);
            int left = Rules.Left(site, k);
            string sub = st switch
            {
                "full" => "Full &middot; waitlist open",
                "progress" => "In progress",
                "completed" => "Finished",
                "cancelled" => "Cancelled",
                _ => Ui.Plural(left, "seat", "seats") + " left"
            };
            int pct = k.Capacity <= 0 ? 100 : Math.Min(100, Rules.Taken(site, k) * 100 / k.Capacity);
            return "<div class=\"cd-ph\"><div><b>" + Ui.Money(k.Price) + "</b><small>for all " + Ui.Plural(k.Dates.Count, "session", "sessions") + "</small></div>" + Ui.StatePill("cd", st) + "</div><div class=\"cd-seatbar\"><i style=\"width:" + pct + "%\"></i></div><p class=\"cd-seatt\">" + sub + "</p>";
        }

        private static string Panel(SiteData site, SportClass k, global::Sport.Models.ClubMember? me, DateTime now)
        {
            Settings s = site.Settings;
            StringBuilder sb = new StringBuilder(PriceHead(site, k, now));
            Gate g = Rules.CanJoin(site, k, me, now);
            if (me == null)
            {
                if (!g.Ok)
                {
                    sb.Append("<div class=\"cd-info-box\">" + g.Why + "</div>");
                }
                sb.Append("<div class=\"cd-guest\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"5\" y=\"10.5\" width=\"14\" height=\"10\" rx=\"2\"/><path d=\"M8 10.5V8a4 4 0 0 1 8 0v2.5\"/></svg><p>Class registration is for members. Membership is free and you earn points from your first class.</p></div><a class=\"cd-btn cd-btn-accent cd-btn-wide\" href=\"SignIn?next=" + Uri.EscapeDataString("Class?id=" + k.Id) + "\">Sign in to register</a><a class=\"cd-btn cd-btn-ghost cd-btn-wide\" href=\"Join?next=" + Uri.EscapeDataString("Class?id=" + k.Id) + "\">Sign up free</a><p class=\"cd-hint cd-ctr\">Members save up to " + Rules.TierOff(s, "diamond").ToString("0.##", Ui.Inv) + "% on every class.</p>");
                return sb.ToString();
            }
            string tier = Rules.TierOf(s, me.Lifetime);
            List<Registration> mine = site.Registrations.Where(r => r.Class == k.Id && r.Member == me.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist").ToList();
            sb.Append("<div class=\"cd-mehead\"><span>Signed in as <b>" + Ui.Enc(Rules.FullName(me)) + "</b></span>" + Ui.TierPill("cd", tier) + "</div>");
            if (mine.Count > 0)
            {
                sb.Append("<div class=\"cd-mine\">");
                foreach (Registration r in mine)
                {
                    sb.Append("<a href=\"Member?tab=classes\"><span><b>" + Ui.Enc(r.Name) + "</b><small>" + (r.Status == "offered" && r.OfferExp != null ? "Pay by " + Ui.When(site, Store.Local(site, r.OfferExp.Value)) : r.Status == "pending" && r.HoldExp != null ? "Pay at the desk by " + Ui.When(site, Store.Local(site, r.HoldExp.Value)) : "Code " + Ui.Enc(r.Code)) + "</small></span>" + Ui.Pill("cd", Rules.RegStatuses, r.Status) + "</a>");
                }
                sb.Append("</div>");
            }
            if (!g.Ok)
            {
                sb.Append("<div class=\"cd-info-box\">" + g.Why + "</div>");
                return sb.ToString();
            }
            if (g.EarlyNow)
            {
                sb.Append("<div class=\"cd-early\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M13 3L5 13.5h6L10 21l8-10.5h-6z\"/></svg><span><b>Early access</b> As a " + Ui.Enc(Rules.TierName(tier)) + " member you can register before everyone else.</span></div>");
            }
            List<(string Id, string Name, DateTime? Born, string Note, bool Ok)> people = new List<(string, string, DateTime?, string, bool)>();
            people.Add(("self", Rules.FullName(me), me.Born, string.Empty, true));
            foreach (FamilyMember f in me.Family)
            {
                people.Add((f.Id, (f.First + " " + f.Last).Trim(), f.Born, string.Empty, true));
            }
            StringBuilder who = new StringBuilder("<fieldset class=\"cd-who\"><legend>Who is taking the class?</legend>");
            bool any = false;
            foreach ((string id, string name, DateTime? born, string note, bool ok) in people)
            {
                string why = string.Empty;
                if (born == null)
                {
                    why = "Add a date of birth in your profile";
                }
                else if (!Rules.AgeFits(k, born.Value))
                {
                    why = "Age " + Rules.Age(born.Value, Rules.Start(k)) + " at the start, class is " + Rules.AgeRule(k).Replace("&ndash;", "-").ToLowerInvariant();
                }
                else if (mine.Any(r => r.Person == id))
                {
                    why = "Already registered";
                }
                bool pick = why == string.Empty && !any;
                any = any || why == string.Empty;
                who.Append("<label class=\"cd-wopt" + (why != string.Empty ? " cd-off" : string.Empty) + "\"><input type=\"radio\" name=\"cd-who\" value=\"" + Ui.Attr(id) + "\"" + (pick ? " checked" : string.Empty) + (why != string.Empty ? " disabled" : string.Empty) + "><span><b>" + Ui.Enc(name) + (id == "self" ? " (you)" : string.Empty) + "</b><small>" + (why != string.Empty ? Ui.Enc(why) : born != null ? "Age " + Rules.Age(born.Value, Rules.Start(k)) + " at the start" : string.Empty) + "</small></span></label>");
            }
            who.Append("<a class=\"cd-addfam\" href=\"Member?tab=family\">+ Add a family member</a></fieldset>");
            if (!any)
            {
                sb.Append(who.ToString().Replace("<legend>", "<legend class=\"cd-nolegend\">") + "<div class=\"cd-info-box\">Nobody on your account can take this class right now.</div>");
                return sb.ToString();
            }
            sb.Append("<form class=\"cd-rform\" id=\"cd-rf\" onsubmit=\"return ClassJs.register()\" novalidate data-mode=\"" + g.Mode + "\">" + who);
            if (g.Mode == "waitlist")
            {
                sb.Append("<div class=\"cd-warn-box\"><b>This class is full.</b>Join the waitlist for free. If a seat opens, the first person on the list gets an offer and " + s.OfferHours + " hours to pay. You'll see it in your member area.</div>");
            }
            else
            {
                Quote q = Rules.Price(site, k, me, 0);
                if (q.MaxPoints > 0)
                {
                    int ppd = Math.Max(1, s.PointsPerDollar);
                    sb.Append("<label class=\"cd-field\"><span>Use points (you have " + Ui.Num(me.Balance) + ")</span><select id=\"cd-pts\" onchange=\"ClassJs.quote()\"><option value=\"0\">Don't use points</option>");
                    for (int p = ppd; p <= q.MaxPoints; p += ppd)
                    {
                        if (q.MaxPoints / ppd > 12 && p != q.MaxPoints && (p / ppd) % Math.Max(1, q.MaxPoints / ppd / 8) != 0)
                        {
                            continue;
                        }
                        sb.Append("<option value=\"" + p + "\">" + Ui.Num(p) + " points &minus;" + Ui.Money(p / (decimal)ppd) + "</option>");
                    }
                    sb.Append("</select></label>");
                }
                sb.Append("<div class=\"cd-sum\" id=\"cd-sum\">" + Summary(s, q) + "</div><fieldset class=\"cd-paym\"><legend>How do you want to pay?</legend><label class=\"cd-wopt\"><input type=\"radio\" name=\"cd-pay\" value=\"card\" checked onchange=\"ClassJs.payMode()\"><span><b>Pay now by card</b><small>Your seat is confirmed right away</small></span></label><label class=\"cd-wopt\"><input type=\"radio\" name=\"cd-pay\" value=\"desk\" onchange=\"ClassJs.payMode()\"><span><b>Pay at the front desk</b><small>We hold your seat for " + s.DeskHours + " hours</small></span></label></fieldset>");
                if (me.Street == string.Empty)
                {
                    sb.Append("<div class=\"cd-bill\"><b>Billing address</b><p class=\"cd-hint\">We ask once, with your first payment.</p><label class=\"cd-field\"><span>Street</span><input type=\"text\" id=\"cd-bstreet\" maxlength=\"100\" autocomplete=\"street-address\"></label><em class=\"cd-err\" id=\"cd-e-bstreet\" role=\"alert\"></em><div class=\"cd-row3\"><label class=\"cd-field\"><span>City</span><input type=\"text\" id=\"cd-bcity\" maxlength=\"60\" autocomplete=\"address-level2\"></label><label class=\"cd-field\"><span>State</span><input type=\"text\" id=\"cd-bregion\" maxlength=\"30\" autocomplete=\"address-level1\"></label><label class=\"cd-field\"><span>ZIP</span><input type=\"text\" id=\"cd-bzip\" maxlength=\"12\" autocomplete=\"postal-code\"></label></div></div>");
                }
                sb.Append("<div id=\"cd-cardbox\">" + CardForm("cd", "ClassJs", Rules.FullName(me), string.Empty, string.Empty) + "</div>");
            }
            sb.Append("<details class=\"cd-wtext\"><summary>Read the waiver</summary><p>" + Ui.Enc(s.Waiver) + "</p></details><label class=\"cd-check\"><input type=\"checkbox\" id=\"cd-wv\"> I accept the waiver and the cancellation rules.</label><em class=\"cd-err\" id=\"cd-e-wv\" role=\"alert\"></em><em class=\"cd-err\" id=\"cd-e-reg\" role=\"alert\"></em>");
            Quote q0 = Rules.Price(site, k, me, 0);
            string payLabel = "Pay " + Ui.Money(q0.Total, true);
            sb.Append("<button type=\"submit\" class=\"cd-btn cd-btn-accent cd-btn-wide\" id=\"cd-go\" data-card=\"" + Ui.Attr(payLabel) + "\" data-desk=\"Hold my seat\">" + (g.Mode == "waitlist" ? "Join the waitlist" : payLabel) + "</button></form>");
            return sb.ToString();
        }

        private static string Summary(Settings s, Quote q)
        {
            StringBuilder sb = new StringBuilder("<div><span>Class fee</span><b>" + Ui.Money(q.Price, true) + "</b></div>");
            if (q.Discount > 0)
            {
                sb.Append("<div class=\"cd-save\"><span>" + Ui.Enc(Rules.TierName(q.Tier)) + " discount, " + q.OffPct.ToString("0.##", Ui.Inv) + "%</span><b>&minus;" + Ui.Money(q.Discount, true) + "</b></div>");
            }
            if (q.PointsOff > 0)
            {
                sb.Append("<div class=\"cd-save\"><span>" + Ui.Num(q.Points) + " points</span><b>&minus;" + Ui.Money(q.PointsOff, true) + "</b></div>");
            }
            sb.Append("<div class=\"cd-tot\"><span>Total</span><b>" + Ui.Money(q.Total, true) + "</b></div><p class=\"cd-earn\">You'll earn " + Ui.Num(Rules.EarnFor(s, q.Total)) + " points now and " + s.PerSession + " for each session you attend.</p>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Panel()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Context();
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("kid"), 20) && x.Published);
            if (k == null)
            {
                return response;
            }
            if (me == null && GetDataValue("mtok") != string.Empty)
            {
                response.ExecuteScript("ClassJs.signedOut();");
            }
            response.SetElementContents("cd-panel", Panel(site, k, me, Store.Now(site)));
            response.ExecuteScript("ClassJs.payMode();");
            return response;
        }

        public async Task<ApiResponse> Quote()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Context();
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("kid"), 20));
            if (k == null || me == null)
            {
                return response;
            }
            Quote q = Rules.Price(site, k, me, int.TryParse(GetDataValue("pts"), out int p) ? p : 0);
            response.SetElementContents("cd-sum", Summary(site.Settings, q));
            response.ExecuteScript("ClassJs.payLabel('" + Ui.Js("Pay " + Ui.Plain(q.Total)) + "');");
            return response;
        }

        public async Task<ApiResponse> Register()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.SetElementContents("cd-e-reg", "Your session ended. Please sign in again.");
                response.ExecuteScript("ClassJs.done(false);");
                return response;
            }
            string kid = Ui.Clip(GetDataValue("kid"), 20);
            string person = Ui.Clip(GetDataValue("who"), 20);
            bool desk = GetDataValue("pay") == "desk";
            string mode = GetDataValue("mode") == "waitlist" ? "waitlist" : "register";
            if (person == string.Empty)
            {
                response.SetElementContents("cd-e-reg", "Choose who is taking the class.");
                response.ExecuteScript("ClassJs.done(false);");
                return response;
            }
            if (GetDataValue("wv") != "1")
            {
                response.SetElementContents("cd-e-wv", "Please accept the waiver to continue.");
                response.ExecuteScript("ClassJs.done(false);");
                return response;
            }
            CardIn card = Auth.ReadCard(GetDataValue("cname"), GetDataValue("cnum"), GetDataValue("cexp"), GetDataValue("ccvc"));
            if (mode == "register" && !desk)
            {
                if (CardErrors(response, "cd", card))
                {
                    response.ExecuteScript("ClassJs.done(false);");
                    return response;
                }
                if (card.Declined)
                {
                    response.SetElementContents("cd-e-reg", "Your card was declined. (This is the demo decline card. Try 4242 4242 4242 4242.)");
                    response.ExecuteScript("ClassJs.done(false);");
                    return response;
                }
            }
            string err = string.Empty;
            string done = string.Empty;
            Registration? made = null;
            SiteData? saved = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                DateTime now = Store.Now(site);
                DateTime nowU = DateTime.UtcNow;
                global::Sport.Models.ClubMember? me = site.Members.FirstOrDefault(x => x.Id == mc.Id && x.Type == "member" && x.Status == "active" && (Store.Demo || !x.Sample));
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == kid && x.Published);
                if (me == null || k == null)
                {
                    err = "That class or account is no longer available.";
                }
                else
                {
                    Gate g = Rules.CanJoin(site, k, me, now);
                    string name = string.Empty;
                    DateTime? born = null;
                    if (person == "self")
                    {
                        name = Rules.FullName(me);
                        born = me.Born;
                    }
                    else
                    {
                        FamilyMember? f = me.Family.FirstOrDefault(x => x.Id == person);
                        if (f != null)
                        {
                            name = (f.First + " " + f.Last).Trim();
                            born = f.Born;
                        }
                    }
                    if (!g.Ok)
                    {
                        err = g.Why;
                    }
                    else if (name == string.Empty)
                    {
                        err = "Choose who is taking the class.";
                    }
                    else if (born == null)
                    {
                        err = "Add a date of birth in your profile first.";
                    }
                    else if (!Rules.AgeFits(k, born.Value))
                    {
                        err = name + " is outside the age range for this class.";
                    }
                    else if (site.Registrations.Any(r => r.Class == k.Id && r.Member == me.Id && r.Person == person && r.Status is "confirmed" or "pending" or "offered" or "waitlist"))
                    {
                        err = name + " is already registered for this class.";
                    }
                    else if (mode == "register" && g.Mode == "waitlist")
                    {
                        err = "The class just filled up. Reload the page to join the waitlist.";
                    }
                    else if (mode == "waitlist" && g.Mode == "register")
                    {
                        err = "A seat just opened. Reload the page to register.";
                    }
                    if (err == string.Empty && mode == "register" && me.Street == string.Empty && GetDataValue("pay") != "desk")
                    {
                        string street = Ui.OneLine(GetDataValue("bstreet"), 100);
                        string city = Ui.OneLine(GetDataValue("bcity"), 60);
                        string region = Ui.OneLine(GetDataValue("bregion"), 30);
                        string zip = Ui.OneLine(GetDataValue("bzip"), 12);
                        if (street.Length < 4 || city.Length < 2 || zip.Length < 3)
                        {
                            err = "Enter your billing address: street, city and ZIP.";
                        }
                        else
                        {
                            me.Street = street;
                            me.City = city;
                            me.Region = region;
                            me.Zip = zip;
                        }
                    }
                    if (err == string.Empty)
                    {
                        Registration r = new Registration { Id = Store.NextId(site.Registrations.Select(x => x.Id), "R-", 1000), Code = Rules.NewCode(site), Class = k.Id, Member = me.Id, Person = person, Name = name, Born = born, Price = k.Price, Waiver = true, Created = nowU, By = "web" };
                        if (mode == "waitlist")
                        {
                            r.Status = "waitlist";
                            r.Tier = Rules.TierOf(site.Settings, me.Lifetime);
                            site.Registrations.Add(r);
                            done = "waitlist";
                        }
                        else
                        {
                            Quote q = Rules.Price(site, k, me, int.TryParse(GetDataValue("pts"), out int p) ? p : 0);
                            r.Tier = q.Tier;
                            r.Discount = q.Discount;
                            r.Points = q.Points;
                            r.PointsOff = q.PointsOff;
                            r.Total = q.Total;
                            site.Registrations.Add(r);
                            if (q.Points > 0)
                            {
                                Rules.AddPoints(site, me, -q.Points, "spend", r.Id, "Used on " + r.Code, "web", nowU);
                            }
                            if (desk)
                            {
                                r.Status = "pending";
                                r.Method = "desk";
                                DateTime hold = nowU.AddHours(Math.Max(1, site.Settings.DeskHours));
                                DateTime startU = Store.Utc(site, Rules.Start(k));
                                r.HoldExp = hold > startU ? startU : hold;
                                done = "desk";
                            }
                            else
                            {
                                Rules.Confirm(site, r, "card", card.Brand, card.Last4, "web", nowU);
                                done = "paid";
                            }
                        }
                        made = r;
                        await Store.SaveSite(site, mc.Sbx);
                        saved = site;
                    }
                }
            }
            if (err != string.Empty || made == null || saved == null)
            {
                response.SetElementContents("cd-e-reg", Ui.Enc(err));
                response.ExecuteScript("ClassJs.done(false);");
                return response;
            }
            SportClass kk = saved.Classes.First(x => x.Id == made.Class);
            await Store.Log(mc.Sbx, "web", "register", kk.Id + " · " + kk.Title + " · " + (done == "waitlist" ? "waitlist" : done == "desk" ? "pay at desk" : "paid by card"), true);
            string head = done switch
            {
                "waitlist" => "<div class=\"cd-ok-box\"><b>You're on the waitlist.</b>" + Ui.Enc(made.Name) + " is number " + saved.Registrations.Count(r => r.Class == kk.Id && r.Status == "waitlist" && r.Created <= made.Created) + " in line. If a seat opens, you'll get an offer in your member area with " + saved.Settings.OfferHours + " hours to pay.</div>",
                "desk" => "<div class=\"cd-ok-box\"><b>Your seat is held.</b>Pay " + Ui.Money(made.Total, true) + " at the front desk by " + Ui.When(saved, Store.Local(saved, made.HoldExp ?? DateTime.UtcNow)) + ". After that the seat goes to the next person.</div>",
                _ => "<div class=\"cd-ok-box\"><b>You're registered!</b>" + Ui.Enc(made.Name) + " is confirmed for " + Ui.Enc(kk.Title) + ". We sent nothing by email in this demo, so keep the receipt below.</div>"
            };
            string body = head + (done == "waitlist" ? "<p class=\"cd-hint\">Waitlist code <b class=\"cd-code\">" + Ui.Enc(made.Code) + "</b></p>" : ReceiptBox("cd", Rules.Receipt(saved, made, "cd"), made.Code)) + "<div class=\"cd-after\"><a class=\"cd-btn cd-btn-wide\" href=\"Member?tab=classes\">Go to my classes</a><button type=\"button\" class=\"cd-btn cd-btn-ghost cd-btn-wide\" onclick=\"ClassJs.again()\">Register someone else</button></div>";
            response.SetElementContents("cd-panel", body);
            response.ExecuteScript("ClassJs.done(true);");
            return response;
        }

        private static string CardForm(string p, string js, string name, string payLabel, string backJs)
        {
            int yy = DateTime.UtcNow.Year % 100 + 2;
            return "<div class=\"" + p + "-demo-card\"><b>Demo payment, nothing is charged.</b> Test card <code>4242 4242 4242 4242</code> is accepted; <code>4000 0000 0000 0002</code> is always declined. Please don't enter a real card.</div><div class=\"" + p + "-form\"><label class=\"" + p + "-field\"><span>Name on card</span><input type=\"text\" id=\"" + p + "-cname\" maxlength=\"60\" autocomplete=\"cc-name\" value=\"" + Ui.Attr(name) + "\"></label><em class=\"" + p + "-err\" id=\"" + p + "-e-cname\" role=\"alert\"></em><label class=\"" + p + "-field\"><span>Card number</span><input type=\"text\" id=\"" + p + "-cnum\" inputmode=\"numeric\" maxlength=\"23\" autocomplete=\"off\" value=\"4242 4242 4242 4242\" oninput=\"" + js + ".fmtCard(this)\"></label><em class=\"" + p + "-err\" id=\"" + p + "-e-cnum\" role=\"alert\"></em><div class=\"" + p + "-row2\"><label class=\"" + p + "-field\"><span>Expiry (MM/YY)</span><input type=\"text\" id=\"" + p + "-cexp\" inputmode=\"numeric\" maxlength=\"5\" autocomplete=\"off\" value=\"12/" + yy.ToString("00", Ui.Inv) + "\" oninput=\"" + js + ".fmtExp(this)\"></label><label class=\"" + p + "-field\"><span>Security code</span><input type=\"text\" id=\"" + p + "-ccvc\" inputmode=\"numeric\" maxlength=\"4\" autocomplete=\"off\" value=\"123\"></label></div><em class=\"" + p + "-err\" id=\"" + p + "-e-cexp\" role=\"alert\"></em><em class=\"" + p + "-err\" id=\"" + p + "-e-ccvc\" role=\"alert\"></em></div>";
        }

        private static bool CardErrors(ApiResponse response, string p, CardIn card)
        {
            response.SetElementContents(p + "-e-cname", card.ErrName);
            response.SetElementContents(p + "-e-cnum", card.ErrNumber);
            response.SetElementContents(p + "-e-cexp", card.ErrExp);
            response.SetElementContents(p + "-e-ccvc", card.ErrCvc);
            return !card.Ok;
        }

        private static string ReceiptBox(string p, string inner, string code)
        {
            return inner + "<div class=\"" + p + "-ract\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"ClassJs.printBox('" + p + "-rcpt-" + Ui.Attr(code) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 9V3h10v6M7 17H4v-7h16v7h-3\"/><path d=\"M7 14h10v7H7z\"/></svg> Print receipt</button></div>";
        }

        private static string Icon(string k)
        {
            return k switch
            {
                "swim" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"16\" cy=\"6\" r=\"2\"/><path d=\"M3 15c2 0 2-1.5 4.5-1.5S10 15 12 15s2.5-1.5 4.5-1.5S19 15 21 15M3 19.5c2 0 2-1.5 4.5-1.5s2.5 1.5 4.5 1.5 2.5-1.5 4.5-1.5 2.5 1.5 4.5 1.5M6 12l4-4 3 3 3-1\"/></svg>",
                "ball" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M3.5 12h17M12 3.5v17M6 6c2.5 2 3.5 4 3.5 6S8.5 16 6 18M18 6c-2.5 2-3.5 4-3.5 6s1 4 3.5 6\"/></svg>",
                "racket" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><ellipse cx=\"14\" cy=\"9\" rx=\"5\" ry=\"6\" transform=\"rotate(40 14 9)\"/><path d=\"M10.2 13.3L4 19.5M11 9l4 4M13 6.5l4 4\"/><circle cx=\"5.5\" cy=\"6\" r=\"1.6\"/></svg>",
                "climb" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 20l6-10 4 6 2-3 6 7z\"/><circle cx=\"16\" cy=\"6\" r=\"2\"/></svg>",
                "yoga" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"5\" r=\"2\"/><path d=\"M12 8v6M6 11l6 3 6-3M5 19c3-2 5-2.5 7-2.5s4 .5 7 2.5\"/></svg>",
                "belt" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 9.5h18v4H3zM10.5 13.5l-3 6.5M13.5 13.5l3 6.5\"/><rect x=\"10\" y=\"9\" width=\"4\" height=\"5\" rx=\"1\"/></svg>",
                "dance" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"13\" cy=\"4.5\" r=\"2\"/><path d=\"M13 7l-1 6 3 3 1 5M12 13l-4 2-2 5M13 8l4 2 2-3\"/></svg>",
                "gym" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M8 3v8M16 3v8\"/><circle cx=\"8\" cy=\"15\" r=\"4\"/><circle cx=\"16\" cy=\"15\" r=\"4\"/></svg>",
                "whistle" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 11a5 5 0 1 0 10 0h6V8H9.5\"/><circle cx=\"9\" cy=\"11\" r=\"1.5\"/><path d=\"M14 5l2-2\"/></svg>",
                "trophy" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M8 4h8v5a4 4 0 0 1-8 0zM8 6H4.5a3 3 0 0 0 3.5 3.5M16 6h3.5A3 3 0 0 1 16 9.5M12 13v4M8.5 20h7M10 17h4\"/></svg>",
                "users" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "star" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "heart" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg>",
                "shield" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 3l7 3v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6z\"/><path d=\"M9 12l2 2 4-4\"/></svg>",
                "clock" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "pin" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "cal" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "doc" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 3h7l4 4v14H7z\"/><path d=\"M14 3v4h4M10 12h5M10 16h5\"/></svg>",
                "ticket" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16v3a2 2 0 0 0 0 4v3H4v-3a2 2 0 0 0 0-4z\"/><path d=\"M14 7.5v9\" stroke-dasharray=\"2 2\"/></svg>",
                _ => string.Empty
            };
        }
    }
}
