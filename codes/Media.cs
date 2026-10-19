using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class Media : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--md-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("md", v.Nav, false))
                .Replace("{plhd_drawer}", Nav("md", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("MediaJs.hydrated(false);");
                return response;
            }
            qd = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Ui.Clip(GetDataValue("qstr"), 600)))
            {
                qd[kv.Key] = kv.Value.ToString();
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = await Render(site);
            response.SetElementContents("md-main", v.Main);
            response.SetElementContents("md-bname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("md-btown", Ui.Enc(site.Settings.Town));
            response.SetElementContents("md-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("md-copyname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("md-ftag", Ui.Enc(site.Settings.Tagline));
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MediaJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "'," + Store.MinutesLeft(sbx) + ");");
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
                response.ExecuteScript("MediaJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("MediaJs.who('', '', '');");
                return response;
            }
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            response.ExecuteScript("MediaJs.who('" + Ui.Js(me.First) + "','" + tier + "','" + Rules.TierName(tier) + "');");
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
                return "<i class=\"md-shut\"></i>Opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"md-shut\"></i>Closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"md-shut\"></i>Closed";
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
            return "<article class=\"md-kc\"><a class=\"md-kc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) + "<span class=\"md-kc-tag\">" + Ui.Enc(Ui.SportName(site, k.Sport)) + "</span><span class=\"md-kc-st\">" + Ui.StatePill("md", st) + "</span></a><div class=\"md-kc-b\"><span class=\"md-kc-t\"><a href=\"" + href + "\">" + Ui.Enc(k.Title) + "</a></span><span class=\"md-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> " + Ui.ClassWhen(site, k) + "</span><span class=\"md-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> " + Ui.Range(k) + "</span><span class=\"md-meta\"><span class=\"md-chipx\">" + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><span class=\"md-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"md-chipx\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span></span><span class=\"md-kc-f\"><b>" + Ui.Money(k.Price) + "</b><small>" + Ui.Plural(k.Dates.Count, "session", "sessions") + " &middot; " + seats + "</small></span></div></article>";
        }

        private static int OpenCount(SiteData site, string sport, DateTime now)
        {
            return site.Classes.Count(k => k.Sport == sport && Rules.State(site, k, now) is "open" or "few" or "full");
        }

        private async Task<View> Render(SiteData site)
        {
            return new View { Title = "Media Center", Desc = "Photos, videos, the program guide and forms from " + site.Settings.Name + ".", Main = Page(site, "media", Store.Today(site)) + "<div class=\"md-lb\" id=\"md-lb\" hidden role=\"dialog\" aria-modal=\"true\" aria-label=\"Album\"><button type=\"button\" class=\"md-lbx\" aria-label=\"Close\" onclick=\"MediaJs.lbClose()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button><button type=\"button\" class=\"md-lbp\" aria-label=\"Previous\" onclick=\"MediaJs.lbGo(-1)\"><svg viewBox=\"0 0 24 24\" width=\"26\" height=\"26\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 5l-7 7 7 7\"/></svg></button><figure><img id=\"md-lbimg\" alt=\"\"><figcaption id=\"md-lbcap\"></figcaption></figure><button type=\"button\" class=\"md-lbn\" aria-label=\"Next\" onclick=\"MediaJs.lbGo(1)\"><svg viewBox=\"0 0 24 24\" width=\"26\" height=\"26\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 5l7 7-7 7\"/></svg></button></div>", Nav = "media" };
        }

        private static List<MediaItem> Tagged(SiteData site, string kind)
        {
            return site.Media.Where(m => m.Kind == kind && m.Tags.Contains("media-center")).OrderByDescending(m => m.When).ToList();
        }

        private string DataPart(SiteData site, Block b, DateTime today)
        {
            string head = Kick(site, b) + H2(site, b);
            StringBuilder sb = new StringBuilder();
            switch (b.Source)
            {
                case "videos":
                    List<MediaItem> vids = Tagged(site, "video");
                    if (vids.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append((head != string.Empty ? "<div class=\"md-bhead\">" + head + "</div>" : string.Empty) + "<div class=\"md-vgrid\">");
                    foreach (MediaItem m in vids)
                    {
                        sb.Append("<figure class=\"md-vid\"><video src=\"" + Ui.Attr(m.Full) + "\" poster=\"" + Ui.Attr(m.Thumb) + "\" controls preload=\"none\" playsinline width=\"1280\" height=\"720\" aria-label=\"" + Ui.Attr(m.Title) + "\"></video><figcaption><b>" + Ui.Enc(m.Title) + "</b>" + (m.Seconds > 0 ? "<small>" + Ui.Secs(m.Seconds) + "</small>" : string.Empty) + "</figcaption></figure>");
                    }
                    sb.Append("</div>");
                    return Sec(b, sb.ToString());
                case "albums":
                    List<Album> albums = site.Albums.Where(a => a.Published && a.Items.Count > 0).OrderByDescending(a => a.Date).ToList();
                    if (albums.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append((head != string.Empty ? "<div class=\"md-bhead\">" + head + "</div>" : string.Empty) + "<div class=\"md-agrid\">");
                    foreach (Album a in albums)
                    {
                        var items = a.Items.Select(src => new { src, cap = site.Media.FirstOrDefault(m => m.Full == src)?.Title ?? string.Empty });
                        string cover = a.Cover != string.Empty ? a.Cover : a.Items[0];
                        sb.Append("<button type=\"button\" class=\"md-alb\" data-items=\"" + Ui.Attr(JsonSerializer.Serialize(items)) + "\" data-title=\"" + Ui.Attr(a.Title) + "\" onclick=\"MediaJs.lbOpen(this)\"><span class=\"md-alb-img\">" + Ui.Pic(cover, string.Empty, string.Empty, true) + "<em>" + Ui.Plural(a.Items.Count, "photo", "photos") + "</em></span><span class=\"md-alb-t\"><b>" + Ui.Enc(a.Title) + "</b><small>" + Ui.Enc(a.Text) + "</small></span></button>");
                    }
                    sb.Append("</div>");
                    return Sec(b, sb.ToString());
                case "docs":
                    List<MediaItem> docs = Tagged(site, "doc");
                    if (docs.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append((head != string.Empty ? "<div class=\"md-bhead\">" + head + "</div>" : string.Empty) + "<div class=\"md-dgrid\">");
                    foreach (MediaItem m in docs)
                    {
                        string file = Path.GetFileName(m.Full);
                        sb.Append("<div class=\"md-doc\"><a class=\"md-doc-img\" href=\"" + Ui.Attr(m.Full) + "\" target=\"_blank\" rel=\"noopener\" tabindex=\"-1\" aria-hidden=\"true\">" + (m.Thumb != string.Empty ? "<img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"828\">" : "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 3h7l4 4v14H7z\"/><path d=\"M14 3v4h4M10 12h5M10 16h5\"/></svg>") + "</a><div class=\"md-doc-b\"><b>" + Ui.Enc(m.Title) + "</b><small>PDF" + (m.Pages > 0 ? " &middot; " + Ui.Plural(m.Pages, "page", "pages") : string.Empty) + (m.Bytes > 0 ? " &middot; " + Ui.Size(m.Bytes) : string.Empty) + "</small><span><a class=\"md-btn md-btn-sm\" href=\"" + Ui.Attr(m.Full) + "\" target=\"_blank\" rel=\"noopener\">Read</a><a class=\"md-btn md-btn-sm md-btn-ghost\" href=\"" + Ui.Attr(m.Full) + "\" download=\"" + Ui.Attr(file) + "\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 4v11M7.5 10.5L12 15l4.5-4.5M5 19h14\"/></svg> Download</a></span></div></div>");
                    }
                    sb.Append("</div>");
                    return Sec(b, sb.ToString());
            }
            return string.Empty;
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"md-btn " + (main ? "md-btn-accent" : "md-btn-ghost") + "\" href=\"" + Ui.Attr(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Enc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"md-hero-b\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<span class=\"md-kick\">" + Ui.Enc(Tok(site, b.Kicker)) + "</span>";
        }

        private static string H2(SiteData site, Block b)
        {
            return b.Title == string.Empty ? string.Empty : "<h2 class=\"md-h2\">" + Ui.Enc(Tok(site, b.Title)) + "</h2>";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"md-rich " + cls + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead)
        {
            string x = Kick(site, b) + H2(site, b) + (lead ? Rich(site, b.Text, "md-lead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"md-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"md-sec md-wrap\" id=\"" + Ui.Attr(b.Id) + "\">" + inner + "</section>";
        }

        private static string Finder(SiteData site)
        {
            return "<form class=\"md-find\" action=\"Classes\" method=\"get\"><label class=\"md-field\"><span>Sport</span><select name=\"sport\"><option value=\"\">Any sport</option>" + Ui.Opts(site.Sports.Where(x => x.Published).OrderBy(x => x.Order).Select(x => (x.Id, x.Name)), string.Empty) + "</select></label><label class=\"md-field\"><span>Who is it for?</span><select name=\"age\"><option value=\"\">Anyone</option>" + Ui.Opts(new[] { ("kids", "Kids, 4 to 12"), ("teens", "Teens, 13 to 17"), ("adults", "Adults, 18+") }, string.Empty) + "</select></label><label class=\"md-field\"><span>Day</span><select name=\"day\"><option value=\"\">Any day</option>" + Ui.Opts(new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(d => (d.ToString(Ui.Inv), Ui.DayNames[d])), string.Empty) + "</select></label><button type=\"submit\" class=\"md-btn md-btn-accent\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"M16 16l4.5 4.5\"/></svg> Find a class</button></form>";
        }

        private static string Stats(SiteData site, DateTime today)
        {
            DateTime now = Store.Now(site);
            int open = site.Classes.Count(k => Rules.State(site, k, now) is "open" or "few" or "full");
            return "<div class=\"md-hstats\"><a href=\"Sports\"><b>" + site.Sports.Count(x => x.Published) + "</b><span>sports under one roof</span></a><a href=\"Classes\"><b>" + open + "</b><span>classes open now</span></a><a href=\"Instructors\"><b>" + site.Instructors.Count(x => x.Published) + "</b><span>certified coaches</span></a></div>";
        }

        private static string Hero(SiteData site, Block b, DateTime today, bool h1, string title, string text)
        {
            bool find = b.Extra == "finder";
            string tag = h1 ? "h1" : "h2";
            string media = Mv(site, b.Media, Tok(site, b.Title), Ui.IsVideo(b.Media), h1);
            string t = title != string.Empty ? Ui.Enc(title) : Ui.Enc(Tok(site, b.Title));
            string lead = text != string.Empty ? "<p class=\"md-lead\">" + Ui.Enc(text) + "</p>" : Rich(site, b.Text, "md-lead");
            return "<section class=\"md-bhero" + (find ? " md-bhome" : string.Empty) + (media == string.Empty ? " md-bplain" : string.Empty) + "\" id=\"" + Ui.Attr(b.Id) + "\"><div class=\"md-wrap md-bhero-in\"><div class=\"md-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"md-btitle\">" + t + "</" + tag + ">" + lead + Btns(site, b) + (find ? Finder(site) : string.Empty) + "</div>" + (media != string.Empty ? "<div class=\"md-bhero-p\">" + media + (find ? Stats(site, today) : string.Empty) + "</div>" : string.Empty) + "</div></section>";
        }

        private static string Split(SiteData site, Block b, DateTime today)
        {
            string counts = b.Extra == "counts" ? "<div class=\"md-nums\"><span><b>" + site.Sports.Count(x => x.Published) + "</b>sports</span><span><b>" + site.Classes.Count(k => k.Published && k.Status == "active" && Rules.End(k) > Store.Now(site)) + "</b>classes this season</span><span><b>" + site.Instructors.Count(x => x.Published) + "</b>instructors</span></div>" : string.Empty;
            string btn = Btn(site, b.Label, b.Link, false);
            return Sec(b, "<div class=\"md-bsplit" + (b.Flip ? " md-flip" : string.Empty) + (b.Media == string.Empty ? " md-nomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"md-bsm\">" + Mv(site, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "md-lead") + counts + (btn != string.Empty ? "<div class=\"md-hero-b\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"md-bcards md-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"md-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Enc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"md-bcard\" href=\"" + Ui.Attr(it.Link) + "\">" + inner + "<span class=\"md-more\">" + Ui.Enc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"md-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"md-bsteps md-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"md-bstats md-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Enc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"md-bpeople md-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"md-person\">" + (it.Media != string.Empty && !Ui.IsVideo(it.Media) ? Mv(site, it.Media, it.Title, false, false) : "<span class=\"md-noface\" style=\"--h:" + Ui.Hue(it.Title) + "\">" + Ui.Enc(Ui.Initials(it.Title)) + "</span>") + "<b>" + Ui.Enc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"md-bboxes md-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"md-box\">" + (it.Title != string.Empty ? "<h2>" + Ui.Enc(Tok(site, it.Title)) + "</h2>" : string.Empty) + "<ul class=\"md-bticks\">");
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
            StringBuilder sb = new StringBuilder("<div class=\"md-bfaq\">" + Head(site, b, false) + "<div class=\"md-faq\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details><summary>" + Ui.Enc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, Block b)
        {
            return Sec(b, "<figure class=\"md-bquote\">" + (b.Media != string.Empty && !Ui.IsVideo(b.Media) ? Mv(site, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Enc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"md-bmedia\">" + Mv(site, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Enc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"md-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "md-lead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"md-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
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
