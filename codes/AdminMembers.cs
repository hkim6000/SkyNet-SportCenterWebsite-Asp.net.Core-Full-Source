using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminMembers : WebPage
    {
        private const string AreaKey = "members";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Members");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--am-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("am-menu", Auth.Menu("am", "AdminMembers", c, site));
            response.SetElementContents("am-who", Auth.Who("am", c));
            response.SetElementContents("am-demo", Auth.DemoPill("am", c));
            response.SetElementContents("am-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminMembersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"am-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"am-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminMembersJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("am-main", NoAccess("Members"));
                return response;
            }
            response.SetElementContents("am-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminMembersJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"am-tr am-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 25;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            return "<div class=\"am-ptop\"><div><h1>Members</h1><p>Everyone with a record: open members who bought a day pass, and signed-up members with points and tiers. One list, one record per email.</p></div></div><div class=\"am-desk\"><section class=\"am-panel\"><h2>Front desk check-in</h2><form class=\"am-cin\" onsubmit=\"return AdminMembersJs.checkIn()\" novalidate><input type=\"text\" id=\"am-ccode\" maxlength=\"8\" placeholder=\"Pass code\" autocomplete=\"off\" aria-label=\"Day pass code\"" + (canE ? string.Empty : " disabled") + "><button type=\"submit\" class=\"am-btn am-btn-accent\"" + (canE ? string.Empty : " disabled") + ">Check in</button></form><p class=\"am-hint\" style=\"margin-top:8px\">Day passes are checked in here. Class check-ins are on the Class board.</p>" + (canE ? "<p style=\"margin-top:8px\"><button type=\"button\" class=\"am-btn am-btn-sm am-btn-ghost\" onclick=\"AdminMembersJs.sellForm()\">Sell a day pass to a walk-in</button></p><form class=\"am-form\" id=\"am-sellf\" hidden onsubmit=\"return AdminMembersJs.sell()\" novalidate style=\"margin-top:10px\"><div class=\"am-f2\"><label class=\"am-field\"><span>First name</span><input type=\"text\" id=\"am-wfirst\" maxlength=\"40\"></label><label class=\"am-field\"><span>Last name</span><input type=\"text\" id=\"am-wlast\" maxlength=\"40\"></label></div><div class=\"am-f2\"><label class=\"am-field\"><span>Email</span><input type=\"email\" id=\"am-wmail\" maxlength=\"120\"></label><label class=\"am-field\"><span>Phone</span><input type=\"tel\" id=\"am-wphone\" maxlength=\"30\"></label></div><em class=\"am-err\" id=\"am-e-sell\" role=\"alert\"></em><div><button type=\"submit\" class=\"am-btn am-btn-sm am-btn-accent\">Paid " + Ui.Money(site.Settings.PassPrice) + " at the desk &middot; check in</button></div></form>" : string.Empty) + "</section><section class=\"am-panel\"><div class=\"am-ph\"><h2>In the building on a day pass</h2></div><div class=\"am-here\" id=\"am-here\"></div></section></div><div class=\"am-bar\"><label>Search<input type=\"search\" id=\"am-q\" placeholder=\"Name, email, phone or ID\" maxlength=\"60\" oninput=\"AdminMembersJs.search()\"></label><label>Account<select id=\"am-mtype\" onchange=\"AdminMembersJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Everyone"), ("member", "Members"), ("open", "Open (day pass)") }, string.Empty) + "</select></label><label>Tier<select id=\"am-mtier\" onchange=\"AdminMembersJs.list(1)\"><option value=\"\">Any tier</option>" + Ui.Opts(Rules.Tiers.Select(t => (t.Key, t.Name)), string.Empty) + "</select></label><label>Status<select id=\"am-mstat\" onchange=\"AdminMembersJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Any"), ("active", "Active"), ("suspended", "Suspended") }, string.Empty) + "</select></label></div><div class=\"am-tbl am-mtbl\" id=\"am-list\"></div><div id=\"am-pager\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string term = Ui.Clip(GetDataValue("term"), 60);
            string type = GetDataValue("mtype") is "member" or "open" ? GetDataValue("mtype") : string.Empty;
            string tier = Ui.Clip(GetDataValue("mtier"), 10);
            string stat = GetDataValue("mstat") is "active" or "suspended" ? GetDataValue("mstat") : string.Empty;
            List<global::Sport.Models.ClubMember> all = site.Members.Where(m => (type == string.Empty || m.Type == type) && (stat == string.Empty || m.Status == stat) && (tier == string.Empty || (m.Type == "member" && Rules.TierOf(site.Settings, m.Lifetime) == tier)) && (term == string.Empty || Rules.FullName(m).Contains(term, StringComparison.OrdinalIgnoreCase) || m.Email.Contains(term, StringComparison.OrdinalIgnoreCase) || m.Phone.Contains(term) || m.Id.Equals(term, StringComparison.OrdinalIgnoreCase))).OrderBy(m => m.Last).ThenBy(m => m.First).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Person", string.Empty), ("Account", string.Empty), ("Tier", string.Empty), ("Points", "am-num"), ("Classes", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"am-tr\"><span class=\"am-none am-wide\">Nobody matches.</span></div>");
            }
            foreach (global::Sport.Models.ClubMember m in all.Skip((page - 1) * Size).Take(Size))
            {
                int regs = site.Registrations.Count(r => r.Member == m.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist");
                int visits = site.Visits.Count(v => v.Member == m.Id);
                sb.Append("<div class=\"am-tr\"><span class=\"am-wide\" style=\"display:flex;gap:10px;align-items:center\">" + Ui.MemberAv("am", m) + "<span style=\"min-width:0\"><b>" + Ui.Enc(Rules.FullName(m)) + "</b><small>" + Ui.Enc(m.Email) + "</small></span></span><span data-l=\"Account\"><span class=\"am-st am-st-" + (m.Type == "member" ? "member\">Member" : "open-acct\">Open") + "</span></span><span data-l=\"Tier\">" + (m.Type == "member" ? Ui.TierPill("am", Rules.TierOf(site.Settings, m.Lifetime)) : "&ndash;") + "</span><span data-l=\"Points\" class=\"am-num\">" + (m.Type == "member" ? Ui.Num(m.Balance) + "<small>" + Ui.Num(m.Lifetime) + " lifetime</small>" : "&ndash;") + "</span><span data-l=\"Classes\">" + regs + " active<small>" + visits + " check-ins</small></span><span data-l=\"Status\"><span class=\"am-st am-st-" + m.Status + "\">" + (m.Status == "suspended" ? "Suspended" : "Active") + "</span></span><span><button type=\"button\" class=\"am-btn am-btn-sm am-btn-ghost\" onclick=\"AdminMembersJs.open('" + Ui.Attr(m.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("am-list", sb.ToString());
            response.SetElementContents("am-pager", Ui.Pager("am", page, Size, all.Count, "person", "people", "AdminMembersJs.list"));
            return response;
        }

        private static string Here(SiteData site, bool canE)
        {
            List<Visit> list = site.Visits.Where(v => v.Kind == "pass" && v.Out == null && v.In > DateTime.UtcNow.AddHours(-18)).OrderByDescending(v => v.In).ToList();
            if (list.Count == 0)
            {
                return "<p class=\"am-none\">Nobody checked in on a day pass right now.</p>";
            }
            StringBuilder sb = new StringBuilder();
            foreach (Visit v in list)
            {
                sb.Append("<div class=\"am-hrw\"><span><b>" + Ui.Enc(v.Name) + "</b><small>In since " + Ui.Hm(site, Store.Local(site, v.In)) + "</small></span>" + (canE ? "<button type=\"button\" class=\"am-btn am-btn-sm\" onclick=\"AdminMembersJs.checkOut('" + Ui.Attr(v.Id) + "')\">Check out</button>" : string.Empty) + "</div>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Desk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            response.SetElementContents("am-here", Here(site, Access.Can(c.U, AreaKey, 'E')));
            return response;
        }

        public async Task<ApiResponse> CheckIn()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "check in", GetDataValue("ccode"), "You can look but not check people in.");
            }
            string code = Rules.CleanCode(GetDataValue("ccode"));
            string msg;
            bool ok = false;
            SiteData? saved = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                DateTime today = Store.Today(site);
                DayPass? d = site.Passes.FirstOrDefault(x => x.Code == code);
                global::Sport.Models.ClubMember? m = d == null ? null : site.Members.FirstOrDefault(x => x.Id == d.Member);
                if (d == null || m == null)
                {
                    msg = "No day pass has that code. Class registrations are checked in on the Class board.";
                }
                else if (m.Status == "suspended")
                {
                    msg = Rules.FullName(m) + "'s account is suspended.";
                }
                else if (d.Status == "refunded")
                {
                    msg = "That pass was refunded.";
                }
                else if (d.Status == "used")
                {
                    msg = "That pass was already used.";
                }
                else if (d.Day.Date != today)
                {
                    msg = "That pass is for " + Ui.LongDay(d.Day) + ".";
                }
                else
                {
                    DateTime nowU = DateTime.UtcNow;
                    bool unpaid = !site.Payments.Any(p => p.Pass == d.Id && p.Kind == "charge");
                    if (unpaid)
                    {
                        Rules.Charge(site, null, d, m.Id, d.Price, "desk", string.Empty, string.Empty, c.U.Id, nowU, site.Settings.PassName);
                        Rules.AddPoints(site, m, Rules.EarnFor(site.Settings, d.Price), "pay", string.Empty, site.Settings.PassName + " " + d.Code, c.U.Id, nowU);
                    }
                    d.Status = "used";
                    site.Visits.Add(new Visit { Id = Store.NextId(site.Visits.Select(x => x.Id), "V-", 7000), Member = m.Id, Kind = "pass", Pass = d.Id, Name = Rules.FullName(m), In = nowU, By = c.U.Id });
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                    ok = true;
                    msg = Rules.FullName(m) + " checked in." + (unpaid ? " Take " + Ui.Plain(d.Price) + " at the desk." : string.Empty);
                }
            }
            if (ok && saved != null)
            {
                await Store.Log(c.Sbx, c.U.Id, "check in", "Day pass · " + code, true);
                response.SetElementContents("am-here", Here(saved, true));
                response.ExecuteScript("document.getElementById('am-ccode').value='';AdminMembersJs.list(1);");
            }
            response.ExecuteScript("AdminMembersJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> CheckOut()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "check out", GetDataValue("vid"), "You can look but not check people out.");
            }
            SiteData? saved = null;
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Visit? v = site.Visits.FirstOrDefault(x => x.Id == GetDataValue("vid") && x.Out == null);
                if (v != null)
                {
                    v.Out = DateTime.UtcNow;
                    name = v.Name;
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                }
            }
            if (saved != null)
            {
                await Store.Log(c.Sbx, c.U.Id, "check out", "Day pass · " + name, true);
                response.SetElementContents("am-here", Here(saved, true));
                response.ExecuteScript("AdminMembersJs.toast('" + Ui.Js(name + " checked out.") + "');");
            }
            return response;
        }

        public async Task<ApiResponse> Sell()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "sell pass", "desk", "You can look but not sell passes.");
            }
            string mid = Ui.Clip(GetDataValue("wmid"), 20);
            string first = Ui.OneLine(GetDataValue("wfirst"), 40);
            string last = Ui.OneLine(GetDataValue("wlast"), 40);
            string mail = Ui.Clip(GetDataValue("wmail"), 120).Trim();
            string phone = Ui.CleanPhone(GetDataValue("wphone"));
            if (mid == string.Empty && (first.Length < 1 || last.Length < 1 || !Ui.Email(mail)))
            {
                response.SetElementContents("am-e-sell", "Enter a first and last name and an email.");
                return response;
            }
            string msg = string.Empty;
            SiteData? saved = null;
            string target = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                DateTime nowU = DateTime.UtcNow;
                global::Sport.Models.ClubMember? m = mid != string.Empty ? site.Members.FirstOrDefault(x => x.Id == mid) : site.Members.FirstOrDefault(x => x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase));
                if (m != null && m.Status == "suspended")
                {
                    msg = "This account is suspended.";
                }
                else
                {
                    if (m == null)
                    {
                        m = new global::Sport.Models.ClubMember { Id = Store.NextId(site.Members.Select(x => x.Id), "M-", 1000), Type = "open", First = first, Last = last, Email = mail, Phone = phone, Created = nowU };
                        site.Members.Add(m);
                    }
                    DayPass d = new DayPass { Id = Store.NextId(site.Passes.Select(x => x.Id), "D-", 3000), Code = Rules.NewCode(site), Member = m.Id, Day = Store.Today(site), Price = site.Settings.PassPrice, Status = "used", Bought = nowU, Method = "desk", By = c.U.Id };
                    site.Passes.Add(d);
                    Rules.Charge(site, null, d, m.Id, d.Price, "desk", string.Empty, string.Empty, c.U.Id, nowU, site.Settings.PassName);
                    Rules.AddPoints(site, m, Rules.EarnFor(site.Settings, d.Price), "pay", string.Empty, site.Settings.PassName + " " + d.Code, c.U.Id, nowU);
                    site.Visits.Add(new Visit { Id = Store.NextId(site.Visits.Select(x => x.Id), "V-", 7000), Member = m.Id, Kind = "pass", Pass = d.Id, Name = Rules.FullName(m), In = nowU, By = c.U.Id });
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                    target = d.Code + " · " + Rules.FullName(m);
                    msg = Rules.FullName(m) + " paid and checked in. Pass code " + d.Code + ".";
                }
            }
            if (saved == null)
            {
                response.SetElementContents("am-e-sell", Ui.Enc(msg));
                response.ExecuteScript("AdminMembersJs.toast('" + Ui.Js(msg) + "');");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "day pass", target + " · desk", true);
            response.SetElementContents("am-here", Here(saved, true));
            response.ExecuteScript("['wfirst','wlast','wmail','wphone'].forEach(function(k){var e=document.getElementById('am-'+k);if(e){e.value='';}});AdminMembersJs.list(1);AdminMembersJs.closeModal();AdminMembersJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }

        private static string Detail(Ctx c, SiteData site, global::Sport.Models.ClubMember m, Dictionary<string, Staff> staff)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canX = Access.Can(c.U, AreaKey, 'X');
            DateTime today = Store.Today(site);
            string ro = canE ? string.Empty : " readonly";
            string tier = Rules.TierOf(site.Settings, m.Lifetime);
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(Rules.FullName(m)) + "</h2><p class=\"am-hint\" style=\"margin:-6px 0 12px\"><span class=\"am-st am-st-" + (m.Type == "member" ? "member\">Member" : "open-acct\">Open member") + "</span> " + (m.Type == "member" ? Ui.TierPill("am", tier) + " " : string.Empty) + "<span class=\"am-st am-st-" + m.Status + "\">" + (m.Status == "suspended" ? "Suspended" : "Active") + "</span> &nbsp;" + Ui.Enc(m.Id) + " &middot; " + Ui.Enc(m.Email) + " &middot; " + (m.Joined != null ? "joined " + Ui.Day(Store.Local(site, m.Joined.Value)) : "first visit " + Ui.Day(Store.Local(site, m.Created))) + "</p>");
            if (m.Status == "suspended")
            {
                sb.Append("<div class=\"am-warn-box\" style=\"margin-bottom:12px\"><b>Suspended" + (m.Suspended != null ? " " + Ui.Day(Store.Local(site, m.Suspended.Value)) : string.Empty) + ".</b> " + Ui.Enc(m.SuspendReason) + "</div>");
            }
            sb.Append("<div class=\"am-mgrid\"><div class=\"am-sec2\"><h3>Profile</h3><div class=\"am-f2\"><label class=\"am-field\"><span>First name</span><input type=\"text\" id=\"am-mfirst\" maxlength=\"40\" value=\"" + Ui.Attr(m.First) + "\"" + ro + "></label><label class=\"am-field\"><span>Last name</span><input type=\"text\" id=\"am-mlast\" maxlength=\"40\" value=\"" + Ui.Attr(m.Last) + "\"" + ro + "></label></div><div class=\"am-f2\"><label class=\"am-field\"><span>Phone</span><input type=\"tel\" id=\"am-mphone\" maxlength=\"30\" value=\"" + Ui.Attr(m.Phone) + "\"" + ro + "></label><label class=\"am-field\"><span>Date of birth</span><input type=\"date\" id=\"am-mborn\" value=\"" + (m.Born != null ? Ui.Ymd(m.Born.Value) : string.Empty) + "\"" + ro + "></label></div><label class=\"am-field\"><span>Account type</span><select id=\"am-mtype2\"" + (canE ? string.Empty : " disabled") + ">" + Ui.Opts(new[] { ("open", "Open member (day pass only, no points)"), ("member", "Member (signs in, classes, points)") }, m.Type) + "</select></label><p class=\"am-hint\" style=\"margin-top:-4px\">Changing an open member to a member doesn't set a password; they choose one with Forgot password.</p><label class=\"am-field\"><span>Staff notes</span><textarea id=\"am-mnotes\" rows=\"2\" maxlength=\"500\"" + ro + ">" + Ui.Enc(m.Notes) + "</textarea></label><p class=\"am-hint\">Billing: " + (m.Street != string.Empty ? Ui.Enc(m.Street + ", " + m.City + " " + m.Region + " " + m.Zip) : "not given yet (asked at the first payment)") + "</p><em class=\"am-err\" id=\"am-e-mem\" role=\"alert\"></em>" + (canE ? "<div style=\"display:flex;gap:8px;flex-wrap:wrap\"><button type=\"button\" class=\"am-btn am-btn-sm am-btn-accent\" onclick=\"AdminMembersJs.save('" + Ui.Attr(m.Id) + "')\">Save profile</button><button type=\"button\" class=\"am-btn am-btn-sm " + (m.Status == "suspended" ? "am-btn-ghost\" onclick=\"AdminMembersJs.suspend('" + Ui.Attr(m.Id) + "',false)\">Reactivate" : "am-btn-danger\" onclick=\"AdminMembersJs.suspend('" + Ui.Attr(m.Id) + "',true)\">Suspend") + "</button><button type=\"button\" class=\"am-btn am-btn-sm am-btn-ghost\" onclick=\"AdminMembersJs.sellTo('" + Ui.Attr(m.Id) + "')\">Sell day pass &amp; check in</button></div>" : string.Empty) + "</div>");
            sb.Append("<div class=\"am-sec2\"><h3>Points</h3>");
            if (m.Type == "member")
            {
                (string next, int at) = Rules.NextTier(site.Settings, m.Lifetime);
                sb.Append("<div class=\"am-ptsbig\"><div><b>" + Ui.Num(m.Balance) + "</b><small>to spend</small></div><div><b>" + Ui.Num(m.Lifetime) + "</b><small>lifetime</small></div><div>" + Ui.TierPill("am", tier) + "<small style=\"display:block;margin-top:4px\">" + (next != string.Empty ? Ui.Num(at - m.Lifetime) + " to " + Rules.TierName(next) : "Top tier") + "</small></div></div>");
                if (canX)
                {
                    sb.Append("<div class=\"am-f2\"><label class=\"am-field\"><span>Adjust points (+ or &minus;)</span><input type=\"number\" id=\"am-apts\" step=\"10\" placeholder=\"150 or -50\"></label><label class=\"am-field\"><span>Reason (required)</span><input type=\"text\" id=\"am-awhy\" maxlength=\"120\" placeholder=\"Volunteer thank-you\"></label></div><em class=\"am-err\" id=\"am-e-adj\" role=\"alert\"></em><div><button type=\"button\" class=\"am-btn am-btn-sm\" onclick=\"AdminMembersJs.adjust('" + Ui.Attr(m.Id) + "')\">Adjust points</button></div>");
                }
                sb.Append("<div class=\"am-mini\">");
                foreach (PointEntry p in site.Points.Where(p => p.Member == m.Id).OrderByDescending(p => p.When).Take(8))
                {
                    sb.Append("<div><span>" + Ui.Enc(p.Note != string.Empty ? p.Note : p.Kind) + "<small> &middot; " + Ui.Day(Store.Local(site, p.When), false) + (p.By != "web" && p.By != "system" ? " &middot; " + Ui.Enc(Who(staff, p.By)) : string.Empty) + "</small></span><span class=\"" + (p.Points < 0 ? "am-neg" : "am-pos") + "\">" + (p.Points > 0 ? "+" : "&minus;") + Ui.Num(Math.Abs(p.Points)) + "</span></div>");
                }
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<p class=\"am-hint\">Open members don't earn points. Points start when they sign up with this email.</p>");
            }
            sb.Append("</div><div class=\"am-sec2\"><h3>Family</h3><div class=\"am-mini\">");
            if (m.Family.Count == 0)
            {
                sb.Append("<p class=\"am-none\">No family members.</p>");
            }
            foreach (FamilyMember f in m.Family)
            {
                sb.Append("<div><span>" + Ui.Enc((f.First + " " + f.Last).Trim()) + "<small> &middot; " + Ui.Enc(Ui.NameOf(Rules.Relations, f.Relation)) + "</small></span><span>age " + Rules.Age(f.Born, today) + "</span></div>");
            }
            sb.Append("</div><h3>Classes</h3><div class=\"am-mini\">");
            List<Registration> regs = site.Registrations.Where(r => r.Member == m.Id).OrderByDescending(r => r.Created).Take(12).ToList();
            if (regs.Count == 0)
            {
                sb.Append("<p class=\"am-none\">No registrations.</p>");
            }
            foreach (Registration r in regs)
            {
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
                sb.Append("<div><a href=\"AdminRegistrations?id=" + Uri.EscapeDataString(r.Id) + "\" style=\"color:var(--am-link);font-weight:600\">" + Ui.Enc(k?.Title ?? r.Class) + "</a><span>" + Ui.Pill("am", Rules.RegStatuses, r.Status) + "</span></div>");
            }
            sb.Append("</div></div><div class=\"am-sec2\"><h3>Check-ins</h3><div class=\"am-mini\">");
            List<Visit> vs = site.Visits.Where(v => v.Member == m.Id).OrderByDescending(v => v.In).Take(10).ToList();
            if (vs.Count == 0)
            {
                sb.Append("<p class=\"am-none\">No check-ins yet.</p>");
            }
            foreach (Visit v in vs)
            {
                DateTime i = Store.Local(site, v.In);
                sb.Append("<div><span>" + (v.Kind == "pass" ? Ui.Enc(site.Settings.PassName) : "Class, session " + (v.Session + 1)) + "<small> &middot; " + Ui.Enc(v.Name) + "</small></span><span>" + Ui.Day(i, false) + " " + Ui.Hm(site, i) + (v.Out != null ? "&ndash;" + Ui.Hm(site, Store.Local(site, v.Out.Value)) : " &middot; in") + "</span></div>");
            }
            sb.Append("</div><h3>Day passes</h3><div class=\"am-mini\">");
            List<DayPass> ps = site.Passes.Where(p => p.Member == m.Id).OrderByDescending(p => p.Day).Take(6).ToList();
            if (ps.Count == 0)
            {
                sb.Append("<p class=\"am-none\">None.</p>");
            }
            foreach (DayPass p in ps)
            {
                sb.Append("<div><span class=\"am-code\">" + Ui.Enc(p.Code) + "</span><span>" + Ui.Day(p.Day, false) + " &middot; " + Ui.Enc(p.Status) + "</span></div>");
            }
            sb.Append("</div></div></div><div class=\"am-mfoot\"><button type=\"button\" class=\"am-btn am-btn-ghost\" onclick=\"AdminMembersJs.closeModal()\">Close</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "member", GetDataValue("mid"), "You don't have access to members.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == GetDataValue("mid"));
            if (m == null)
            {
                return response;
            }
            response.SetElementContents("am-mbody", Detail(c, site, m, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminMembersJs.openModal();");
            return response;
        }

        private async Task<ApiResponse> Reopen(ApiResponse response, Ctx c, SiteData site, string id, string msg)
        {
            global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == id);
            if (m != null)
            {
                response.SetElementContents("am-mbody", Detail(c, site, m, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            }
            response.ExecuteScript("AdminMembersJs.list(1);AdminMembersJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "member", GetDataValue("mid"), "You can look but not change members.");
            }
            string first = Ui.OneLine(GetDataValue("mfirst"), 40);
            string last = Ui.OneLine(GetDataValue("mlast"), 40);
            if (first.Length < 1 || last.Length < 1)
            {
                response.SetElementContents("am-e-mem", "Enter a first and last name.");
                return response;
            }
            SiteData? saved = null;
            string id = GetDataValue("mid");
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.First = first;
                    m.Last = last;
                    m.Phone = Ui.CleanPhone(GetDataValue("mphone"));
                    m.Born = Ui.ParseDay(GetDataValue("mborn")) ?? m.Born;
                    string type = GetDataValue("mtype2") == "member" ? "member" : "open";
                    if (type == "member" && m.Type == "open")
                    {
                        m.Joined = DateTime.UtcNow;
                    }
                    m.Type = type;
                    m.Notes = Ui.Clip(GetDataValue("mnotes"), 500).Trim();
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                }
            }
            if (saved == null)
            {
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "member", id + " · " + first + " " + last, true);
            return await Reopen(response, c, saved, id, "Saved");
        }

        public async Task<ApiResponse> Suspend()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "suspend", GetDataValue("mid"), "You can look but not change members.");
            }
            bool on = GetDataValue("son") == "1";
            string why = Ui.OneLine(GetDataValue("swhy"), 200);
            string id = GetDataValue("mid");
            SiteData? saved = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Status = on ? "suspended" : "active";
                    m.Suspended = on ? DateTime.UtcNow : null;
                    m.SuspendReason = on ? why : string.Empty;
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                }
            }
            if (saved == null)
            {
                return response;
            }
            if (on)
            {
                await Auth.CloseUser(c.Sbx, id, string.Empty, "member");
            }
            await Store.Log(c.Sbx, c.U.Id, on ? "suspend" : "reactivate", id + (on ? " · " + why : string.Empty), true);
            return await Reopen(response, c, saved, id, on ? "Suspended" : "Reactivated");
        }

        public async Task<ApiResponse> Adjust()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMembersJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "points", GetDataValue("mid"), "Adjusting points needs the Adjust points right.");
            }
            int pts = int.TryParse(GetDataValue("apts"), out int p) ? p : 0;
            string why = Ui.OneLine(GetDataValue("awhy"), 120);
            if (pts == 0 || Math.Abs(pts) > 100000)
            {
                response.SetElementContents("am-e-adj", "Enter the points to add (or a minus number to take away).");
                return response;
            }
            if (why.Length < 3)
            {
                response.SetElementContents("am-e-adj", "Write the reason. It shows in the member's points history.");
                return response;
            }
            string id = GetDataValue("mid");
            SiteData? saved = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == id && x.Type == "member");
                if (m != null)
                {
                    Rules.AddPoints(site, m, pts, "adjust", string.Empty, why, c.U.Id, DateTime.UtcNow);
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                }
            }
            if (saved == null)
            {
                response.SetElementContents("am-e-adj", "Points are for members only.");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "points", id + " · " + (pts > 0 ? "+" : string.Empty) + pts + " · " + why, true);
            return await Reopen(response, c, saved, id, "Points adjusted");
        }
    }
}
