using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class SignIn : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--si-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("si", v.Nav, false))
                .Replace("{plhd_drawer}", Nav("si", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("SignInJs.hydrated(false);");
                return response;
            }
            qd = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Ui.Clip(GetDataValue("qstr"), 600)))
            {
                qd[kv.Key] = kv.Value.ToString();
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = await Render(site);
            response.SetElementContents("si-main", v.Main);
            response.SetElementContents("si-bname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("si-btown", Ui.Enc(site.Settings.Town));
            response.SetElementContents("si-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("si-copyname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("si-ftag", Ui.Enc(site.Settings.Tagline));
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("SignInJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "'," + Store.MinutesLeft(sbx) + ");");
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
                response.ExecuteScript("SignInJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("SignInJs.who('', '', '');");
                return response;
            }
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            response.ExecuteScript("SignInJs.who('" + Ui.Js(me.First) + "','" + tier + "','" + Rules.TierName(tier) + "');");
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
                return "<i class=\"si-shut\"></i>Opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"si-shut\"></i>Closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"si-shut\"></i>Closed";
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
            return "<article class=\"si-kc\"><a class=\"si-kc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) + "<span class=\"si-kc-tag\">" + Ui.Enc(Ui.SportName(site, k.Sport)) + "</span><span class=\"si-kc-st\">" + Ui.StatePill("si", st) + "</span></a><div class=\"si-kc-b\"><span class=\"si-kc-t\"><a href=\"" + href + "\">" + Ui.Enc(k.Title) + "</a></span><span class=\"si-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> " + Ui.ClassWhen(site, k) + "</span><span class=\"si-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> " + Ui.Range(k) + "</span><span class=\"si-meta\"><span class=\"si-chipx\">" + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><span class=\"si-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"si-chipx\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span></span><span class=\"si-kc-f\"><b>" + Ui.Money(k.Price) + "</b><small>" + Ui.Plural(k.Dates.Count, "session", "sessions") + " &middot; " + seats + "</small></span></div></article>";
        }

        private static int OpenCount(SiteData site, string sport, DateTime now)
        {
            return site.Classes.Count(k => k.Sport == sport && Rules.State(site, k, now) is "open" or "few" or "full");
        }

        private async Task<View> Render(SiteData site)
        {
            string main = "<section class=\"si-wrap si-sgrid\"><div class=\"si-box si-scard\" id=\"si-sbox\"><span class=\"si-sico\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"5\" y=\"10.5\" width=\"14\" height=\"10\" rx=\"2\"/><path d=\"M8 10.5V8a4 4 0 0 1 8 0v2.5\"/></svg></span><h1>Member sign in</h1><p class=\"si-hint\">See your classes, receipts, family and points.</p><form class=\"si-form\" id=\"si-sf\" onsubmit=\"return SignInJs.signIn()\" novalidate><label class=\"si-field\"><span>Email</span><input type=\"email\" id=\"si-smail\" maxlength=\"120\" autocomplete=\"email\"" + (Store.Demo ? " value=\"olivia.grant@example.com\"" : string.Empty) + "></label><label class=\"si-field\"><span>Password</span><input type=\"password\" id=\"si-spass\" maxlength=\"100\" autocomplete=\"current-password\"" + (Store.Demo ? " value=\"demo\"" : string.Empty) + "></label><em class=\"si-err\" id=\"si-e-spass\" role=\"alert\"></em><button type=\"submit\" class=\"si-btn si-btn-accent si-btn-wide\" id=\"si-sgo\">Sign in</button><button type=\"button\" class=\"si-link\" onclick=\"SignInJs.forgot(true)\">Forgot your password?</button></form><form class=\"si-form\" id=\"si-ff\" onsubmit=\"return SignInJs.reset()\" novalidate hidden><h2>Reset your password</h2><p class=\"si-hint\" id=\"si-fhint\">Enter your email and we'll give you a reset code.</p><label class=\"si-field\"><span>Email</span><input type=\"email\" id=\"si-fmail\" maxlength=\"120\" autocomplete=\"email\"></label><div id=\"si-fstep2\" hidden><div class=\"si-codebox\" id=\"si-fcode\"></div><label class=\"si-field\"><span>Reset code</span><input type=\"text\" id=\"si-fcodein\" maxlength=\"6\" inputmode=\"numeric\" autocomplete=\"one-time-code\"></label><label class=\"si-field\"><span>New password <small>(8+ characters)</small></span><input type=\"password\" id=\"si-fpass\" maxlength=\"100\" autocomplete=\"new-password\"></label></div><em class=\"si-err\" id=\"si-e-fmail\" role=\"alert\"></em><button type=\"submit\" class=\"si-btn si-btn-accent si-btn-wide\" id=\"si-fgo\">Get a reset code</button><button type=\"button\" class=\"si-link\" onclick=\"SignInJs.forgot(false)\">&larr; Back to sign in</button></form>" + (Store.Demo ? "<div class=\"si-demo-card\"><b>Live demo.</b> Sample member: <code>olivia.grant@example.com</code> with the password <code>demo</code>. You get a private copy of the center for one hour.</div>" : string.Empty) + "</div><div class=\"si-sside\"><h2>New here?</h2><p>Membership is free. Sign up to register for classes, add your family and earn points from day one.</p><a class=\"si-btn\" href=\"Join\">Sign up free</a><p class=\"si-hint\">Only want to drop in? <a class=\"si-more\" href=\"Pass\">Buy a day pass</a> without an account.</p></div></section>";
            return new View { Title = "Sign in", Desc = "Member sign in for " + site.Settings.Name + ".", Main = main, Nav = "member" };
        }

        private static string Next(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^(Member(\\?tab=[a-z]+)?|Class\\?id=C-[0-9]{1,6})$") ? v! : "Member";
        }

        public async Task<ApiResponse> SignInMember()
        {
            ApiResponse response = new ApiResponse();
            string mail = Ui.Clip(GetDataValue("smail"), 120).Trim().ToLowerInvariant();
            string pass = Ui.Clip(GetDataValue("spass"), 200);
            if (mail == string.Empty || pass == string.Empty)
            {
                response.ExecuteScript("SignInJs.fail('Enter your email and password.');");
                return response;
            }
            if (Auth.Locked("m:" + mail))
            {
                response.ExecuteScript("SignInJs.fail('Too many attempts. Please wait a few minutes.');");
                return response;
            }
            string sbx = Store.Demo ? Store.Sandbox(GetDataValue("sbx")) : string.Empty;
            SiteData site = await Store.LoadSite(sbx);
            global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase));
            if (m != null && m.Type == "open")
            {
                response.ExecuteScript("SignInJs.fail('This email has day passes but no account yet. Sign up with the same email to keep your visits.');");
                return response;
            }
            if (m == null || m.Hash == string.Empty || !Access.Verify(pass, m.Hash) || (!Store.Demo && m.Sample))
            {
                Auth.Fail("m:" + mail);
                response.ExecuteScript("SignInJs.fail('Wrong email or password.');");
                return response;
            }
            if (m.Status == "suspended")
            {
                response.ExecuteScript("SignInJs.fail('Your account is suspended. Please talk to the front desk.');");
                return response;
            }
            if (Store.Demo && sbx == string.Empty)
            {
                sbx = await Writable(response, string.Empty);
            }
            DateTime exp = Auth.Expiry(sbx, 24 * 14);
            string tok = await Auth.Open(sbx, m.Id, exp, "member");
            await Store.Log(sbx, "web", "member sign in", Rules.FullName(m), true);
            response.ExecuteScript("SignInJs.signedIn('" + tok + "'," + Math.Max(60, (int)(exp - DateTime.UtcNow).TotalSeconds) + ",'" + Ui.Js(Next(Ui.Clip(GetDataValue("snext"), 60))) + "');");
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            string mail = Ui.Clip(GetDataValue("fmail"), 120).Trim();
            string code = Ui.Digits(GetDataValue("fcode"));
            string pass = GetDataValue("fpass");
            bool step2 = GetDataValue("fstep") == "2";
            if (!Ui.Email(mail))
            {
                response.SetElementContents("si-e-fmail", "Enter the email on your account.");
                response.ExecuteScript("SignInJs.resetDone();");
                return response;
            }
            if (Auth.Locked("r:" + mail.ToLowerInvariant()))
            {
                response.SetElementContents("si-e-fmail", "Too many attempts. Please wait a few minutes.");
                response.ExecuteScript("SignInJs.resetDone();");
                return response;
            }
            if (step2 && (pass.Length < 8 || pass.Length > 100))
            {
                response.SetElementContents("si-e-fmail", "Use a new password of 8 characters or more.");
                response.ExecuteScript("SignInJs.resetDone();");
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            string err = string.Empty;
            string shown = string.Empty;
            string id = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) && x.Type == "member" && (Store.Demo || !x.Sample));
                if (m == null)
                {
                    err = "No member account uses that email.";
                }
                else if (m.Status == "suspended")
                {
                    err = "This account is suspended. Please talk to the front desk.";
                }
                else if (!step2)
                {
                    shown = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString(Ui.Inv);
                    m.Reset = Access.HashPassword(shown);
                    m.ResetExp = DateTime.UtcNow.AddMinutes(15);
                    await Store.SaveSite(site, sbx);
                }
                else if (m.ResetExp == null || m.ResetExp < DateTime.UtcNow || code.Length != 6 || !Access.Verify(code, m.Reset))
                {
                    Auth.Fail("r:" + mail.ToLowerInvariant());
                    err = "That code is wrong or has expired. Ask for a new one.";
                }
                else
                {
                    m.Hash = Access.HashPassword(pass);
                    m.Reset = string.Empty;
                    m.ResetExp = null;
                    id = m.Id;
                    await Store.SaveSite(site, sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("si-e-fmail", Ui.Enc(err));
                response.ExecuteScript("SignInJs.resetDone();");
                return response;
            }
            if (!step2)
            {
                response.SetElementContents("si-fcode", "<span>Your reset code</span><b>" + shown + "</b><small>A real site would email this code. This template shows it on screen. It works for 15 minutes.</small>");
                response.ExecuteScript("SignInJs.codeShown();");
                return response;
            }
            await Auth.CloseUser(sbx, id, string.Empty, "member");
            DateTime exp = Auth.Expiry(sbx, 24 * 14);
            string tok = await Auth.Open(sbx, id, exp, "member");
            await Store.Log(sbx, "web", "password reset", mail, true);
            response.ExecuteScript("SignInJs.signedIn('" + tok + "'," + Math.Max(60, (int)(exp - DateTime.UtcNow).TotalSeconds) + ",'Member');");
            return response;
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
