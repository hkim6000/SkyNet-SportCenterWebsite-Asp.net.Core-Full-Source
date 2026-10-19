using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class MyRegistration : WebPage
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
                .Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--mr-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_nav}", Nav("mr", v.Nav, false))
                .Replace("{plhd_drawer}", Nav("mr", v.Nav, true))
                .Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("MyRegistrationJs.hydrated(false);");
                return response;
            }
            qd = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> kv in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Ui.Clip(GetDataValue("qstr"), 600)))
            {
                qd[kv.Key] = kv.Value.ToString();
            }
            SiteData site = await Store.LoadSite(sbx);
            View v = await Render(site);
            response.SetElementContents("mr-main", v.Main);
            response.SetElementContents("mr-bname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mr-btown", Ui.Enc(site.Settings.Town));
            response.SetElementContents("mr-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mr-copyname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("mr-ftag", Ui.Enc(site.Settings.Tagline));
            string accent = Ui.SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("MyRegistrationJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "'," + Store.MinutesLeft(sbx) + ");");
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
                response.ExecuteScript("MyRegistrationJs.sandboxed('" + sbx + "');");
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
                response.ExecuteScript("MyRegistrationJs.who('', '', '');");
                return response;
            }
            string tier = Rules.TierOf(site.Settings, me.Lifetime);
            response.ExecuteScript("MyRegistrationJs.who('" + Ui.Js(me.First) + "','" + tier + "','" + Rules.TierName(tier) + "');");
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
                return "<i class=\"mr-shut\"></i>Opens today at " + Ui.HmText(site, d!.Start);
            }
            for (int i = 1; i <= 7; i++)
            {
                DateTime n = now.Date.AddDays(i);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == (int)n.DayOfWeek);
                if (o != null && o.Open)
                {
                    return "<i class=\"mr-shut\"></i>Closed now &middot; opens " + (i == 1 ? "tomorrow" : Ui.DayNames[(int)n.DayOfWeek]) + " at " + Ui.HmText(site, o.Start);
                }
            }
            return "<i class=\"mr-shut\"></i>Closed";
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
            return "<article class=\"mr-kc\"><a class=\"mr-kc-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Ui.Pic(Ui.ClassPic(site, k), string.Empty, string.Empty, true) + "<span class=\"mr-kc-tag\">" + Ui.Enc(Ui.SportName(site, k.Sport)) + "</span><span class=\"mr-kc-st\">" + Ui.StatePill("mr", st) + "</span></a><div class=\"mr-kc-b\"><span class=\"mr-kc-t\"><a href=\"" + href + "\">" + Ui.Enc(k.Title) + "</a></span><span class=\"mr-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg> " + Ui.ClassWhen(site, k) + "</span><span class=\"mr-kc-w\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\" stroke-linecap=\"round\"/></svg> " + Ui.Range(k) + "</span><span class=\"mr-meta\"><span class=\"mr-chipx\">" + Ui.Enc(Ui.NameOf(Rules.Levels, k.Level)) + "</span><span class=\"mr-chipx\">" + Rules.AgeRule(k) + "</span><span class=\"mr-chipx\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span></span><span class=\"mr-kc-f\"><b>" + Ui.Money(k.Price) + "</b><small>" + Ui.Plural(k.Dates.Count, "session", "sessions") + " &middot; " + seats + "</small></span></div></article>";
        }

        private static int OpenCount(SiteData site, string sport, DateTime now)
        {
            return site.Classes.Count(k => k.Sport == sport && Rules.State(site, k, now) is "open" or "few" or "full");
        }

        private async Task<View> Render(SiteData site)
        {
            string code = Rules.CleanCode(Q("code", 12));
            string main = "<section class=\"mr-wrap mr-phead\"><span class=\"mr-kick\">Look up</span><h1>Find my registration</h1><p class=\"mr-lead\">Enter the code from your receipt and the email on the account. It works for class registrations, waitlist places and day passes.</p></section><section class=\"mr-wrap mr-lgrid\"><form class=\"mr-box mr-form mr-lform\" onsubmit=\"return MyRegistrationJs.find()\" novalidate><h2>Your details</h2><label class=\"mr-field\"><span>Code</span><input type=\"text\" id=\"mr-lcode\" maxlength=\"8\" autocapitalize=\"characters\" autocomplete=\"off\" placeholder=\"For example K7P3QX\" value=\"" + Ui.Attr(code) + "\"></label><em class=\"mr-err\" id=\"mr-e-lcode\" role=\"alert\"></em><label class=\"mr-field\"><span>Email</span><input type=\"email\" id=\"mr-lmail\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"mr-err\" id=\"mr-e-lmail\" role=\"alert\"></em><button type=\"submit\" class=\"mr-btn mr-btn-accent mr-btn-wide\" id=\"mr-lgo\">Find it</button><p class=\"mr-hint\">Members can see everything in the <a class=\"mr-more\" href=\"Member\">member area</a>.</p>" + (Store.Demo ? "<div class=\"mr-demo-card\"><b>Try it:</b> code <code>" + Ui.Enc(DemoCode(site)) + "</code> with <code>olivia.grant@example.com</code></div>" : string.Empty) + "</form><div id=\"mr-result\" class=\"mr-result\"><div class=\"mr-rempty\">Your registration will show here.</div></div></section>";
            return new View { Title = "Find my registration", Desc = "Look up a class registration or day pass at " + site.Settings.Name + ".", Main = main, Nav = "lookup" };
        }

        private static string DemoCode(SiteData site)
        {
            Registration? r = site.Registrations.Where(x => x.Member == "M-1001" && x.Status == "confirmed").OrderByDescending(x => x.Created).FirstOrDefault();
            return r?.Code ?? string.Empty;
        }

        private static string Result(SiteData site, Registration r)
        {
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            (bool cancel, bool ask, string text, decimal amt) = Rules.Options(site, r, DateTime.UtcNow);
            StringBuilder sb = new StringBuilder("<div class=\"mr-box mr-rbox\"><div class=\"mr-rtop\"><div><span class=\"mr-kick\">" + Ui.Enc(k != null ? Ui.SportName(site, k.Sport) : string.Empty) + "</span><h2>" + Ui.Enc(k?.Title ?? r.Class) + "</h2><p>" + Ui.Enc(r.Name) + (k != null ? " &middot; " + Ui.ClassWhen(site, k) : string.Empty) + "</p></div>" + Ui.Pill("mr", Rules.RegStatuses, r.Status) + "</div>");
            if (r.Status == "offered" && r.OfferExp != null)
            {
                sb.Append("<div class=\"mr-ok-box\"><b>A seat is yours.</b>Pay by " + Ui.When(site, Store.Local(site, r.OfferExp.Value)) + " in the member area to keep it.</div><a class=\"mr-btn mr-btn-accent\" href=\"SignIn?next=" + Uri.EscapeDataString("Member?tab=classes") + "\">Sign in to pay</a>");
            }
            if (r.Status == "pending" && r.HoldExp != null)
            {
                sb.Append("<div class=\"mr-warn-box\"><b>Pay at the front desk.</b>" + Ui.Money(r.Total, true) + " by " + Ui.When(site, Store.Local(site, r.HoldExp.Value)) + ", or pay online in the member area.</div>");
            }
            if (r.Ask != null)
            {
                sb.Append("<div class=\"" + (r.Ask.Status == "approved" ? "mr-ok-box" : r.Ask.Status == "declined" ? "mr-bad-box" : "mr-info-box") + "\"><b>Refund request " + (r.Ask.Status == "open" ? "sent" : r.Ask.Status) + "</b><br>" + Ui.Enc(r.Ask.Reason) + (r.Ask.Note != string.Empty ? "<br><i>Front desk: " + Ui.Enc(r.Ask.Note) + "</i>" : string.Empty) + "</div>");
            }
            if (r.Status == "waitlist")
            {
                int pos = site.Registrations.Count(x => x.Class == r.Class && x.Status == "waitlist" && x.Created <= r.Created);
                sb.Append("<div class=\"mr-info-box\"><b>Number " + pos + " on the waitlist.</b>If a seat opens you'll get an offer in the member area.</div>");
            }
            if (r.Status is "confirmed" or "pending" or "cancelled" or "expired")
            {
                sb.Append(ReceiptBox("mr", Rules.Receipt(site, r, "mr"), r.Code));
            }
            if (text != string.Empty)
            {
                sb.Append("<p class=\"mr-hint\">" + Ui.Enc(text) + "</p>");
            }
            if (cancel)
            {
                string label = r.Status switch { "waitlist" => "Leave the waitlist", "offered" => "Decline the seat", _ => "Cancel registration" };
                sb.Append("<button type=\"button\" class=\"mr-btn mr-btn-danger\" onclick=\"MyRegistrationJs.cancelReg('" + Ui.Js(label) + "','" + Ui.Js(amt > 0 ? Ui.Plain(amt) + " will be refunded." : "No money changes hands.") + "')\">" + label + "</button>");
            }
            if (ask)
            {
                sb.Append("<form class=\"mr-askf\" onsubmit=\"return MyRegistrationJs.askRefund()\" novalidate><label class=\"mr-field\"><span>Ask the front desk for a refund</span><textarea id=\"mr-areason\" rows=\"3\" maxlength=\"600\" placeholder=\"What happened?\"></textarea></label><em class=\"mr-err\" id=\"mr-e-areason\" role=\"alert\"></em><button type=\"submit\" class=\"mr-btn mr-btn-ghost\">Send request</button></form>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string PassResult(SiteData site, DayPass d)
        {
            return "<div class=\"mr-box mr-rbox\"><div class=\"mr-rtop\"><div><span class=\"mr-kick\">" + Ui.Enc(site.Settings.PassName) + "</span><h2>" + Ui.LongDay(d.Day) + "</h2><p>Show the code at the front desk.</p></div><span class=\"mr-st mr-st-" + d.Status + "\">" + (d.Status == "used" ? "Used" : d.Status == "refunded" ? "Refunded" : "Valid") + "</span></div>" + ReceiptBox("mr", Rules.PassReceipt(site, d, "mr"), d.Code) + "</div>";
        }

        private (string Code, string Mail) Keys()
        {
            return (Rules.CleanCode(GetDataValue("lcode")), Ui.Clip(GetDataValue("lmail"), 120).Trim());
        }

        private static (Registration? R, DayPass? P) Match(SiteData site, string code, string mail)
        {
            Registration? r = site.Registrations.FirstOrDefault(x => x.Code == code);
            if (r != null)
            {
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
                return (m != null && m.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) ? r : null, null);
            }
            DayPass? d = site.Passes.FirstOrDefault(x => x.Code == code);
            if (d != null)
            {
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == d.Member);
                return (null, m != null && m.Email.Equals(mail, StringComparison.OrdinalIgnoreCase) ? d : null);
            }
            return (null, null);
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            (string code, string mail) = Keys();
            if (code.Length != 6)
            {
                response.ExecuteScript("MyRegistrationJs.fieldErr('lcode','Enter the 6-character code from your receipt.');MyRegistrationJs.found();");
                return response;
            }
            if (!Ui.Email(mail))
            {
                response.ExecuteScript("MyRegistrationJs.fieldErr('lmail','Enter the email on the account.');MyRegistrationJs.found();");
                return response;
            }
            if (Auth.Locked("lookup:" + code))
            {
                response.ExecuteScript("MyRegistrationJs.fieldErr('lcode','Too many tries. Please wait a few minutes.');MyRegistrationJs.found();");
                return response;
            }
            (SiteData site, string sbx, global::Sport.Models.ClubMember? me) = await Context();
            (Registration? r, DayPass? d) = Match(site, code, mail);
            if (r == null && d == null)
            {
                Auth.Fail("lookup:" + code);
                response.SetElementContents("mr-result", "<div class=\"mr-bad-box\"><b>Nothing found.</b>Check the code and use the email address on the account.</div>");
                response.ExecuteScript("MyRegistrationJs.found();");
                return response;
            }
            response.SetElementContents("mr-result", r != null ? Result(site, r) : PassResult(site, d!));
            response.ExecuteScript("MyRegistrationJs.found();");
            return response;
        }

        private async Task<string> WriteBox(ApiResponse response)
        {
            MCtx? mc = await Auth.Member(GetDataValue("mtok"));
            return mc != null ? mc.Sbx : await Writable(response, GetDataValue("sbx"));
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            (string code, string mail) = Keys();
            string sbx = await WriteBox(response);
            string msg = string.Empty;
            bool ok = false;
            SiteData? saved = null;
            Registration? reg = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                (Registration? r, DayPass? d) = Match(site, code, mail);
                if (r != null)
                {
                    (ok, msg) = Rules.CancelOnline(site, r, DateTime.UtcNow);
                    if (ok)
                    {
                        await Store.SaveSite(site, sbx);
                    }
                    saved = site;
                    reg = r;
                }
            }
            if (reg == null || saved == null)
            {
                return response;
            }
            if (ok)
            {
                await Store.Log(sbx, "web", "cancel", reg.Class + " · " + reg.Code + " · " + msg, true);
            }
            response.SetElementContents("mr-result", Result(saved, reg));
            response.ExecuteScript("MyRegistrationJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> Ask()
        {
            ApiResponse response = new ApiResponse();
            (string code, string mail) = Keys();
            string reason = Ui.Clip(GetDataValue("areason"), 600);
            if (reason.Length < 5)
            {
                response.ExecuteScript("MyRegistrationJs.fieldErr('areason','Tell us briefly why you need a refund.');");
                return response;
            }
            string sbx = await WriteBox(response);
            string msg = string.Empty;
            bool ok = false;
            SiteData? saved = null;
            Registration? reg = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                (Registration? r, DayPass? d) = Match(site, code, mail);
                if (r != null)
                {
                    (ok, msg) = Rules.AskRefund(site, r, reason, DateTime.UtcNow);
                    if (ok)
                    {
                        await Store.SaveSite(site, sbx);
                    }
                    saved = site;
                    reg = r;
                }
            }
            if (reg == null || saved == null)
            {
                return response;
            }
            if (ok)
            {
                await Store.Log(sbx, "web", "refund request", reg.Class + " · " + reg.Code, true);
            }
            response.SetElementContents("mr-result", Result(saved, reg));
            response.ExecuteScript("MyRegistrationJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }

        private static string ReceiptBox(string p, string inner, string code)
        {
            return inner + "<div class=\"" + p + "-ract\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"MyRegistrationJs.printBox('" + p + "-rcpt-" + Ui.Attr(code) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 9V3h10v6M7 17H4v-7h16v7h-3\"/><path d=\"M7 14h10v7H7z\"/></svg> Print receipt</button></div>";
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
