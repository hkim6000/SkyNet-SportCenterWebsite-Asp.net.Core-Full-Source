using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class Home : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--hm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("hm", v.Nav, false))
                .Replace("{plhd_drawer}", Nav("hm", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("HomeJs.hydrated(false);");
                return response;
            }
            qd = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Ui.Clip(GetDataValue("qstr"), 600)))
            {
                qd[kv.Key] = kv.Value.ToString();
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = await Render(site);
            response.SetElementContents("hm-main", v.Main);
            response.SetElementContents("hm-bname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-btown", Ui.Enc(site.Settings.Town));
            response.SetElementContents("hm-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-copyname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("hm-ftag", Ui.Enc(site.Settings.Tagline));
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("HomeJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "'," + Store.MinutesLeft(sbx) + ");");
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
                response.ExecuteScript("HomeJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("HomeJs.who('', '', '');");
                return response;
            }
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            response.ExecuteScript("HomeJs.who('" + Ui.Js(me.First) + "','" + tier + "','" + Rules.TierName(tier) + "');");
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
                return "<i class=\"hm-shut\"></i>Opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"hm-shut\"></i>Closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"hm-shut\"></i>Closed";
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
            return "<article class=\"hm-kc\"><a class=\"hm-kc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) + "<span class=\"hm-kc-tag\">" + Ui.Enc(Ui.SportName(site, k.Sport)) + "</span><span class=\"hm-kc-st\">" + Ui.StatePill("hm", st) + "</span></a><div class=\"hm-kc-b\"><span class=\"hm-kc-t\"><a href=\"" + href + "\">" + Ui.Enc(k.Title) + "</a></span><span class=\"hm-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> " + Ui.ClassWhen(site, k) + "</span><span class=\"hm-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> " + Ui.Range(k) + "</span><span class=\"hm-meta\"><span class=\"hm-chipx\">" + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><span class=\"hm-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"hm-chipx\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span></span><span class=\"hm-kc-f\"><b>" + Ui.Money(k.Price) + "</b><small>" + Ui.Plural(k.Dates.Count, "session", "sessions") + " &middot; " + seats + "</small></span></div></article>";
        }

        private static int OpenCount(SiteData site, string sport, DateTime now)
        {
            return site.Classes.Count(k => k.Sport == sport && Rules.State(site, k, now) is "open" or "few" or "full");
        }

        private async Task<View> Render(SiteData site)
        {
            return new View { Title = string.Empty, Desc = site.Settings.Tagline, Main = Page(site, "home", Store.Today(site)), Nav = "home" };
        }

        private string DataPart(SiteData site, Block b, DateTime today)
        {
            Settings s = site.Settings;
            DateTime now = Store.Now(site);
            string kick = Kick(site, b);
            string h2 = H2(site, b);
            StringBuilder sb = new StringBuilder();
            switch (b.Source)
            {
                case "sportgrid":
                    List<SportType> sports = site.Sports.Where(x => x.Published).OrderBy(x => x.Order).ToList();
                    if (sports.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append("<div class=\"hm-shead\"><div>" + kick + h2 + Rich(site, b.Text, "hm-lead") + "</div><a class=\"hm-more\" href=\"Sports\">All sports &rarr;</a></div><div class=\"hm-sgrid\">");
                    foreach (SportType x in sports)
                    {
                        int n = OpenCount(site, x.Id, now);
                        sb.Append("<a class=\"hm-stile\" href=\"Sport?id=" + Uri.EscapeDataString(x.Id) + "\">" + Ui.Pic(x.Image, string.Empty, string.Empty, true) + "<span class=\"hm-stile-in\"><span class=\"hm-sico\">" + Icon(x.Icon) + "</span><b>" + Ui.Enc(x.Name) + "</b><small>" + (n > 0 ? Ui.Plural(n, "class", "classes") + " open" : "New classes soon") + "</small></span></a>");
                    }
                    sb.Append("</div>");
                    return Sec(b, sb.ToString());
                case "upcoming":
                    List<SportClass> soon = site.Classes.Where(k => Rules.State(site, k, now) is "open" or "few" or "full").OrderBy(k => Rules.Start(k)).Take(6).ToList();
                    if (soon.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append("<div class=\"hm-shead\"><div>" + kick + h2 + "</div><a class=\"hm-more\" href=\"Classes\">Full schedule &rarr;</a></div><div class=\"hm-cards\">");
                    foreach (SportClass k in soon)
                    {
                        sb.Append(ClassCard(site, k, now));
                    }
                    sb.Append("</div>");
                    return Sec(b, sb.ToString());
                case "tiers":
                    sb.Append("<div class=\"hm-tband\"><div class=\"hm-shead\"><div>" + kick + h2 + Rich(site, b.Text, "hm-lead") + "</div>" + Btns(site, b) + "</div>" + TierLadder(site, "hm", string.Empty) + "</div>");
                    return Sec(b, sb.ToString());
                case "coaches":
                    List<global::Sport.Models.Coach> coaches = site.Instructors.Where(x => x.Published).OrderBy(x => x.Order).Take(4).ToList();
                    if (coaches.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append("<div class=\"hm-shead\"><div>" + kick + h2 + "</div><a class=\"hm-more\" href=\"Instructors\">Meet the team &rarr;</a></div><div class=\"hm-cards hm-c4\">");
                    foreach (global::Sport.Models.Coach x in coaches)
                    {
                        sb.Append("<a class=\"hm-coach\" href=\"Instructor?id=" + Uri.EscapeDataString(x.Id) + "\">" + Ui.Face(x.Photo, x.Name, string.Empty) + "<b>" + Ui.Enc(x.Name) + "</b><small>" + Ui.Enc(x.Title) + "</small><span class=\"hm-meta\">" + string.Concat(x.Sports.Select(sp => "<span class=\"hm-chipx\">" + Ui.Enc(Ui.SportName(site, sp)) + "</span>")) + "</span></a>");
                    }
                    sb.Append("</div>");
                    return Sec(b, sb.ToString());
                case "passband":
                    sb.Append("<div class=\"hm-pband\"><span class=\"hm-pico\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16v3a2 2 0 0 0 0 4v3H4v-3a2 2 0 0 0 0-4z\"/><path d=\"M14 7.5v9\" stroke-dasharray=\"2 2\"/></svg></span><div>" + kick + h2 + Rich(site, b.Text, "hm-lead") + "</div><div class=\"hm-pprice\"><b>" + Ui.Money(s.PassPrice) + "</b><small>" + Ui.Enc(s.PassName) + "</small><a class=\"hm-btn hm-btn-accent\" href=\"Pass\">" + Ui.Enc(b.Label != string.Empty ? b.Label : "Buy a day pass") + "</a></div></div>");
                    return Sec(b, sb.ToString());
            }
            return string.Empty;
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

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Settings.Name).Replace("{town}", site.Settings.Town);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Enc(site.Settings.Name)).Replace("{town}", Ui.Enc(site.Settings.Town));
        }

        private static string Lines(string v)
        {
            return Ui.Enc(v).Replace("\n", "<br>");
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

        private static string Mv(SiteData site, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = site.Media.FirstOrDefault(x => x.Full == src);
            int w = m != null && m.Width > 0 ? m.Width : 1600;
            int h = m != null && m.Height > 0 ? m.Height : 1200;
            if (Ui.IsVideo(src))
            {
                return "<video src=\"" + Ui.Attr(src) + "\" poster=\"" + Ui.Attr(m?.Thumb ?? string.Empty) + "\" width=\"" + w + "\" height=\"" + h + "\" playsinline " + (bg ? "autoplay muted loop data-bg=\"1\" aria-hidden=\"true\"" : "controls preload=\"none\" aria-label=\"" + Ui.Attr(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Attr(src) + "\" alt=\"" + Ui.Attr(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\" width=\"" + w + "\" height=\"" + h + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"hm-btn " + (main ? "hm-btn-accent" : "hm-btn-ghost") + "\" href=\"" + Ui.Attr(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Enc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"hm-hero-b\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<span class=\"hm-kick\">" + Ui.Enc(Tok(site, b.Kicker)) + "</span>";
        }

        private static string H2(SiteData site, Block b)
        {
            return b.Title == string.Empty ? string.Empty : "<h2 class=\"hm-h2\">" + Ui.Enc(Tok(site, b.Title)) + "</h2>";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"hm-rich " + cls + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead)
        {
            string x = Kick(site, b) + H2(site, b) + (lead ? Rich(site, b.Text, "hm-lead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"hm-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"hm-sec hm-wrap\" id=\"" + Ui.Attr(b.Id) + "\">" + inner + "</section>";
        }

        private static string Finder(SiteData site)
        {
            return "<form class=\"hm-find\" action=\"Classes\" method=\"get\"><label class=\"hm-field\"><span>Sport</span><select name=\"sport\"><option value=\"\">Any sport</option>" + Ui.Opts(site.Sports.Where(x => x.Published).OrderBy(x => x.Order).Select(x => (x.Id, x.Name)), string.Empty) + "</select></label><label class=\"hm-field\"><span>Who is it for?</span><select name=\"age\"><option value=\"\">Anyone</option>" + Ui.Opts(new[] { ("kids", "Kids, 4 to 12"), ("teens", "Teens, 13 to 17"), ("adults", "Adults, 18+") }, string.Empty) + "</select></label><label class=\"hm-field\"><span>Day</span><select name=\"day\"><option value=\"\">Any day</option>" + Ui.Opts(new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(d => (d.ToString(Ui.Inv), Ui.DayNames[d])), string.Empty) + "</select></label><button type=\"submit\" class=\"hm-btn hm-btn-accent\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4.5 4.5\"/></svg> Find a class</button></form>";
        }

        private static string Stats(SiteData site, DateTime today)
        {
            DateTime now = Store.Now(site);
            int open = site.Classes.Count(k => Rules.State(site, k, now) is "open" or "few" or "full");
            return "<div class=\"hm-hstats\"><a href=\"Sports\"><b>" + site.Sports.Count(x => x.Published) + "</b><span>sports under one roof</span></a><a href=\"Classes\"><b>" + open + "</b><span>classes open now</span></a><a href=\"Instructors\"><b>" + site.Instructors.Count(x => x.Published) + "</b><span>certified coaches</span></a></div>";
        }

        private static string Hero(SiteData site, Block b, DateTime today, bool h1, string title, string text)
        {
            bool find = b.Extra == "finder";
            string tag = h1 ? "h1" : "h2";
            string media = Mv(site, b.Media, Tok(site, b.Title), Ui.IsVideo(b.Media), h1);
            string t = title != string.Empty ? Ui.Enc(title) : Ui.Enc(Tok(site, b.Title));
            string lead = text != string.Empty ? "<p class=\"hm-lead\">" + Ui.Enc(text) + "</p>" : Rich(site, b.Text, "hm-lead");
            return "<section class=\"hm-bhero" + (find ? " hm-bhome" : string.Empty) + (media == string.Empty ? " hm-bplain" : string.Empty) + "\" id=\"" + Ui.Attr(b.Id) + "\"><div class=\"hm-wrap hm-bhero-in\"><div class=\"hm-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"hm-btitle\">" + t + "</" + tag + ">" + lead + Btns(site, b) + (find ? Finder(site) : string.Empty) + "</div>" + (media != string.Empty ? "<div class=\"hm-bhero-p\">" + media + (find ? Stats(site, today) : string.Empty) + "</div>" : string.Empty) + "</div></section>";
        }

        private static string Split(SiteData site, Block b, DateTime today)
        {
            string counts = b.Extra == "counts" ? "<div class=\"hm-nums\"><span><b>" + site.Sports.Count(x => x.Published) + "</b>sports</span><span><b>" + site.Classes.Count(k => k.Published && k.Status == "active" && Rules.End(k) > Store.Now(site)) + "</b>classes this season</span><span><b>" + site.Instructors.Count(x => x.Published) + "</b>instructors</span></div>" : string.Empty;
            string btn = Btn(site, b.Label, b.Link, false);
            return Sec(b, "<div class=\"hm-bsplit" + (b.Flip ? " hm-flip" : string.Empty) + (b.Media == string.Empty ? " hm-nomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"hm-bsm\">" + Mv(site, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "hm-lead") + counts + (btn != string.Empty ? "<div class=\"hm-hero-b\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"hm-bcards hm-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"hm-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Enc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"hm-bcard\" href=\"" + Ui.Attr(it.Link) + "\">" + inner + "<span class=\"hm-more\">" + Ui.Enc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"hm-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"hm-bsteps hm-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><span>" + i++ + "</span><b>" + Ui.Enc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"hm-bstats hm-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Enc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"hm-bpeople hm-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"hm-person\">" + (it.Media != string.Empty && !Ui.IsVideo(it.Media) ? Mv(site, it.Media, it.Title, false, false) : "<span class=\"hm-noface\" style=\"--h:" + Ui.Hue(it.Title) + "\">" + Ui.Enc(Ui.Initials(it.Title)) + "</span>") + "<b>" + Ui.Enc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"hm-bboxes hm-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"hm-box\">" + (it.Title != string.Empty ? "<h2>" + Ui.Enc(Tok(site, it.Title)) + "</h2>" : string.Empty) + "<ul class=\"hm-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>CHECKICON<span>" + Ui.Enc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"hm-bfaq\">" + Head(site, b, false) + "<div class=\"hm-faq\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details><summary>" + Ui.Enc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, Block b)
        {
            return Sec(b, "<figure class=\"hm-bquote\">" + (b.Media != string.Empty && !Ui.IsVideo(b.Media) ? Mv(site, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Enc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"hm-bmedia\">" + Mv(site, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Enc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"hm-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "hm-lead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string Page(SiteData site, string key, DateTime today, string title = "", string text = "")
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, b, today, first, first ? title : string.Empty, first ? text : string.Empty),
                    "text" => Sec(b, "<div class=\"hm-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, b, today),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, b),
                    "boxes" => Boxes(site, b),
                    "faq" => Faq(site, b),
                    "quote" => Quote(site, b),
                    "media" => Wide(site, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, b, today),
                    _ => string.Empty
                };
                if (b.Type == "hero")
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }
    }
}
