using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class Admin : WebPage
    {
        private const string AreaKey = "";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Dashboard");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--db-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-menu", Auth.Menu("db", "Admin", c, site));
            response.SetElementContents("db-who", Auth.Who("db", c));
            response.SetElementContents("db-demo", Auth.DemoPill("db", c));
            response.SetElementContents("db-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"db-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"db-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("db-main", NoAccess("Dashboard"));
                return response;
            }
            response.SetElementContents("db-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminJs.loaded();");
            return response;
        }

        private static string Per(string v)
        {
            return v is "today" or "7" or "30" or "range" ? v : "all";
        }

        private static string Who(Dictionary<string, Staff> staff, string id)
        {
            return id == "web" ? "Website" : id == "system" ? "System" : staff.TryGetValue(id, out Staff? s) ? s.Name : id;
        }

        private static string Head((string Name, string Cls)[] cols)
        {
            StringBuilder sb = new StringBuilder("<div class=\"db-tr db-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Kpi(string label, string value, string sub, string href, bool hot = false)
        {
            string inner = "<span>" + label + "</span><b>" + value + "</b><small>" + sub + "</small>";
            return href != string.Empty ? "<a class=\"db-kpi" + (hot ? " db-hot" : string.Empty) + "\" href=\"" + href + "\">" + inner + "</a>" : "<div class=\"db-kpi\">" + inner + "</div>";
        }

        private static string Todo(int n, string text, string href)
        {
            return n > 0 ? "<a href=\"" + href + "\"><span>" + text + "</span><b>" + n + "</b></a>" : string.Empty;
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            DateTime now = Store.Now(site);
            DateTime today = now.Date;
            DateTime nowU = DateTime.UtcNow;
            bool own = Access.OwnOnly(c.U);
            bool canB = Access.Can(c.U, "board", 'V');
            bool canK = Access.Can(c.U, "classes", 'V');
            bool canR = Access.Can(c.U, "registrations", 'V');
            bool canP = Access.Can(c.U, "payments", 'V');
            bool canM = Access.Can(c.U, "members", 'V');
            bool canMsg = Access.Can(c.U, "messages", 'V');
            List<SportClass> mine = site.Classes.Where(k => Access.Mine(c.U, k)).ToList();
            HashSet<string> mineIds = mine.Select(k => k.Id).ToHashSet();
            List<(SportClass K, DateTime At, int N)> sessions = mine.Where(k => k.Status != "cancelled").SelectMany(k => k.Dates.Select((d, i) => (K: k, At: d, N: i))).Where(x => x.At.Date == today).OrderBy(x => x.At).ToList();
            string hello = now.Hour < 12 ? "Good morning" : now.Hour < 18 ? "Good afternoon" : "Good evening";
            StringBuilder sb = new StringBuilder("<div class=\"db-ptop\"><div><h1>" + hello + ", " + Ui.Enc(c.U.Name.Split(' ')[0]) + "</h1><p>" + Ui.LongDay(today) + (own ? " &middot; your classes" : string.Empty) + "</p></div><div class=\"db-acts\">" + (Access.Can(c.U, "registrations", 'E') ? "<a class=\"db-btn db-btn-accent\" href=\"AdminRegistrations?add=1\">Register someone</a>" : string.Empty) + (Access.Can(c.U, "classes", 'E') ? "<a class=\"db-btn db-btn-ghost\" href=\"AdminClasses?id=new\">New class</a>" : string.Empty) + "<a class=\"db-btn db-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">View site</a></div></div><div class=\"db-kpis\">");
            if (canB)
            {
                int left = sessions.Count(x => x.At > now);
                sb.Append(Kpi("Sessions today", sessions.Count.ToString(CultureInfo.InvariantCulture), left + " still to start", "AdminBoard"));
                int inClass = site.Visits.Count(v => v.Kind == "class" && v.Out == null && v.In > nowU.AddHours(-6) && site.Registrations.Any(r => r.Id == v.Reg && mineIds.Contains(r.Class)));
                int inPass = own ? 0 : site.Visits.Count(v => v.Kind == "pass" && v.Out == null && v.In > nowU.AddHours(-18));
                sb.Append(Kpi("In the building", (inClass + inPass).ToString(CultureInfo.InvariantCulture), inClass + " in class" + (own ? string.Empty : " &middot; " + inPass + " day pass"), canM && !own ? "AdminMembers" : "AdminBoard"));
            }
            if (canP)
            {
                DateTime m0 = new DateTime(today.Year, today.Month, 1);
                decimal inM = site.Payments.Where(p => p.Kind == "charge" && Store.Local(site, p.When) >= m0).Sum(p => p.Amount);
                decimal outM = site.Payments.Where(p => p.Kind == "refund" && Store.Local(site, p.When) >= m0).Sum(p => p.Amount);
                sb.Append(Kpi("Revenue &middot; " + today.ToString("MMM", CultureInfo.InvariantCulture), Ui.Whole(inM - outM), Ui.Whole(outM) + " refunded", "AdminPayments"));
            }
            if (canM)
            {
                DateTime d30 = nowU.AddDays(-30);
                int joined = site.Members.Count(x => x.Type == "member" && x.Joined != null && x.Joined.Value >= d30);
                sb.Append(Kpi("New members", joined.ToString(CultureInfo.InvariantCulture), "last 30 days &middot; " + site.Members.Count(x => x.Type == "member") + " total", "AdminMembers?type=member"));
            }
            if (canR || canB)
            {
                int wait = site.Registrations.Count(r => r.Status == "waitlist" && mineIds.Contains(r.Class));
                int off = site.Registrations.Count(r => r.Status == "offered" && mineIds.Contains(r.Class));
                sb.Append(Kpi("Waitlist", wait.ToString(CultureInfo.InvariantCulture), off + " seat offer" + (off == 1 ? string.Empty : "s") + " out", canR ? "AdminRegistrations" : "AdminBoard"));
            }
            if (canP)
            {
                int asks = site.Registrations.Count(r => r.Ask != null && r.Ask.Status == "open");
                sb.Append(Kpi("Refund requests", asks.ToString(CultureInfo.InvariantCulture), asks > 0 ? "waiting for a decision" : "all decided", "AdminPayments?tab=asks", asks > 0));
            }
            bool ahead = sessions.Count == 0;
            List<(SportClass K, DateTime At, int N)> show = ahead ? mine.Where(k => k.Status != "cancelled" && k.Published).SelectMany(k => k.Dates.Select((d, i) => (K: k, At: d, N: i))).Where(x => x.At > now).OrderBy(x => x.At).Take(6).ToList() : sessions;
            sb.Append("</div><div class=\"db-grid2\" style=\"margin-top:0\"><section class=\"db-panel\"><div class=\"db-ph\"><h2>" + (ahead ? "Coming up" : "Today&rsquo;s sessions") + "</h2>" + (canB ? "<a href=\"AdminBoard\">Class board</a>" : string.Empty) + "</div>" + (ahead ? "<p class=\"db-hint\" style=\"margin:-4px 0 6px\">No sessions today. The next ones:</p>" : string.Empty));
            if (show.Count == 0)
            {
                sb.Append("<p class=\"db-none\">Nothing scheduled.</p>");
            }
            foreach ((SportClass k, DateTime at, int n) in show)
            {
                DateTime end = at.AddMinutes(k.Minutes);
                bool past = now >= end;
                bool live = now >= at && now < end;
                int booked = site.Registrations.Count(r => r.Class == k.Id && r.Status == "confirmed");
                int here = site.Visits.Count(v => v.Kind == "class" && v.Session == n && site.Registrations.Any(r => r.Id == v.Reg && r.Class == k.Id));
                string row = "<span class=\"db-tm\">" + (ahead ? "<small style=\"font-family:var(--db-text);font-size:11.5px;font-weight:800;color:var(--db-ink2);text-transform:uppercase\">" + at.ToString("ddd d", CultureInfo.InvariantCulture) + "</small>" : string.Empty) + Ui.Hm(site, at) + "</span>" + (Ui.ClassPic(site, k) != string.Empty ? "<img src=\"" + Ui.Attr(Ui.Thumb(Ui.ClassPic(site, k))) + "\" alt=\"\" loading=\"lazy\">" : string.Empty) + "<div><b>" + Ui.Enc(k.Title) + "</b><small" + (live ? " class=\"db-now\"" : string.Empty) + ">" + (live ? "In progress &middot; " : string.Empty) + Ui.Enc(Ui.CoachName(site, k.Instructor)) + " &middot; " + Ui.Enc(k.Place) + " &middot; session " + (n + 1) + " of " + k.Dates.Count + "</small></div><span class=\"db-cap\">" + (past || live ? here + " / " + booked + " in" : booked + " / " + k.Capacity) + "</span>";
                sb.Append(canB ? "<a class=\"db-ses" + (past ? " db-past" : string.Empty) + "\" href=\"AdminBoard?id=" + Uri.EscapeDataString(k.Id) + "\">" + row + "</a>" : "<div class=\"db-ses" + (past ? " db-past" : string.Empty) + "\">" + row + "</div>");
            }
            sb.Append("</section><section class=\"db-panel\"><h2>Needs attention</h2><div class=\"db-todo\">");
            StringBuilder todo = new StringBuilder();
            if (canP)
            {
                todo.Append(Todo(site.Registrations.Count(r => r.Ask != null && r.Ask.Status == "open"), "Refund requests to decide", "AdminPayments?tab=asks"));
            }
            if (canR)
            {
                todo.Append(Todo(site.Registrations.Count(r => r.Status == "pending" && mineIds.Contains(r.Class)), "Pay-at-desk seats on hold", "AdminRegistrations?status=pending"));
                todo.Append(Todo(site.Registrations.Count(r => r.Status == "offered" && mineIds.Contains(r.Class)), "Waitlist offers waiting for payment", "AdminRegistrations?status=offered"));
            }
            if (canK)
            {
                DateTime week = today.AddDays(8);
                todo.Append(Todo(mine.Count(k => k.Status != "cancelled" && k.Published && k.Dates.Count > 0 && k.Dates[0] > now && k.Dates[0] < week && Rules.Taken(site, k) * 3 < k.Capacity), "Classes starting this week under a third full", "AdminClasses"));
                todo.Append(Todo(mine.Count(k => !k.Published && k.Status != "cancelled" && k.Dates.Count > 0 && k.Dates[^1] > now), "Draft classes not on the website", "AdminClasses"));
            }
            if (canB)
            {
                todo.Append(Todo(mine.Count(k => k.Status != "cancelled" && Rules.State(site, k, now) is "full" && Rules.Waiting(site, k) > 0), "Full classes with a waitlist", canR ? "AdminRegistrations?status=waitlist" : "AdminBoard"));
            }
            if (canMsg)
            {
                todo.Append(Todo(site.Messages.Count(x => x.Status == "new"), "New messages", "AdminMessages"));
            }
            if (canM && !own)
            {
                todo.Append(Todo(site.Members.Count(x => x.Status == "suspended"), "Suspended accounts", "AdminMembers"));
            }
            sb.Append(todo.Length > 0 ? todo.ToString() : "<p class=\"db-none\">Nothing waiting. Nice.</p>");
            sb.Append("</div>");
            if (canP)
            {
                sb.Append("<h2 style=\"margin-top:20px\">Latest payments</h2>");
                Dictionary<string, global::Sport.Models.ClubMember> mem = site.Members.ToDictionary(x => x.Id);
                foreach (Payment p in site.Payments.OrderByDescending(p => p.When).Take(5))
                {
                    string who = mem.TryGetValue(p.Member, out global::Sport.Models.ClubMember? x) ? Rules.FullName(x) : p.Member;
                    string what = p.Note != string.Empty ? p.Note : site.Classes.FirstOrDefault(k => k.Id == site.Registrations.FirstOrDefault(r => r.Id == p.Reg)?.Class)?.Title ?? (p.Kind == "refund" ? "Refund" : "Payment");
                    sb.Append("<div class=\"db-ses\"><div><b>" + Ui.Enc(who) + "</b><small>" + Ui.Enc(what) + " &middot; " + Ui.Time(p.When) + "</small></div><span class=\"db-amt" + (p.Kind == "refund" ? "\" style=\"color:var(--db-err)\">&minus;" : "\">") + Ui.Money(p.Amount) + "</span></div>");
                }
            }
            sb.Append("</section></div>");
            return sb.ToString();
        }
    }
}
