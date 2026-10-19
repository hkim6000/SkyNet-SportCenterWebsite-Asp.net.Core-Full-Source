using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class Member : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--mb-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("mb", v.Nav, false))
                .Replace("{plhd_drawer}", Nav("mb", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("MemberJs.hydrated(false);");
                return response;
            }
            qd = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Ui.Clip(GetDataValue("qstr"), 600)))
            {
                qd[kv.Key] = kv.Value.ToString();
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = await Render(site);
            response.SetElementContents("mb-main", v.Main);
            response.SetElementContents("mb-bname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mb-btown", Ui.Enc(site.Settings.Town));
            response.SetElementContents("mb-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mb-copyname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mb-ftag", Ui.Enc(site.Settings.Tagline));
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MemberJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "'," + Store.MinutesLeft(sbx) + ");");
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
                response.ExecuteScript("MemberJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("MemberJs.who('', '', '');");
                return response;
            }
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            response.ExecuteScript("MemberJs.who('" + Ui.Js(me.First) + "','" + tier + "','" + Rules.TierName(tier) + "');");
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
                return "<i class=\"mb-shut\"></i>Opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"mb-shut\"></i>Closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"mb-shut\"></i>Closed";
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
            return "<article class=\"mb-kc\"><a class=\"mb-kc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) + "<span class=\"mb-kc-tag\">" + Ui.Enc(Ui.SportName(site, k.Sport)) + "</span><span class=\"mb-kc-st\">" + Ui.StatePill("mb", st) + "</span></a><div class=\"mb-kc-b\"><span class=\"mb-kc-t\"><a href=\"" + href + "\">" + Ui.Enc(k.Title) + "</a></span><span class=\"mb-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> " + Ui.ClassWhen(site, k) + "</span><span class=\"mb-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> " + Ui.Range(k) + "</span><span class=\"mb-meta\"><span class=\"mb-chipx\">" + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><span class=\"mb-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"mb-chipx\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span></span><span class=\"mb-kc-f\"><b>" + Ui.Money(k.Price) + "</b><small>" + Ui.Plural(k.Dates.Count, "session", "sessions") + " &middot; " + seats + "</small></span></div></article>";
        }

        private static int OpenCount(SiteData site, string sport, DateTime now)
        {
            return site.Classes.Count(k => k.Sport == sport && Rules.State(site, k, now) is "open" or "few" or "full");
        }

        private static readonly (string Key, string Name)[] Tabs = { ("overview", "Overview"), ("classes", "My classes"), ("family", "Family"), ("visits", "Check-ins"), ("payments", "Payments"), ("points", "Points"), ("profile", "Profile") };

        private async Task<View> Render(SiteData site)
        {
            string tab = Tabs.Any(t => t.Key == Q("tab", 20)) ? Q("tab", 20) : "overview";
            StringBuilder sb = new StringBuilder("<section class=\"mb-mhero\"><div class=\"mb-wrap mb-mhero-in\" id=\"mb-mhead\"><div><span class=\"mb-kick\">Member area</span><h1>Welcome back</h1></div></div></section><div class=\"mb-wrap\"><nav class=\"mb-mtabs\" id=\"mb-mtabs\" aria-label=\"Member area\">");
            foreach ((string k, string n) in Tabs)
            {
                sb.Append("<button type=\"button\" class=\"mb-mtab" + (k == tab ? " mb-act" : string.Empty) + "\" data-tab=\"" + k + "\" onclick=\"MemberJs.tab('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</nav><div id=\"mb-mbody\" class=\"mb-mbody\"><p class=\"mb-none\">Loading your account&hellip;</p></div></div><div class=\"mb-modal\" id=\"mb-modal\" hidden onclick=\"if(event.target===this)MemberJs.closeModal()\"><div class=\"mb-mbox\" role=\"dialog\" aria-modal=\"true\"><button type=\"button\" class=\"mb-ibtn mb-mclose\" aria-label=\"Close\" onclick=\"MemberJs.closeModal()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button><div id=\"mb-mpanel\"></div></div></div>");
            return new View { Title = "Member area", Desc = "Your classes, family, receipts and points at " + site.Settings.Name + ".", Main = sb.ToString(), Nav = "member" };
        }

        private static string Head(SiteData site, global::Sport.Models.ClubMember me)
        {
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            return "<div class=\"mb-mh-l\">" + Ui.MemberAv("mb", me) + "<div><span class=\"mb-kick\">Member since " + (me.Joined ?? me.Created).ToString("MMMM yyyy", Ui.Inv) + "</span><h1>" + Ui.Enc(Rules.FullName(me)) + "</h1></div></div><div class=\"mb-mh-r\">" + Ui.TierPill("mb", tier) + "<div><b>" + Ui.Num(me.Balance) + "</b><small>points to spend</small></div><div><b>" + Ui.Num(me.Lifetime) + "</b><small>lifetime points</small></div><button type=\"button\" class=\"mb-mh-out\" onclick=\"MemberJs.signOutMember()\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3\"/><path d=\"M10 17l5-5-5-5\"/><path d=\"M15 12H4\"/></svg>Sign out</button></div>";
        }

        private static string Progress(SiteData site, global::Sport.Models.ClubMember me)
        {
            Settings s = site.Settings;
            string tier = Rules.TierOf(s, me.Lifetime);
            (string next, int at) = Rules.NextTier(s, me.Lifetime);
            int from = Rules.TierAt(s, tier);
            int pct = next == string.Empty ? 100 : Math.Clamp((me.Lifetime - from) * 100 / Math.Max(1, at - from), 0, 100);
            int ppd = Math.Max(1, s.PointsPerDollar);
            return "<div class=\"mb-tcard mb-tcard-" + tier + "\"><div class=\"mb-tcard-top\">" + Ui.TierPill("mb", tier) + "<span>" + (Rules.TierOff(s, tier) > 0 ? Rules.TierOff(s, tier).ToString("0.##", Ui.Inv) + "% off every class" : "Member prices") + (Rules.Early(s, tier) ? " &middot; early registration" : string.Empty) + "</span></div><b>" + Ui.Num(me.Lifetime) + " <small>lifetime points</small></b><div class=\"mb-tbar\"><i style=\"width:" + pct + "%\"></i></div><p>" + (next == string.Empty ? "You reached the top tier. Thank you!" : Ui.Num(at - me.Lifetime) + " points to " + Rules.TierName(next) + " (" + Rules.TierOff(s, next).ToString("0.##", Ui.Inv) + "% off" + (Rules.Early(s, next) && !Rules.Early(s, tier) ? ", early registration" : string.Empty) + ")") + "</p><div class=\"mb-bal\"><span><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M13 3L5 13.5h6L10 21l8-10.5h-6z\"/></svg> " + Ui.Num(me.Balance) + " points to spend</span><b>worth " + Ui.Money(me.Balance / ppd) + "</b></div></div>";
        }

        private static string RegLine(SiteData site, Registration r, bool acts)
        {
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            DateTime now = Store.Now(site);
            (bool cancel, bool ask, string text, decimal amt) = Rules.Options(site, r, DateTime.UtcNow);
            StringBuilder sb = new StringBuilder("<div class=\"mb-rl\" id=\"mb-r-" + Ui.Attr(r.Id) + "\"><div class=\"mb-rl-img\">" + (k != null ? Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) : string.Empty) + "</div><div class=\"mb-rl-b\"><div class=\"mb-rl-top\"><a href=\"Class?id=" + Uri.EscapeDataString(r.Class) + "\"><b>" + Ui.Enc(k?.Title ?? r.Class) + "</b></a>" + Ui.Pill("mb", Rules.RegStatuses, r.Status) + "</div><small>" + Ui.Enc(r.Name) + (k != null ? " &middot; " + Ui.ClassWhen(site, k) + " &middot; " + Ui.Range(k) : string.Empty) + "</small>");
            if (k != null && r.Status == "confirmed" && now >= Rules.Start(k))
            {
                int past = k.Dates.Count(d => d.AddMinutes(k.Minutes) <= now);
                int came = site.Visits.Count(v => v.Reg == r.Id && v.Kind == "class");
                sb.Append("<small class=\"mb-att\">Attended " + came + " of " + past + " sessions so far</small>");
            }
            if (r.Status == "offered" && r.OfferExp != null)
            {
                sb.Append("<div class=\"mb-ok-box mb-sm\"><b>A seat opened up for you!</b>Pay by " + Ui.When(site, Store.Local(site, r.OfferExp.Value)) + " to keep it.</div>");
            }
            if (r.Status == "pending" && r.HoldExp != null)
            {
                sb.Append("<div class=\"mb-warn-box mb-sm\"><b>Seat held until " + Ui.When(site, Store.Local(site, r.HoldExp.Value)) + ".</b>Pay " + Ui.Money(r.Total, true) + " at the front desk or online now.</div>");
            }
            if (r.Status == "waitlist")
            {
                sb.Append("<small class=\"mb-att\">Number " + site.Registrations.Count(x => x.Class == r.Class && x.Status == "waitlist" && x.Created <= r.Created) + " on the waitlist</small>");
            }
            if (r.Ask != null)
            {
                sb.Append("<div class=\"" + (r.Ask.Status == "approved" ? "mb-ok-box" : r.Ask.Status == "declined" ? "mb-bad-box" : "mb-info-box") + " mb-sm\"><b>Refund request: " + (r.Ask.Status == "open" ? "waiting for the front desk" : r.Ask.Status) + "</b>" + Ui.Enc(r.Ask.Reason) + (r.Ask.Note != string.Empty ? "<br><i>" + Ui.Enc(r.Ask.Note) + "</i>" : string.Empty) + "</div>");
            }
            if (r.EndNote != string.Empty && r.Status is "cancelled" or "expired")
            {
                sb.Append("<small class=\"mb-att\">" + Ui.Enc(r.EndNote) + "</small>");
            }
            if (acts)
            {
                sb.Append("<div class=\"mb-rl-acts\">");
                if (r.Status is "offered" or "pending")
                {
                    sb.Append("<button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-accent\" onclick=\"MemberJs.payForm('" + Ui.Attr(r.Id) + "')\">" + (r.Status == "offered" ? "Pay for the seat" : "Pay online now") + "</button>");
                }
                if (r.Status is "confirmed" or "pending" or "cancelled" or "expired" && (r.Paid > 0 || r.Status == "pending"))
                {
                    sb.Append("<button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-ghost\" onclick=\"MemberJs.receipt('" + Ui.Attr(r.Id) + "')\">Receipt</button>");
                }
                if (cancel)
                {
                    string label = r.Status switch { "waitlist" => "Leave the waitlist", "offered" => "Decline", _ => "Cancel" };
                    sb.Append("<button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-danger\" onclick=\"MemberJs.cancelReg('" + Ui.Attr(r.Id) + "','" + Ui.Js(label) + "','" + Ui.Js(text) + "')\">" + label + "</button>");
                }
                if (ask)
                {
                    sb.Append("<button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-ghost\" onclick=\"MemberJs.askForm('" + Ui.Attr(r.Id) + "','" + Ui.Js(text) + "')\">Ask for a refund</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string Overview(SiteData site, global::Sport.Models.ClubMember me, string welcome)
        {
            DateTime now = Store.Now(site);
            StringBuilder sb = new StringBuilder();
            if (welcome == "1" || welcome == "2")
            {
                sb.Append("<div class=\"mb-ok-box mb-welcome\"><b>Welcome to " + Ui.Enc(site.Settings.Name) + ", " + Ui.Enc(me.First) + "!</b>" + (welcome == "2" ? "We found your earlier day passes and kept your check-in history. " : string.Empty) + "Your points start now. Add family members, then find a class.</div>");
            }
            List<Registration> act = site.Registrations.Where(r => r.Member == me.Id && r.Status is "offered" or "pending").ToList();
            sb.Append("<div class=\"mb-ogrid\"><div class=\"mb-ocol\">");
            if (act.Count > 0)
            {
                sb.Append("<section class=\"mb-box\"><h2>Needs your attention</h2>");
                foreach (Registration r in act)
                {
                    sb.Append(RegLine(site, r, true));
                }
                sb.Append("</section>");
            }
            List<(DateTime When, SportClass K, Registration R, int I)> up = new List<(DateTime, SportClass, Registration, int)>();
            foreach (Registration r in site.Registrations.Where(r => r.Member == me.Id && r.Status is "confirmed" or "pending"))
            {
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
                if (k == null || k.Status == "cancelled")
                {
                    continue;
                }
                for (int i = 0; i < k.Dates.Count; i++)
                {
                    if (k.Dates[i].AddMinutes(k.Minutes) > now)
                    {
                        up.Add((k.Dates[i], k, r, i));
                    }
                }
            }
            sb.Append("<section class=\"mb-box\"><div class=\"mb-bh2\"><h2>Coming up</h2><a class=\"mb-more\" href=\"Classes\">Find a class &rarr;</a></div>");
            if (up.Count == 0)
            {
                sb.Append("<p class=\"mb-none\">No sessions booked. <a class=\"mb-more\" href=\"Classes\">Browse the schedule</a></p>");
            }
            foreach ((DateTime when, SportClass k, Registration r, int i) in up.OrderBy(x => x.When).Take(6))
            {
                sb.Append("<a class=\"mb-up\" href=\"Class?id=" + Uri.EscapeDataString(k.Id) + "\"><span class=\"mb-upd\"><b>" + when.ToString("MMM", Ui.Inv).ToUpperInvariant() + "</b><i>" + when.Day + "</i><small>" + when.ToString("ddd", Ui.Inv) + "</small></span><span><b>" + Ui.Enc(k.Title) + "</b><small>" + Ui.Hm(site, when) + " &middot; " + Ui.Enc(k.Place) + " &middot; session " + (i + 1) + " of " + k.Dates.Count + "</small><em>" + Ui.Enc(r.Name) + "</em></span></a>");
            }
            sb.Append("</section></div><div class=\"mb-ocol\">" + Progress(site, me));
            sb.Append("<section class=\"mb-box mb-quick\"><h2>Quick links</h2><a href=\"Classes\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> Book a class</a><a href=\"Pass\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16v3a2 2 0 0 0 0 4v3H4v-3a2 2 0 0 0 0-4z\"/><path d=\"M14 7.5v9\" stroke-dasharray=\"2 2\"/></svg> Buy a day pass</a><button type=\"button\" onclick=\"MemberJs.tab('family')\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg> Add a family member</button><a href=\"MyRegistration\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> Look up a code</a></section></div></div>");
            return sb.ToString();
        }

        private static string ClassesTab(SiteData site, global::Sport.Models.ClubMember me)
        {
            DateTime now = Store.Now(site);
            List<Registration> all = site.Registrations.Where(r => r.Member == me.Id).OrderBy(r => site.Classes.FirstOrDefault(k => k.Id == r.Class) is SportClass k ? Rules.Start(k) : DateTime.MaxValue).ToList();
            bool Past(Registration r) => site.Classes.FirstOrDefault(k => k.Id == r.Class) is SportClass k && now >= Rules.End(k);
            List<Registration> current = all.Where(r => r.Status is "confirmed" or "pending" or "offered" or "waitlist" && !Past(r)).ToList();
            List<Registration> past = all.Where(r => r.Status == "confirmed" && Past(r)).OrderByDescending(r => r.Created).ToList();
            List<Registration> ended = all.Where(r => r.Status is "cancelled" or "expired" || (r.Status is "waitlist" or "offered" or "pending" && Past(r))).OrderByDescending(r => r.Ended ?? r.Created).ToList();
            StringBuilder sb = new StringBuilder("<section class=\"mb-box\"><div class=\"mb-bh2\"><h2>Current and upcoming</h2><a class=\"mb-btn mb-btn-sm mb-btn-accent\" href=\"Classes\">Book a class</a></div>");
            if (current.Count == 0)
            {
                sb.Append("<p class=\"mb-none\">Nothing booked right now.</p>");
            }
            foreach (Registration r in current)
            {
                sb.Append(RegLine(site, r, true));
            }
            sb.Append("</section>");
            if (past.Count > 0)
            {
                sb.Append("<section class=\"mb-box\"><h2>Finished</h2>");
                foreach (Registration r in past)
                {
                    sb.Append(RegLine(site, r, true));
                }
                sb.Append("</section>");
            }
            if (ended.Count > 0)
            {
                sb.Append("<section class=\"mb-box\"><h2>Cancelled and expired</h2>");
                foreach (Registration r in ended)
                {
                    sb.Append(RegLine(site, r, true));
                }
                sb.Append("</section>");
            }
            return sb.ToString();
        }

        private static string FamilyTab(SiteData site, global::Sport.Models.ClubMember me)
        {
            DateTime today = Store.Today(site);
            StringBuilder sb = new StringBuilder("<section class=\"mb-box\"><div class=\"mb-bh2\"><h2>Family members</h2><button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-accent\" onclick=\"MemberJs.famForm('')\">Add a family member</button></div><p class=\"mb-hint\">Add your children or partner to book classes for them. Points go to your account.</p><div class=\"mb-fam\">");
            sb.Append("<div class=\"mb-fm\">" + Ui.MemberAv("mb", me) + "<span><b>" + Ui.Enc(Rules.FullName(me)) + " (you)</b><small>" + (me.Born != null ? "Age " + Rules.Age(me.Born.Value, today) : "Add your date of birth in Profile") + "</small></span></div>");
            foreach (FamilyMember f in me.Family)
            {
                int active = site.Registrations.Count(r => r.Member == me.Id && r.Person == f.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist");
                sb.Append("<div class=\"mb-fm\"><span class=\"mb-av\" style=\"--h:" + Ui.Hue(f.Id) + "\" aria-hidden=\"true\">" + Ui.Enc(Ui.Initials(f.First + " " + f.Last)) + "</span><span><b>" + Ui.Enc((f.First + " " + f.Last).Trim()) + "</b><small>" + Ui.Enc(Ui.NameOf(Rules.Relations, f.Relation)) + " &middot; age " + Rules.Age(f.Born, today) + (active > 0 ? " &middot; " + Ui.Plural(active, "class", "classes") : string.Empty) + "</small></span><button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-ghost\" onclick=\"MemberJs.famForm('" + Ui.Attr(f.Id) + "')\">Edit</button></div>");
            }
            sb.Append("</div></section>");
            return sb.ToString();
        }

        private static string VisitsTab(SiteData site, global::Sport.Models.ClubMember me)
        {
            List<Visit> list = site.Visits.Where(v => v.Member == me.Id).OrderByDescending(v => v.In).ToList();
            StringBuilder sb = new StringBuilder("<section class=\"mb-box\"><h2>Check-in history</h2><p class=\"mb-hint\">Every time you or your family checked in at the front desk or in class.</p>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"mb-none\">No check-ins yet.</p>");
            }
            else
            {
                sb.Append("<div class=\"mb-tbl\"><div class=\"mb-tr mb-th\"><span>Date</span><span>What</span><span>Who</span><span>In</span><span>Out</span></div>");
                foreach (Visit v in list.Take(200))
                {
                    string what;
                    if (v.Kind == "pass")
                    {
                        what = Ui.Enc(site.Settings.PassName);
                    }
                    else
                    {
                        Registration? r = site.Registrations.FirstOrDefault(x => x.Id == v.Reg);
                        SportClass? k = r != null ? site.Classes.FirstOrDefault(x => x.Id == r.Class) : null;
                        what = Ui.Enc(k?.Title ?? "Class") + " &middot; session " + (v.Session + 1);
                    }
                    DateTime i = Store.Local(site, v.In);
                    sb.Append("<div class=\"mb-tr\"><span data-l=\"Date\">" + Ui.Day(i) + "</span><span data-l=\"What\">" + what + "</span><span data-l=\"Who\">" + Ui.Enc(v.Name) + "</span><span data-l=\"In\">" + Ui.Hm(site, i) + "</span><span data-l=\"Out\">" + (v.Out != null ? Ui.Hm(site, Store.Local(site, v.Out.Value)) : "<b class=\"mb-here\">In the building</b>") + "</span></div>");
                }
                sb.Append("</div>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string PaymentsTab(SiteData site, global::Sport.Models.ClubMember me)
        {
            List<Payment> list = site.Payments.Where(p => p.Member == me.Id && p.Status == "paid").OrderByDescending(p => p.When).ToList();
            decimal paid = list.Where(p => p.Kind == "charge").Sum(p => p.Amount);
            decimal back = list.Where(p => p.Kind == "refund").Sum(p => p.Amount);
            StringBuilder sb = new StringBuilder("<div class=\"mb-pstats\"><div><b>" + Ui.Money(paid, true) + "</b><small>paid</small></div><div><b>" + Ui.Money(back, true) + "</b><small>refunded</small></div><div><b>" + Ui.Money(paid - back, true) + "</b><small>net</small></div></div><section class=\"mb-box\"><h2>Payments and refunds</h2>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"mb-none\">No payments yet.</p>");
            }
            else
            {
                sb.Append("<div class=\"mb-tbl mb-ptbl\"><div class=\"mb-tr mb-th\"><span>Date</span><span>For</span><span>Method</span><span class=\"mb-num\">Amount</span><span></span></div>");
                foreach (Payment p in list)
                {
                    string what;
                    string rid = string.Empty;
                    string pid = string.Empty;
                    if (p.Pass != string.Empty)
                    {
                        DayPass? d = site.Passes.FirstOrDefault(x => x.Id == p.Pass);
                        what = Ui.Enc(site.Settings.PassName) + (d != null ? " &middot; " + Ui.Short(d.Day) : string.Empty);
                        pid = p.Pass;
                    }
                    else
                    {
                        Registration? r = site.Registrations.FirstOrDefault(x => x.Id == p.Reg);
                        SportClass? k = r != null ? site.Classes.FirstOrDefault(x => x.Id == r.Class) : null;
                        what = Ui.Enc(k?.Title ?? p.Reg) + (r != null ? " &middot; " + Ui.Enc(r.Name) : string.Empty);
                        rid = p.Reg;
                    }
                    sb.Append("<div class=\"mb-tr\"><span data-l=\"Date\">" + Ui.Day(Store.Local(site, p.When)) + "</span><span data-l=\"For\" class=\"mb-wide\">" + (p.Kind == "refund" ? "<span class=\"mb-st mb-st-refund\">Refund</span> " : string.Empty) + what + "</span><span data-l=\"Method\">" + (p.Method == "card" ? Ui.Enc(p.Brand) + " &bull;&bull;" + Ui.Enc(p.Last4) : "Front desk") + "</span><span data-l=\"Amount\" class=\"mb-num" + (p.Kind == "refund" ? " mb-neg" : string.Empty) + "\">" + (p.Kind == "refund" ? "&minus;" : string.Empty) + Ui.Money(p.Amount, true) + "</span><span>" + (rid != string.Empty ? "<button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-ghost\" onclick=\"MemberJs.receipt('" + Ui.Attr(rid) + "')\">Receipt</button>" : "<button type=\"button\" class=\"mb-btn mb-btn-sm mb-btn-ghost\" onclick=\"MemberJs.passReceipt('" + Ui.Attr(pid) + "')\">Receipt</button>") + "</span></div>");
                }
                sb.Append("</div>");
            }
            sb.Append("</section>");
            List<DayPass> passes = site.Passes.Where(p => p.Member == me.Id).OrderByDescending(p => p.Day).ToList();
            if (passes.Count > 0)
            {
                sb.Append("<section class=\"mb-box\"><h2>Day passes</h2><div class=\"mb-plist\">");
                foreach (DayPass d in passes)
                {
                    sb.Append("<div><span class=\"mb-code\">" + Ui.Enc(d.Code) + "</span><span>" + Ui.Day(d.Day) + "</span><span class=\"mb-st mb-st-" + d.Status + "\">" + (d.Status == "used" ? "Used" : d.Status == "refunded" ? "Refunded" : "Valid") + "</span></div>");
                }
                sb.Append("</div></section>");
            }
            return sb.ToString();
        }

        private static string PointsTab(SiteData site, global::Sport.Models.ClubMember me)
        {
            List<PointEntry> list = site.Points.Where(p => p.Member == me.Id).OrderByDescending(p => p.When).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"mb-ogrid\"><div class=\"mb-ocol\"><section class=\"mb-box\"><h2>Points history</h2>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"mb-none\">No points yet. You earn them when you pay for a class and every time you attend.</p>");
            }
            else
            {
                sb.Append("<div class=\"mb-tbl mb-ltbl\"><div class=\"mb-tr mb-th\"><span>Date</span><span>What</span><span class=\"mb-num\">Points</span></div>");
                foreach (PointEntry p in list.Take(300))
                {
                    string kind = p.Kind switch { "pay" => "Payment", "session" => "Session attended", "spend" => "Spent", "return" => "Points returned", "refund" => "Refund", "adjust" => "Adjustment", "bonus" => "Bonus", _ => p.Kind };
                    sb.Append("<div class=\"mb-tr\"><span data-l=\"Date\">" + Ui.Day(Store.Local(site, p.When)) + "</span><span data-l=\"What\" class=\"mb-wide\"><b>" + kind + "</b><small>" + Ui.Enc(p.Note) + "</small></span><span data-l=\"Points\" class=\"mb-num " + (p.Points < 0 ? "mb-neg" : "mb-pos") + "\">" + (p.Points > 0 ? "+" : "&minus;") + Ui.Num(Math.Abs(p.Points)) + "</span></div>");
                }
                sb.Append("</div>");
            }
            sb.Append("</section></div><div class=\"mb-ocol\">" + Progress(site, me) + "<section class=\"mb-box\"><h2>How points work</h2><ul class=\"mb-howl\"><li>Earn " + site.Settings.PerDollar + " point for every $1 you pay.</li><li>Earn " + site.Settings.PerSession + " points each time you or your family attend a session.</li><li>Spend " + Ui.Num(Math.Max(1, site.Settings.PointsPerDollar)) + " points for $1 off, up to " + site.Settings.PointsMaxPct + "% of a class.</li><li>Your tier follows lifetime points and never goes down. Refunds take back the points they earned.</li></ul></section></div></div><section class=\"mb-box\"><h2>Tiers</h2>" + TierLadder(site, "mb", Rules.TierOf(site.Settings, me.Lifetime)) + "</section>");
            return sb.ToString();
        }

        private static string ProfileTab(SiteData site, global::Sport.Models.ClubMember me)
        {
            return "<div class=\"mb-ogrid\"><section class=\"mb-box\"><h2>Your details</h2><form class=\"mb-form\" onsubmit=\"return MemberJs.saveProfile()\" novalidate><div class=\"mb-row2\"><label class=\"mb-field\"><span>First name</span><input type=\"text\" id=\"mb-pfirst\" maxlength=\"40\" value=\"" + Ui.Attr(me.First) + "\"></label><label class=\"mb-field\"><span>Last name</span><input type=\"text\" id=\"mb-plast\" maxlength=\"40\" value=\"" + Ui.Attr(me.Last) + "\"></label></div><div class=\"mb-row2\"><label class=\"mb-field\"><span>Email</span><input type=\"email\" value=\"" + Ui.Attr(me.Email) + "\" readonly></label><label class=\"mb-field\"><span>Phone</span><input type=\"tel\" id=\"mb-pphone\" maxlength=\"30\" value=\"" + Ui.Attr(me.Phone) + "\"></label></div><label class=\"mb-field\"><span>Date of birth</span><input type=\"date\" id=\"mb-pborn\" value=\"" + (me.Born != null ? Ui.Ymd(me.Born.Value) : string.Empty) + "\"></label><h3>Billing address</h3><label class=\"mb-field\"><span>Street</span><input type=\"text\" id=\"mb-pstreet\" maxlength=\"100\" value=\"" + Ui.Attr(me.Street) + "\"></label><div class=\"mb-row3\"><label class=\"mb-field\"><span>City</span><input type=\"text\" id=\"mb-pcity\" maxlength=\"60\" value=\"" + Ui.Attr(me.City) + "\"></label><label class=\"mb-field\"><span>State</span><input type=\"text\" id=\"mb-pregion\" maxlength=\"30\" value=\"" + Ui.Attr(me.Region) + "\"></label><label class=\"mb-field\"><span>ZIP</span><input type=\"text\" id=\"mb-pzip\" maxlength=\"12\" value=\"" + Ui.Attr(me.Zip) + "\"></label></div><em class=\"mb-err\" id=\"mb-e-prof\" role=\"alert\"></em><div><button type=\"submit\" class=\"mb-btn mb-btn-accent\">Save details</button></div></form></section><div class=\"mb-ocol\"><section class=\"mb-box\"><h2>Password</h2><form class=\"mb-form\" onsubmit=\"return MemberJs.savePass()\" novalidate><label class=\"mb-field\"><span>Current password</span><input type=\"password\" id=\"mb-pcur\" maxlength=\"100\" autocomplete=\"current-password\"></label><label class=\"mb-field\"><span>New password</span><input type=\"password\" id=\"mb-pnew\" maxlength=\"100\" autocomplete=\"new-password\"></label><em class=\"mb-err\" id=\"mb-e-pw\" role=\"alert\"></em><div><button type=\"submit\" class=\"mb-btn\">Change password</button></div></form></section><section class=\"mb-box\"><h2>Sign out</h2><p class=\"mb-hint\">Sign out on shared computers.</p><button type=\"button\" class=\"mb-btn mb-btn-ghost\" onclick=\"MemberJs.signOutMember()\">Sign out</button></section></div></div>";
        }

        private async Task<(SiteData Site, string Sbx, global::Sport.Models.ClubMember? Me)> Mine()
        {
            return await Context();
        }

        private static string TabHtml(SiteData site, global::Sport.Models.ClubMember me, string tab, string welcome)
        {
            return tab switch
            {
                "classes" => ClassesTab(site, me),
                "family" => FamilyTab(site, me),
                "visits" => VisitsTab(site, me),
                "payments" => PaymentsTab(site, me),
                "points" => PointsTab(site, me),
                "profile" => ProfileTab(site, me),
                _ => Overview(site, me, welcome)
            };
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Mine();
            if (me == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string tab = Tabs.Any(t => t.Key == GetDataValue("mtab")) ? GetDataValue("mtab") : "overview";
            response.SetElementContents("mb-mhead", Head(site, me));
            response.SetElementContents("mb-mbody", TabHtml(site, me, tab, Ui.Clip(GetDataValue("mwel"), 2)));
            response.ExecuteScript("MemberJs.loaded('" + tab + "');");
            return response;
        }

        private static string Modal(string title, string body)
        {
            return "<h2>" + title + "</h2>" + body;
        }

        public async Task<ApiResponse> Receipt()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Mine();
            Registration? r = me == null ? null : site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Member == me.Id);
            if (r == null)
            {
                return response;
            }
            response.SetElementContents("mb-mpanel", Modal("Receipt", ReceiptBox("mb", Rules.Receipt(site, r, "mb"), r.Code)));
            response.ExecuteScript("MemberJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> PassReceipt()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Mine();
            DayPass? d = me == null ? null : site.Passes.FirstOrDefault(x => x.Id == GetDataValue("pid") && x.Member == me.Id);
            if (d == null)
            {
                return response;
            }
            response.SetElementContents("mb-mpanel", Modal("Receipt", ReceiptBox("mb", Rules.PassReceipt(site, d, "mb"), d.Code)));
            response.ExecuteScript("MemberJs.openModal();");
            return response;
        }

        private static string PaySummary(Settings s, Quote q, bool fixedTotal, Registration r)
        {
            StringBuilder sb = new StringBuilder("<div><span>Class fee</span><b>" + Ui.Money(fixedTotal ? r.Price : q.Price, true) + "</b></div>");
            decimal disc = fixedTotal ? r.Discount : q.Discount;
            decimal poff = fixedTotal ? r.PointsOff : q.PointsOff;
            int pts = fixedTotal ? r.Points : q.Points;
            if (disc > 0)
            {
                sb.Append("<div class=\"mb-save\"><span>" + Ui.Enc(Rules.TierName(fixedTotal ? r.Tier : q.Tier)) + " discount</span><b>&minus;" + Ui.Money(disc, true) + "</b></div>");
            }
            if (poff > 0)
            {
                sb.Append("<div class=\"mb-save\"><span>" + Ui.Num(pts) + " points</span><b>&minus;" + Ui.Money(poff, true) + "</b></div>");
            }
            sb.Append("<div class=\"mb-tot\"><span>Total</span><b>" + Ui.Money(fixedTotal ? r.Total : q.Total, true) + "</b></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> PayForm()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Mine();
            Registration? r = me == null ? null : site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Member == me.Id && x.Status is "offered" or "pending");
            SportClass? k = r == null ? null : site.Classes.FirstOrDefault(x => x.Id == r.Class);
            if (r == null || k == null || me == null)
            {
                response.ExecuteScript("MemberJs.toast('This seat is no longer waiting for payment.');MemberJs.tab('classes');");
                return response;
            }
            bool fixedTotal = r.Status == "pending";
            Quote q = Rules.Price(site, k, me, 0);
            StringBuilder sb = new StringBuilder("<p class=\"mb-hint\">" + Ui.Enc(k.Title) + " &middot; " + Ui.Enc(r.Name) + "</p><form class=\"mb-form\" onsubmit=\"return MemberJs.pay('" + Ui.Attr(r.Id) + "')\" novalidate>");
            if (!fixedTotal && q.MaxPoints > 0)
            {
                int ppd = Math.Max(1, site.Settings.PointsPerDollar);
                sb.Append("<label class=\"mb-field\"><span>Use points (you have " + Ui.Num(me.Balance) + ")</span><select id=\"mb-ppts\" onchange=\"MemberJs.payQuote('" + Ui.Attr(r.Id) + "')\"><option value=\"0\">Don't use points</option>");
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
            sb.Append("<div class=\"mb-sum\" id=\"mb-psum\">" + PaySummary(site.Settings, q, fixedTotal, r) + "</div>");
            if (me.Street == string.Empty)
            {
                sb.Append("<b>Billing address</b><label class=\"mb-field\"><span>Street</span><input type=\"text\" id=\"mb-bstreet\" maxlength=\"100\"></label><div class=\"mb-row3\"><label class=\"mb-field\"><span>City</span><input type=\"text\" id=\"mb-bcity\" maxlength=\"60\"></label><label class=\"mb-field\"><span>State</span><input type=\"text\" id=\"mb-bregion\" maxlength=\"30\"></label><label class=\"mb-field\"><span>ZIP</span><input type=\"text\" id=\"mb-bzip\" maxlength=\"12\"></label></div>");
            }
            sb.Append(CardForm("mb", "MemberJs", Rules.FullName(me), string.Empty, string.Empty) + "<em class=\"mb-err\" id=\"mb-e-pay\" role=\"alert\"></em><button type=\"submit\" class=\"mb-btn mb-btn-accent mb-btn-wide\" id=\"mb-paygo\">Pay " + Ui.Plain(fixedTotal ? r.Total : q.Total) + "</button></form>");
            response.SetElementContents("mb-mpanel", Modal(r.Status == "offered" ? "Pay for your seat" : "Pay online", sb.ToString()));
            response.ExecuteScript("MemberJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> PayQuote()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Mine();
            Registration? r = me == null ? null : site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Member == me.Id && x.Status == "offered");
            SportClass? k = r == null ? null : site.Classes.FirstOrDefault(x => x.Id == r.Class);
            if (r == null || k == null || me == null)
            {
                return response;
            }
            Quote q = Rules.Price(site, k, me, int.TryParse(GetDataValue("pts"), out int p) ? p : 0);
            response.SetElementContents("mb-psum", PaySummary(site.Settings, q, false, r));
            response.ExecuteScript("document.getElementById('mb-paygo').textContent='" + Ui.Js("Pay " + Ui.Plain(q.Total)) + "';");
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            CardIn card = Auth.ReadCard(GetDataValue("cname"), GetDataValue("cnum"), GetDataValue("cexp"), GetDataValue("ccvc"));
            if (CardErrors(response, "mb", card))
            {
                response.ExecuteScript("MemberJs.paid(false);");
                return response;
            }
            if (card.Declined)
            {
                response.SetElementContents("mb-e-pay", "Your card was declined. (This is the demo decline card. Try 4242 4242 4242 4242.)");
                response.ExecuteScript("MemberJs.paid(false);");
                return response;
            }
            string err = string.Empty;
            Registration? done = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                global::Sport.Models.ClubMember? me = site.Members.FirstOrDefault(x => x.Id == mc.Id && x.Type == "member" && x.Status == "active");
                Registration? r = me == null ? null : site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Member == me.Id && x.Status is "offered" or "pending");
                SportClass? k = r == null ? null : site.Classes.FirstOrDefault(x => x.Id == r.Class);
                if (r == null || k == null || me == null)
                {
                    err = "This seat is no longer waiting for payment.";
                }
                else
                {
                    if (me.Street == string.Empty)
                    {
                        string street = Ui.OneLine(GetDataValue("bstreet"), 100);
                        string city = Ui.OneLine(GetDataValue("bcity"), 60);
                        string zip = Ui.OneLine(GetDataValue("bzip"), 12);
                        if (street.Length < 4 || city.Length < 2 || zip.Length < 3)
                        {
                            err = "Enter your billing address: street, city and ZIP.";
                        }
                        else
                        {
                            me.Street = street;
                            me.City = city;
                            me.Region = Ui.OneLine(GetDataValue("bregion"), 30);
                            me.Zip = zip;
                        }
                    }
                    if (err == string.Empty)
                    {
                        DateTime nowU = DateTime.UtcNow;
                        if (r.Status == "offered")
                        {
                            Quote q = Rules.Price(site, k, me, int.TryParse(GetDataValue("pts"), out int p) ? p : 0);
                            r.Price = q.Price;
                            r.Tier = q.Tier;
                            r.Discount = q.Discount;
                            r.Points = q.Points;
                            r.PointsOff = q.PointsOff;
                            r.Total = q.Total;
                            if (q.Points > 0)
                            {
                                Rules.AddPoints(site, me, -q.Points, "spend", r.Id, "Used on " + r.Code, "web", nowU);
                            }
                        }
                        Rules.Confirm(site, r, "card", card.Brand, card.Last4, "web", nowU);
                        await Store.SaveSite(site, mc.Sbx);
                        done = r;
                    }
                }
            }
            if (done == null)
            {
                response.SetElementContents("mb-e-pay", Ui.Enc(err));
                response.ExecuteScript("MemberJs.paid(false);");
                return response;
            }
            await Store.Log(mc.Sbx, "web", "pay", done.Class + " · " + done.Code + " · " + Ui.Plain(done.Total), true);
            response.ExecuteScript("MemberJs.paid(true);MemberJs.toast('Paid. Your seat is confirmed.');");
            return response;
        }

        public async Task<ApiResponse> CancelReg()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string msg = "This registration can't be cancelled online.";
            bool ok = false;
            string target = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Member == mc.Id);
                if (r != null)
                {
                    (ok, msg) = Rules.CancelOnline(site, r, DateTime.UtcNow);
                    if (ok)
                    {
                        target = r.Class + " · " + r.Code;
                        await Store.SaveSite(site, mc.Sbx);
                    }
                }
            }
            if (ok)
            {
                await Store.Log(mc.Sbx, "web", "cancel", target + " · " + msg, true);
            }
            response.ExecuteScript("MemberJs.toast('" + Ui.Js(msg) + "');MemberJs.reload();");
            return response;
        }

        public async Task<ApiResponse> AskRefund()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string reason = Ui.Clip(GetDataValue("areason"), 600);
            string msg = string.Empty;
            bool ok = false;
            string target = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Member == mc.Id);
                if (r != null)
                {
                    (ok, msg) = Rules.AskRefund(site, r, reason, DateTime.UtcNow);
                    if (ok)
                    {
                        target = r.Class + " · " + r.Code;
                        await Store.SaveSite(site, mc.Sbx);
                    }
                }
            }
            if (!ok)
            {
                response.SetElementContents("mb-e-areason", Ui.Enc(msg));
                return response;
            }
            await Store.Log(mc.Sbx, "web", "refund request", target, true);
            response.ExecuteScript("MemberJs.closeModal();MemberJs.toast('" + Ui.Js(msg) + "');MemberJs.reload();");
            return response;
        }

        public async Task<ApiResponse> FamForm()
        {
            ApiResponse response = new ApiResponse();
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Mine();
            if (me == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            FamilyMember? f = me.Family.FirstOrDefault(x => x.Id == GetDataValue("fid"));
            bool busy = f != null && site.Registrations.Any(r => r.Member == me.Id && r.Person == f.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist");
            string body = "<form class=\"mb-form\" onsubmit=\"return MemberJs.famSave('" + Ui.Attr(f?.Id ?? string.Empty) + "')\" novalidate><div class=\"mb-row2\"><label class=\"mb-field\"><span>First name</span><input type=\"text\" id=\"mb-ffirst\" maxlength=\"40\" value=\"" + Ui.Attr(f?.First ?? string.Empty) + "\"></label><label class=\"mb-field\"><span>Last name</span><input type=\"text\" id=\"mb-flast\" maxlength=\"40\" value=\"" + Ui.Attr(f?.Last ?? me.Last) + "\"></label></div><div class=\"mb-row2\"><label class=\"mb-field\"><span>Date of birth</span><input type=\"date\" id=\"mb-fborn\" max=\"" + Ui.Ymd(Store.Today(site)) + "\" min=\"1920-01-01\" value=\"" + (f != null ? Ui.Ymd(f.Born) : string.Empty) + "\"></label><label class=\"mb-field\"><span>Relation</span><select id=\"mb-frel\">" + Ui.Opts(Rules.Relations, f?.Relation ?? "child") + "</select></label></div><em class=\"mb-err\" id=\"mb-e-fam\" role=\"alert\"></em><div class=\"mb-mfoot\">" + (f != null ? (busy ? "<span class=\"mb-hint mb-left\">Booked in a class, so it can't be removed.</span>" : "<button type=\"button\" class=\"mb-btn mb-btn-danger mb-left\" onclick=\"MemberJs.famDrop('" + Ui.Attr(f.Id) + "')\">Remove</button>") : string.Empty) + "<button type=\"button\" class=\"mb-btn mb-btn-ghost\" onclick=\"MemberJs.closeModal()\">Cancel</button><button type=\"submit\" class=\"mb-btn mb-btn-accent\">Save</button></div></form>";
            response.SetElementContents("mb-mpanel", Modal(f == null ? "Add a family member" : "Edit " + Ui.Enc(f.First), body));
            response.ExecuteScript("MemberJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> FamSave()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string first = Ui.OneLine(GetDataValue("ffirst"), 40);
            string last = Ui.OneLine(GetDataValue("flast"), 40);
            DateTime? born = Ui.ParseDay(GetDataValue("fborn"));
            string rel = Rules.Relations.Any(x => x.Key == GetDataValue("frel")) ? GetDataValue("frel") : "child";
            if (first.Length < 1)
            {
                response.SetElementContents("mb-e-fam", "Enter a first name.");
                return response;
            }
            if (born == null || born.Value.Year < 1920 || born.Value > DateTime.UtcNow.Date)
            {
                response.SetElementContents("mb-e-fam", "Enter a date of birth. We use it to check class ages.");
                return response;
            }
            string err = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                global::Sport.Models.ClubMember? me = site.Members.FirstOrDefault(x => x.Id == mc.Id && x.Type == "member");
                if (me == null)
                {
                    err = "Please sign in again.";
                }
                else
                {
                    FamilyMember? f = me.Family.FirstOrDefault(x => x.Id == GetDataValue("fid"));
                    if (f == null)
                    {
                        if (me.Family.Count >= 10)
                        {
                            err = "That's the most family members on one account.";
                        }
                        else
                        {
                            f = new FamilyMember { Id = Store.NextId(site.Members.SelectMany(m => m.Family).Select(x => x.Id), "F-", 100) };
                            me.Family.Add(f);
                        }
                    }
                    if (f != null && err == string.Empty)
                    {
                        f.First = first;
                        f.Last = last;
                        f.Born = born.Value;
                        f.Relation = rel;
                        await Store.SaveSite(site, mc.Sbx);
                    }
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("mb-e-fam", err);
                return response;
            }
            await Store.Log(mc.Sbx, "web", "family", first, true);
            response.ExecuteScript("MemberJs.closeModal();MemberJs.toast('Saved');MemberJs.reload();");
            return response;
        }

        public async Task<ApiResponse> FamDrop()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string msg = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                global::Sport.Models.ClubMember? me = site.Members.FirstOrDefault(x => x.Id == mc.Id);
                FamilyMember? f = me?.Family.FirstOrDefault(x => x.Id == GetDataValue("fid"));
                if (me != null && f != null)
                {
                    if (site.Registrations.Any(r => r.Member == me.Id && r.Person == f.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist"))
                    {
                        msg = "Booked in a class, so it can't be removed.";
                    }
                    else
                    {
                        me.Family.Remove(f);
                        await Store.SaveSite(site, mc.Sbx);
                        msg = "Removed.";
                    }
                }
            }
            response.ExecuteScript("MemberJs.closeModal();MemberJs.toast('" + Ui.Js(msg) + "');MemberJs.reload();");
            return response;
        }

        public async Task<ApiResponse> SaveProfile()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string first = Ui.OneLine(GetDataValue("pfirst"), 40);
            string last = Ui.OneLine(GetDataValue("plast"), 40);
            string phone = Ui.CleanPhone(GetDataValue("pphone"));
            DateTime? born = Ui.ParseDay(GetDataValue("pborn"));
            if (first.Length < 1 || last.Length < 1)
            {
                response.SetElementContents("mb-e-prof", "Enter your first and last name.");
                return response;
            }
            if (born == null || born.Value.Year < 1920 || born.Value > DateTime.UtcNow.Date.AddYears(-16))
            {
                response.SetElementContents("mb-e-prof", "Enter your date of birth (16 or older).");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                global::Sport.Models.ClubMember? me = site.Members.FirstOrDefault(x => x.Id == mc.Id);
                if (me != null)
                {
                    me.First = first;
                    me.Last = last;
                    me.Phone = phone;
                    me.Born = born;
                    me.Street = Ui.OneLine(GetDataValue("pstreet"), 100);
                    me.City = Ui.OneLine(GetDataValue("pcity"), 60);
                    me.Region = Ui.OneLine(GetDataValue("pregion"), 30);
                    me.Zip = Ui.OneLine(GetDataValue("pzip"), 12);
                    await Store.SaveSite(site, mc.Sbx);
                }
            }
            await Store.Log(mc.Sbx, "web", "profile", first + " " + last, true);
            response.ExecuteScript("MemberJs.toast('Saved');MemberJs.reload();");
            return response;
        }

        public async Task<ApiResponse> SavePass()
        {
            ApiResponse response = new ApiResponse();
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            if (mc == null)
            {
                response.ExecuteScript("MemberJs.needSignIn();");
                return response;
            }
            string cur = GetDataValue("pcur");
            string nw = GetDataValue("pnew");
            if (nw.Length < 8 || nw.Length > 100)
            {
                response.SetElementContents("mb-e-pw", "Use a new password of 8 characters or more.");
                return response;
            }
            string err = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(mc.Sbx);
                global::Sport.Models.ClubMember? me = site.Members.FirstOrDefault(x => x.Id == mc.Id);
                if (me == null || !Access.Verify(cur, me.Hash))
                {
                    err = "Your current password isn't right.";
                }
                else
                {
                    me.Hash = Access.HashPassword(nw);
                    await Store.SaveSite(site, mc.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("mb-e-pw", err);
                return response;
            }
            await Auth.CloseUser(mc.Sbx, mc.Id, Store.CleanId(GetDataValue("mtok")), "member");
            response.ExecuteScript("document.getElementById('mb-pcur').value='';document.getElementById('mb-pnew').value='';MemberJs.toast('Password changed');");
            return response;
        }

        public async Task<ApiResponse> SignOutMember()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("mtok"));
            response.ExecuteScript("MemberJs.signedOut();location.href='Home';");
            return response;
        }

        private static string TierLadder(SiteData site, string p, string cur)
        {
            Settings s = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-tiers\">");
            foreach ((string k, string n) in Rules.Tiers)
            {
                int at = Rules.TierAt(s, k);
                decimal off = Rules.TierOff(s, k);
                List<string> perks = new List<string>();
                perks.Add(off > 0 ? off.ToString("0.##", Ui.Inv) + "% off every class" : "Member prices and points");
                perks.Add("Earn " + s.PerDollar + " point per $1 and " + s.PerSession + " per session");
                if (Rules.Early(s, k))
                {
                    perks.Add("Register " + s.EarlyHours + " hours early");
                }
                perks.Add(Ui.Num(Math.Max(1, s.PointsPerDollar)) + " points = $1 off");
                sb.Append("<div class=\"" + p + "-tc " + p + "-tc-" + k + (k == cur ? " " + p + "-tc-on" : string.Empty) + "\"><span class=\"" + p + "-tier " + p + "-t-" + k + "\">" + n + "</span><b>" + (at == 0 ? "0 points" : Ui.Num(at) + " points") + "</b><small>" + (at == 0 ? "When you sign up" : "Lifetime points") + "</small><ul>");
                foreach (string perk in perks)
                {
                    sb.Append("<li>" + Ui.Enc(perk) + "</li>");
                }
                sb.Append("</ul>" + (k == cur ? "<em>Your tier</em>" : string.Empty) + "</div>");
            }
            sb.Append("</div>");
            return sb.ToString();
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
            return inner + "<div class=\"" + p + "-ract\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"MemberJs.printBox('" + p + "-rcpt-" + Ui.Attr(code) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 9V3h10v6M7 17H4v-7h16v7h-3\"/><path d=\"M7 14h10v7H7z\"/></svg> Print receipt</button></div>";
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
