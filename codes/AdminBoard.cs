using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminBoard : WebPage
    {
        private const string AreaKey = "board";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Class board");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ab-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ab-menu", Auth.Menu("ab", "AdminBoard", c, site));
            response.SetElementContents("ab-who", Auth.Who("ab", c));
            response.SetElementContents("ab-demo", Auth.DemoPill("ab", c));
            response.SetElementContents("ab-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminBoardJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ab-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ab-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminBoardJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBoardJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ab-main", NoAccess("Class board"));
                return response;
            }
            response.SetElementContents("ab-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminBoardJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ab-tr ab-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string title = Access.OwnOnly(c.U) ? "My classes" : "Class board";
            return "<div class=\"ab-ptop\"><div><h1>" + title + "</h1><p>Check people in and out of each session, see attendance, and move people up from the waitlist.</p></div><div class=\"ab-acts\"><div class=\"ab-bar\" style=\"margin:0\"><label>Show<select id=\"ab-bshow\" onchange=\"AdminBoardJs.list()\">" + Ui.Opts(new[] { ("now", "Running and upcoming"), ("today", "With a session today"), ("done", "Finished"), ("all", "All classes") }, "now") + "</select></label></div></div></div><div class=\"ab-bwrap\"><div class=\"ab-blist\" id=\"ab-blist\"></div><div id=\"ab-board\"><div class=\"ab-bempty\">Pick a class on the left.</div></div></div>";
        }

        private static int DefaultSession(SportClass k, DateTime now)
        {
            for (int i = 0; i < k.Dates.Count; i++)
            {
                if (k.Dates[i].AddMinutes(k.Minutes + 30) > now)
                {
                    return i;
                }
            }
            return Math.Max(0, k.Dates.Count - 1);
        }

        public async Task<ApiResponse> Side()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBoardJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime now = Store.Now(site);
            string show = GetDataValue("bshow") is "today" or "done" or "all" ? GetDataValue("bshow") : "now";
            List<SportClass> mine = site.Classes.Where(k => Access.Mine(c.U, k) && k.Status != "cancelled").ToList();
            List<SportClass> list = mine.Where(k =>
            {
                string st = Rules.State(site, k, now);
                return show switch
                {
                    "today" => k.Dates.Any(d => d.Date == now.Date),
                    "done" => st == "completed",
                    "all" => true,
                    _ => st != "completed"
                };
            }).OrderBy(k => k.Dates.Any(d => d.Date == now.Date) ? 0 : 1).ThenBy(k => Rules.State(site, k, now) == "progress" ? 0 : 1).ThenBy(k => Rules.Start(k)).ToList();
            StringBuilder sb = new StringBuilder();
            string group = string.Empty;
            foreach (SportClass k in list)
            {
                string g = k.Dates.Any(d => d.Date == now.Date) ? "Today" : Rules.State(site, k, now) == "progress" ? "Running" : Rules.State(site, k, now) == "completed" ? "Finished" : "Upcoming";
                if (g != group)
                {
                    sb.Append("<div class=\"ab-bl-h\">" + g + "</div>");
                    group = g;
                }
                DateTime? next = k.Dates.Where(d => d.AddMinutes(k.Minutes) > now).Select(d => (DateTime?)d).FirstOrDefault();
                sb.Append("<button type=\"button\" class=\"ab-bl" + (k.Id == GetDataValue("kid") ? " ab-act" : string.Empty) + "\" data-id=\"" + Ui.Attr(k.Id) + "\" onclick=\"AdminBoardJs.pick('" + Ui.Attr(k.Id) + "')\"><b>" + Ui.Enc(k.Title) + "</b><small>" + (next != null ? (next.Value.Date == now.Date ? "Today " : next.Value.ToString("ddd MMM d ", Ui.Inv)) + Ui.Hm(site, next.Value) : "Ended " + Ui.Short(Rules.End(k))) + " &middot; " + Rules.Taken(site, k) + "/" + k.Capacity + "</small>" + Ui.StatePill("ab", Rules.State(site, k, now)) + "</button>");
            }
            if (list.Count == 0)
            {
                sb.Append("<p class=\"ab-none\">No classes here.</p>");
            }
            response.SetElementContents("ab-blist", sb.ToString());
            string pick = list.Any(k => k.Id == GetDataValue("kid")) ? GetDataValue("kid") : mine.Any(k => k.Id == GetDataValue("kid")) ? GetDataValue("kid") : list.FirstOrDefault()?.Id ?? string.Empty;
            if (pick != string.Empty)
            {
                SportClass k = site.Classes.First(x => x.Id == pick);
                response.SetElementContents("ab-board", Board(c, site, k, DefaultSession(k, now)));
                response.ExecuteScript("AdminBoardJs.shown('" + Ui.Js(pick) + "'," + DefaultSession(k, now) + ");");
            }
            return response;
        }

        private static string Board(Ctx c, SiteData site, SportClass k, int si)
        {
            DateTime now = Store.Now(site);
            bool canE = Access.Can(c.U, AreaKey, 'E');
            si = Math.Clamp(si, 0, Math.Max(0, k.Dates.Count - 1));
            List<Registration> roster = site.Registrations.Where(r => r.Class == k.Id && r.Status is "confirmed" or "pending" or "offered").OrderBy(r => r.Name).ToList();
            List<Registration> waiting = site.Registrations.Where(r => r.Class == k.Id && r.Status == "waitlist").OrderBy(r => r.Created).ToList();
            int past = k.Dates.Count(d => d.AddMinutes(k.Minutes) <= now);
            List<Visit> visits = site.Visits.Where(v => v.Kind == "class" && roster.Any(r => r.Id == v.Reg)).ToList();
            int possible = roster.Count(r => r.Status == "confirmed") * past;
            int came = visits.Count(v => v.Session < past);
            StringBuilder sb = new StringBuilder("<section class=\"ab-panel\"><div class=\"ab-bhd\"><div><h2>" + Ui.Enc(k.Title) + "</h2><p>" + Ui.Enc(k.Id) + " &middot; " + Ui.ClassWhen(site, k) + " &middot; " + Ui.Enc(k.Place) + " &middot; " + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</p></div><div class=\"ab-bstats\"><div><b>" + Rules.Taken(site, k) + "/" + k.Capacity + "</b><small>seats</small></div><div><b>" + waiting.Count + "</b><small>waiting</small></div><div><b>" + (possible > 0 ? (came * 100 / possible) + "%" : "&ndash;") + "</b><small>attendance</small></div><div>" + Ui.StatePill("ab", Rules.State(site, k, now)) + "</div></div></div><div class=\"ab-sess\" role=\"tablist\" aria-label=\"Sessions\">");
            for (int i = 0; i < k.Dates.Count; i++)
            {
                DateTime d = k.Dates[i];
                bool isPast = d.AddMinutes(k.Minutes) <= now;
                sb.Append("<button type=\"button\" role=\"tab\" aria-selected=\"" + (i == si ? "true" : "false") + "\" class=\"ab-sb" + (i == si ? " ab-act" : string.Empty) + (isPast ? " ab-past" : string.Empty) + (d.Date == now.Date ? " ab-today" : string.Empty) + "\" onclick=\"AdminBoardJs.session(" + i + ")\"><span>#" + (i + 1) + "</span><b>" + d.ToString("MMM d", Ui.Inv) + "</b><span>" + d.ToString("ddd", Ui.Inv) + "</span></button>");
            }
            DateTime sd = k.Dates.Count > 0 ? k.Dates[si] : k.First;
            bool open = sd.Date <= now.Date && k.Status != "cancelled";
            sb.Append("</div><h2 style=\"margin:6px 0 10px\">Session " + (si + 1) + " &middot; " + Ui.LongDay(sd) + " &middot; " + Ui.Hm(site, sd) + "</h2>");
            if (!open)
            {
                sb.Append("<p class=\"ab-hint\" style=\"margin-bottom:10px\">Check-in opens on the day of the session.</p>");
            }
            sb.Append("<div class=\"ab-tbl ab-rtbl\">" + Head(new[] { ("Participant", string.Empty), ("Account", string.Empty), ("Status", string.Empty), ("In", string.Empty), ("Out", string.Empty), (string.Empty, string.Empty) }));
            if (roster.Count == 0)
            {
                sb.Append("<div class=\"ab-tr\"><span class=\"ab-none ab-wide\">Nobody registered yet.</span></div>");
            }
            foreach (Registration r in roster)
            {
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
                Visit? v = visits.FirstOrDefault(x => x.Reg == r.Id && x.Session == si);
                string acts = string.Empty;
                if (canE && open && r.Status == "confirmed")
                {
                    acts = v == null ? "<button type=\"button\" class=\"ab-btn ab-btn-sm ab-btn-accent\" onclick=\"AdminBoardJs.act('in','" + Ui.Attr(r.Id) + "')\">Check in</button>" : (v.Out == null ? "<button type=\"button\" class=\"ab-btn ab-btn-sm\" onclick=\"AdminBoardJs.act('out','" + Ui.Attr(r.Id) + "')\">Check out</button>" : string.Empty) + "<button type=\"button\" class=\"ab-btn ab-btn-sm ab-btn-ghost\" onclick=\"AdminBoardJs.act('undo','" + Ui.Attr(r.Id) + "')\">Undo</button>";
                }
                else if (r.Status == "pending")
                {
                    acts = "<a class=\"ab-btn ab-btn-sm ab-btn-ghost\" href=\"AdminRegistrations?id=" + Uri.EscapeDataString(r.Id) + "\">Take payment</a>";
                }
                sb.Append("<div class=\"ab-tr\"><span class=\"ab-wide\"><b>" + Ui.Enc(r.Name) + "</b><small>" + (r.Born != null ? "Age " + Rules.Age(r.Born.Value, now) : string.Empty) + " &middot; " + Ui.Enc(r.Code) + "</small></span><span data-l=\"Account\">" + Ui.Enc(m != null ? Rules.FullName(m) : r.Member) + "<small>" + Ui.Enc(m?.Phone ?? string.Empty) + "</small></span><span data-l=\"Status\">" + Ui.Pill("ab", Rules.RegStatuses, r.Status) + "</span><span data-l=\"In\">" + (v != null ? "<span class=\"ab-in\">" + Ui.Hm(site, Store.Local(site, v.In)) + "</span>" : "&ndash;") + "</span><span data-l=\"Out\">" + (v?.Out != null ? Ui.Hm(site, Store.Local(site, v.Out.Value)) : "&ndash;") + "</span><span class=\"ab-acts2\">" + acts + "</span></div>");
            }
            sb.Append("</div></section>");
            if (roster.Count > 0 && k.Dates.Count > 0)
            {
                sb.Append("<section class=\"ab-panel\"><h2>Attendance</h2><div class=\"ab-att\"><div class=\"ab-agrid\" style=\"--n:" + k.Dates.Count + "\"><div class=\"ab-arow ab-ah\"><span>Participant</span>");
                for (int i = 0; i < k.Dates.Count; i++)
                {
                    sb.Append("<span>" + (i + 1) + "</span>");
                }
                sb.Append("<span>Total</span></div>");
                foreach (Registration r in roster.Where(r => r.Status == "confirmed"))
                {
                    sb.Append("<div class=\"ab-arow\"><span title=\"" + Ui.Attr(r.Name) + "\">" + Ui.Enc(r.Name) + "</span>");
                    int n = 0;
                    for (int i = 0; i < k.Dates.Count; i++)
                    {
                        bool y = visits.Any(x => x.Reg == r.Id && x.Session == i);
                        bool done = k.Dates[i].AddMinutes(k.Minutes) <= now;
                        n += y ? 1 : 0;
                        sb.Append("<span><i class=\"ab-dot" + (y ? " ab-y" : done ? " ab-n" : string.Empty) + "\" title=\"Session " + (i + 1) + (y ? ": attended" : done ? ": missed" : string.Empty) + "\"></i></span>");
                    }
                    sb.Append("<span><b>" + n + "</b>/" + past + "</span></div>");
                }
                sb.Append("</div></div></section>");
            }
            sb.Append("<section class=\"ab-panel\"><div class=\"ab-ph\"><h2>Waitlist</h2><a href=\"AdminClasses?id=" + Uri.EscapeDataString(k.Id) + "\">Class settings</a></div>");
            List<Registration> offered = roster.Where(r => r.Status == "offered").ToList();
            if (waiting.Count == 0 && offered.Count == 0)
            {
                sb.Append("<p class=\"ab-none\">Nobody is waiting.</p>");
            }
            else
            {
                sb.Append("<div class=\"ab-wl\">");
                foreach (Registration r in offered)
                {
                    sb.Append("<div class=\"ab-wli\"><span><b>" + Ui.Enc(r.Name) + "</b><small>Seat offered" + (r.OfferExp != null ? ", pay by " + Ui.When(site, Store.Local(site, r.OfferExp.Value)) : string.Empty) + "</small></span>" + Ui.Pill("ab", Rules.RegStatuses, r.Status) + "</div>");
                }
                int left = Rules.Left(site, k);
                int pos = 1;
                foreach (Registration r in waiting)
                {
                    global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
                    sb.Append("<div class=\"ab-wli\"><span class=\"ab-num1\">" + pos++ + "</span><span style=\"flex:1\"><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(m != null ? Rules.FullName(m) : string.Empty) + " &middot; joined " + Ui.Day(Store.Local(site, r.Created), false) + "</small></span>" + (canE && now < Rules.Start(k) ? (left > 0 ? "<button type=\"button\" class=\"ab-btn ab-btn-sm ab-btn-accent\" onclick=\"AdminBoardJs.offer('" + Ui.Attr(r.Id) + "','" + Ui.Js(r.Name) + "')\">Offer seat</button>" : "<span class=\"ab-hint\">No free seat</span>") : string.Empty) + "</div>");
                }
                sb.Append("</div>");
                if (left == 0 && waiting.Count > 0)
                {
                    sb.Append("<p class=\"ab-hint\" style=\"margin-top:8px\">Seats are offered automatically when someone cancels. Add seats in Class settings to offer more now.</p>");
                }
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Board()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBoardJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == GetDataValue("kid"));
            if (k == null || !Access.Mine(c.U, k))
            {
                return response;
            }
            int si = int.TryParse(GetDataValue("bsess"), out int s) && s >= 0 ? s : DefaultSession(k, Store.Now(site));
            response.SetElementContents("ab-board", Board(c, site, k, si));
            response.ExecuteScript("AdminBoardJs.shown('" + Ui.Js(k.Id) + "'," + Math.Clamp(si, 0, Math.Max(0, k.Dates.Count - 1)) + ");");
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBoardJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "board", GetDataValue("rid"), "You can look at the board but not check people in.");
            }
            string what = GetDataValue("bwhat");
            string msg = string.Empty;
            string log = string.Empty;
            SiteData? saved = null;
            SportClass? k = null;
            int si = int.TryParse(GetDataValue("bsess"), out int s) ? s : 0;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                DateTime now = Store.Now(site);
                DateTime nowU = DateTime.UtcNow;
                k = site.Classes.FirstOrDefault(x => x.Id == GetDataValue("kid"));
                Registration? r = k == null ? null : site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Class == k.Id);
                if (k == null || r == null || !Access.Mine(c.U, k))
                {
                    msg = "That registration is gone. Reload the board.";
                }
                else if (what == "offer")
                {
                    if (r.Status != "waitlist" || Rules.Left(site, k) <= 0 || now >= Rules.Start(k))
                    {
                        msg = "There's no free seat to offer right now.";
                    }
                    else
                    {
                        r.Status = "offered";
                        r.Offered = nowU;
                        DateTime exp = nowU.AddHours(Math.Max(1, site.Settings.OfferHours));
                        DateTime startU = Store.Utc(site, Rules.Start(k));
                        r.OfferExp = exp > startU ? startU : exp;
                        msg = "Seat offered to " + r.Name + ".";
                        log = "offer seat";
                    }
                }
                else
                {
                    si = Math.Clamp(si, 0, Math.Max(0, k.Dates.Count - 1));
                    Visit? v = site.Visits.FirstOrDefault(x => x.Kind == "class" && x.Reg == r.Id && x.Session == si);
                    global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
                    if (k.Dates.Count == 0 || k.Dates[si].Date > now.Date)
                    {
                        msg = "Check-in opens on the day of the session.";
                    }
                    else if (r.Status != "confirmed")
                    {
                        msg = "Only confirmed, paid registrations can be checked in.";
                    }
                    else if (what == "in" && v == null)
                    {
                        site.Visits.Add(new Visit { Id = Store.NextId(site.Visits.Select(x => x.Id), "V-", 7000), Member = r.Member, Kind = "class", Reg = r.Id, Session = si, Name = r.Name, In = nowU, By = c.U.Id });
                        if (m != null)
                        {
                            Rules.AddPoints(site, m, site.Settings.PerSession, "session", r.Id, k.Title + ", session " + (si + 1), c.U.Id, nowU);
                        }
                        msg = r.Name + " checked in.";
                        log = "check in";
                    }
                    else if (what == "out" && v != null && v.Out == null)
                    {
                        v.Out = nowU;
                        msg = r.Name + " checked out.";
                        log = "check out";
                    }
                    else if (what == "undo" && v != null)
                    {
                        site.Visits.Remove(v);
                        if (m != null && site.Points.Any(p => p.Reg == r.Id && p.Kind == "session" && p.Note.EndsWith("session " + (si + 1), StringComparison.Ordinal)))
                        {
                            Rules.AddPoints(site, m, -site.Settings.PerSession, "session", r.Id, "Check-in undone, session " + (si + 1), c.U.Id, nowU);
                        }
                        msg = "Check-in for " + r.Name + " removed.";
                        log = "undo check in";
                    }
                    else
                    {
                        msg = "Nothing to change.";
                    }
                }
                if (log != string.Empty)
                {
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                }
            }
            if (saved != null && k != null)
            {
                await Store.Log(c.Sbx, c.U.Id, log, k.Id + " · session " + (si + 1) + " · " + GetDataValue("rid"), true);
                response.SetElementContents("ab-board", Board(c, saved, k, si));
            }
            response.ExecuteScript("AdminBoardJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }
    }
}
