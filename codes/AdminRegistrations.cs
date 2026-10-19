using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminRegistrations : WebPage
    {
        private const string AreaKey = "registrations";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Registrations");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ar-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ar-menu", Auth.Menu("ar", "AdminRegistrations", c, site));
            response.SetElementContents("ar-who", Auth.Who("ar", c));
            response.SetElementContents("ar-demo", Auth.DemoPill("ar", c));
            response.SetElementContents("ar-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminRegistrationsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ar-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ar-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminRegistrationsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ar-main", NoAccess("Registrations"));
                return response;
            }
            response.SetElementContents("ar-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminRegistrationsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ar-tr ar-th\">");
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
            return "<div class=\"ar-ptop\"><div><h1>Registrations</h1><p>Everyone signed up for a class, on the waitlist or waiting to pay at the front desk.</p></div><div class=\"ar-acts\">" + (canE ? "<button type=\"button\" class=\"ar-btn ar-btn-accent\" onclick=\"AdminRegistrationsJs.addForm()\">Register someone</button>" : string.Empty) + "</div></div><div class=\"ar-bar\"><label>Search<input type=\"search\" id=\"ar-q\" placeholder=\"Name, code or email\" maxlength=\"60\" oninput=\"AdminRegistrationsJs.search()\"></label><label>Status<select id=\"ar-rstatus\" onchange=\"AdminRegistrationsJs.list(1)\"><option value=\"\">Any status</option>" + Ui.Opts(Rules.RegStatuses.Select(s => (s.Key, s.Name)), string.Empty) + "</select></label><label>Class<select id=\"ar-rclass\" onchange=\"AdminRegistrationsJs.list(1)\"><option value=\"\">Any class</option>" + Ui.Opts(site.Classes.OrderBy(k => k.Title).Select(k => (k.Id, k.Title + " (" + k.Id + ")")), string.Empty) + "</select></label></div><div class=\"ar-bar\" id=\"ar-perbar\">" + Ui.PeriodBar("ar", "AdminRegistrationsJs.per", "all", string.Empty, string.Empty) + "</div><div class=\"ar-tbl ar-rgtbl\" id=\"ar-list\"></div><div id=\"ar-pager\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string term = Ui.Clip(GetDataValue("term"), 60);
            string status = Ui.Clip(GetDataValue("rstatus"), 20);
            string cls = Ui.Clip(GetDataValue("rclass"), 20);
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(Store.Today(site), per, GetDataValue("pfrom"), GetDataValue("pto"));
            Dictionary<string, global::Sport.Models.ClubMember> mem = site.Members.ToDictionary(m => m.Id);
            List<Registration> all = site.Registrations.Where(r => (status == string.Empty || r.Status == status) && (cls == string.Empty || r.Class == cls) && Ui.InPeriod(Store.Local(site, r.Created), from, to) && (term == string.Empty || r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Id.Equals(term, StringComparison.OrdinalIgnoreCase) || (mem.TryGetValue(r.Member, out global::Sport.Models.ClubMember? m) && (Rules.FullName(m).Contains(term, StringComparison.OrdinalIgnoreCase) || m.Email.Contains(term, StringComparison.OrdinalIgnoreCase))))).OrderByDescending(r => r.Created).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Code", string.Empty), ("Participant", string.Empty), ("Class", string.Empty), ("Status", string.Empty), ("Paid", "ar-num"), ("Booked", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"ar-tr\"><span class=\"ar-none ar-wide\">No registrations match.</span></div>");
            }
            foreach (Registration r in all.Skip((page - 1) * Size).Take(Size))
            {
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
                global::Sport.Models.ClubMember? m = mem.TryGetValue(r.Member, out global::Sport.Models.ClubMember? mm) ? mm : null;
                sb.Append("<div class=\"ar-tr\"><span data-l=\"Code\" class=\"ar-code\">" + Ui.Enc(r.Code) + "</span><span class=\"ar-wide\"><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(m != null ? Rules.FullName(m) + " · " + m.Email : r.Member) + "</small></span><span data-l=\"Class\" class=\"ar-wide\">" + Ui.Enc(k?.Title ?? r.Class) + "<small>" + (k != null ? Ui.Range(k) : string.Empty) + "</small></span><span data-l=\"Status\">" + Ui.Pill("ar", Rules.RegStatuses, r.Status) + (r.Ask != null && r.Ask.Status == "open" ? " <span class=\"ar-st ar-st-pending\">Refund asked</span>" : string.Empty) + "</span><span data-l=\"Paid\" class=\"ar-num\">" + Ui.Money(r.Paid - r.Refunded, true) + "</span><span data-l=\"Booked\">" + Ui.Day(Store.Local(site, r.Created), false) + "</span><span><button type=\"button\" class=\"ar-btn ar-btn-sm ar-btn-ghost\" onclick=\"AdminRegistrationsJs.open('" + Ui.Attr(r.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("ar-list", sb.ToString());
            response.SetElementContents("ar-pager", Ui.Pager("ar", page, Size, all.Count, "registration", "registrations", "AdminRegistrationsJs.list"));
            response.SetElementContents("ar-perbar", Ui.PeriodBar("ar", "AdminRegistrationsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        private static string Detail(Ctx c, SiteData site, Registration r)
        {
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
            bool canE = Access.Can(c.U, AreaKey, 'E');
            bool canRefund = Access.Can(c.U, "payments", 'X');
            DateTime now = Store.Now(site);
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(r.Name) + "</h2><p class=\"ar-hint\" style=\"margin:-6px 0 12px\">" + Ui.Pill("ar", Rules.RegStatuses, r.Status) + " &nbsp;" + Ui.Enc(r.Id) + " &middot; code <b class=\"ar-code\">" + Ui.Enc(r.Code) + "</b>" + (m != null ? " &middot; <a href=\"AdminMembers?id=" + Uri.EscapeDataString(m.Id) + "\" style=\"color:var(--ar-link);font-weight:700\">" + Ui.Enc(Rules.FullName(m)) + "</a>" : string.Empty) + "</p>");
            if (r.Status == "offered" && r.OfferExp != null)
            {
                sb.Append("<div class=\"ar-info-box\">Seat offered. The member has until " + Ui.When(site, Store.Local(site, r.OfferExp.Value)) + " to pay in the member area.</div>");
            }
            if (r.Status == "pending" && r.HoldExp != null)
            {
                sb.Append("<div class=\"ar-warn-box\">Seat held until " + Ui.When(site, Store.Local(site, r.HoldExp.Value)) + ". " + Ui.Money(r.Total, true) + " to take at the desk.</div>");
            }
            if (r.Ask != null)
            {
                sb.Append("<div class=\"" + (r.Ask.Status == "open" ? "ar-warn-box" : "ar-info-box") + "\" style=\"margin-top:8px\"><b>Refund request " + (r.Ask.Status == "open" ? "waiting" : r.Ask.Status) + "</b> &middot; " + Ui.Day(Store.Local(site, r.Ask.When)) + "<br>" + Ui.Enc(r.Ask.Reason) + (r.Ask.Status == "open" ? "<br><a href=\"AdminPayments?tab=asks\" style=\"color:var(--ar-link);font-weight:700\">Decide in Payments &amp; refunds</a>" : r.Ask.Note != string.Empty ? "<br><i>" + Ui.Enc(r.Ask.Note) + "</i>" : string.Empty) + "</div>");
            }
            if (r.EndNote != string.Empty)
            {
                sb.Append("<p class=\"ar-hint\" style=\"margin-top:8px\">" + Ui.Enc(r.EndNote) + "</p>");
            }
            sb.Append("<div style=\"margin-top:12px\">" + Rules.Receipt(site, r, "ar") + "</div><div class=\"ar-acts3\"><button type=\"button\" class=\"ar-btn ar-btn-sm ar-btn-ghost\" onclick=\"AdminRegistrationsJs.printBox('ar-rcpt-" + Ui.Attr(r.Code) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 9V3h10v6M7 17H4v-7h16v7h-3\"/><path d=\"M7 14h10v7H7z\"/></svg> Print receipt</button>");
            if (canE && r.Status == "pending")
            {
                sb.Append("<button type=\"button\" class=\"ar-btn ar-btn-sm ar-btn-accent\" onclick=\"AdminRegistrationsJs.desk('" + Ui.Attr(r.Id) + "')\">Payment taken at the desk</button>");
            }
            sb.Append("</div>");
            if (canE && r.Status is "confirmed" or "pending" or "offered" or "waitlist" && k != null)
            {
                decimal refundable = Rules.Refundable(r);
                (int pct, bool self, string rule) = Rules.Policy(site, k, now);
                decimal suggest = Math.Round(refundable * pct / 100m, 2);
                sb.Append("<div class=\"ar-cbox\"><h3>Cancel this registration</h3>" + (refundable > 0 ? "<p class=\"ar-hint\">Policy now: " + Ui.Enc(rule) + " Paid " + Ui.Money(refundable, true) + " still refundable.</p>" + (canRefund ? "<label class=\"ar-field\"><span>Refund ($)</span><input type=\"text\" inputmode=\"decimal\" id=\"ar-ramt\" maxlength=\"10\" value=\"" + suggest.ToString("0.00", Ui.Inv) + "\"></label>" : "<input type=\"hidden\" id=\"ar-ramt\" value=\"0\"><p class=\"ar-hint\">You don't have the Refund right, so this cancels without a refund. Ask a manager to refund in Payments &amp; refunds.</p>") : "<input type=\"hidden\" id=\"ar-ramt\" value=\"0\"><p class=\"ar-hint\">Nothing was paid, so nothing is refunded." + (r.Points > 0 ? " Points used are given back." : string.Empty) + "</p>") + "<label class=\"ar-field\"><span>Reason (kept in the log)</span><input type=\"text\" id=\"ar-rwhy\" maxlength=\"200\" placeholder=\"Asked at the front desk\"></label><em class=\"ar-err\" id=\"ar-e-rc\" role=\"alert\"></em><div><button type=\"button\" class=\"ar-btn ar-btn-sm ar-btn-danger\" onclick=\"AdminRegistrationsJs.cancelReg('" + Ui.Attr(r.Id) + "')\">Cancel registration</button></div></div>");
            }
            sb.Append("<div class=\"ar-mfoot\"><button type=\"button\" class=\"ar-btn ar-btn-ghost\" onclick=\"AdminRegistrationsJs.closeModal()\">Close</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "registration", GetDataValue("rid"), "You don't have access to registrations.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") || x.Code == GetDataValue("rid"));
            if (r == null)
            {
                return response;
            }
            response.SetElementContents("ar-mbody", Detail(c, site, r));
            response.ExecuteScript("AdminRegistrationsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Desk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "desk payment", GetDataValue("rid"), "You can look but not take payments.");
            }
            string msg = "That registration isn't waiting for a desk payment.";
            SiteData? saved = null;
            Registration? reg = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Status == "pending");
                if (r != null)
                {
                    Rules.Confirm(site, r, "desk", string.Empty, string.Empty, c.U.Id, DateTime.UtcNow);
                    await Store.SaveSite(site, c.Sbx);
                    msg = "Paid at the desk. The seat is confirmed.";
                    saved = site;
                    reg = r;
                }
            }
            if (saved != null && reg != null)
            {
                await Store.Log(c.Sbx, c.U.Id, "desk payment", reg.Id + " · " + Ui.Plain(reg.Total), true);
                response.SetElementContents("ar-mbody", Detail(c, saved, reg));
                response.ExecuteScript("AdminRegistrationsJs.list(1);");
            }
            response.ExecuteScript("AdminRegistrationsJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "cancel registration", GetDataValue("rid"), "You can look but not change registrations.");
            }
            decimal amt = decimal.TryParse(GetDataValue("ramt").Replace("$", string.Empty).Trim(), NumberStyles.Number, Ui.Inv, out decimal a) ? Math.Round(a, 2) : -1m;
            string why = Ui.OneLine(GetDataValue("rwhy"), 200);
            if (amt < 0)
            {
                response.SetElementContents("ar-e-rc", "Enter the refund as a number, or 0.");
                return response;
            }
            if (amt > 0 && !Access.Can(c.U, "payments", 'X'))
            {
                return await Refuse(response, c, "refund", GetDataValue("rid"), "Refunds need the Refund right in Payments & refunds.");
            }
            string err = string.Empty;
            SiteData? saved = null;
            Registration? reg = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Status is "confirmed" or "pending" or "offered" or "waitlist");
                if (r == null)
                {
                    err = "That registration can't be cancelled any more.";
                }
                else if (amt > Rules.Refundable(r))
                {
                    err = "The refund can't be more than " + Ui.Plain(Rules.Refundable(r)) + ".";
                }
                else
                {
                    Rules.EndReg(site, r, "cancelled", amt, c.U.Id, DateTime.UtcNow, "Cancelled by staff" + (why != string.Empty ? ": " + why : string.Empty) + (amt > 0 ? ". " + Ui.Plain(amt) + " refunded." : "."));
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                    reg = r;
                }
            }
            if (err != string.Empty || saved == null || reg == null)
            {
                response.SetElementContents("ar-e-rc", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "cancel", reg.Id + " · " + reg.Class + (amt > 0 ? " · refund " + Ui.Plain(amt) : string.Empty) + (why != string.Empty ? " · " + why : string.Empty), true);
            response.SetElementContents("ar-mbody", Detail(c, saved, reg));
            response.ExecuteScript("AdminRegistrationsJs.list(1);AdminRegistrationsJs.toast('Cancelled');");
            return response;
        }

        public async Task<ApiResponse> AddForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "register form", "new", "You can look but not register people.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime now = Store.Now(site);
            string q = Ui.Clip(GetDataValue("amem"), 120);
            global::Sport.Models.ClubMember? m = q == string.Empty ? null : site.Members.FirstOrDefault(x => x.Type == "member" && (x.Id == q || x.Email.Equals(q, StringComparison.OrdinalIgnoreCase) || (Rules.FullName(x) + " · " + x.Email).Equals(q, StringComparison.OrdinalIgnoreCase)));
            StringBuilder sb = new StringBuilder("<h2>Register someone</h2><div class=\"ar-form\"><label class=\"ar-field\"><span>Member account</span><input type=\"text\" id=\"ar-amem\" list=\"ar-amlist\" maxlength=\"120\" value=\"" + Ui.Attr(m != null ? Rules.FullName(m) + " · " + m.Email : q) + "\" placeholder=\"Start typing a name or email\" onchange=\"AdminRegistrationsJs.addMember()\"></label>" + Ui.DataList("ar-amlist", site.Members.Where(x => x.Type == "member" && x.Status == "active").Select(x => Rules.FullName(x) + " · " + x.Email)) + "<p class=\"ar-hint\" style=\"margin-top:-6px\">Day-pass visitors need to sign up first; class registration is for members.</p>");
            if (q != string.Empty && m == null)
            {
                sb.Append("<div class=\"ar-warn-box\">No member account matches. Choose one from the list.</div>");
            }
            if (m != null)
            {
                if (m.Status == "suspended")
                {
                    sb.Append("<div class=\"ar-warn-box\">This account is suspended.</div>");
                }
                else
                {
                    sb.Append("<input type=\"hidden\" id=\"ar-amemid\" value=\"" + Ui.Attr(m.Id) + "\"><div><span class=\"ar-hint\">Who is taking the class?</span><div class=\"ar-plst\">");
                    List<(string Id, string Name, DateTime? Born)> people = new List<(string, string, DateTime?)> { ("self", Rules.FullName(m), m.Born) };
                    people.AddRange(m.Family.Select(f => (f.Id, (f.First + " " + f.Last).Trim(), (DateTime?)f.Born)));
                    bool first = true;
                    foreach ((string id, string name, DateTime? born) in people)
                    {
                        sb.Append("<label><input type=\"radio\" name=\"ar-aperson\" value=\"" + Ui.Attr(id) + "\"" + (first ? " checked" : string.Empty) + "> <span><b>" + Ui.Enc(name) + "</b> <small>" + (born != null ? "age " + Rules.Age(born.Value, now.Date) : "no date of birth") + "</small></span></label>");
                        first = false;
                    }
                    sb.Append("</div></div><label class=\"ar-field\"><span>Class</span><select id=\"ar-aclass\">" + Ui.Opts(site.Classes.Where(k => Rules.State(site, k, now) is "open" or "few" or "full" or "draft" && k.Status == "active").OrderBy(k => k.Title).Select(k => (k.Id, k.Title + " · " + Rules.Taken(site, k) + "/" + k.Capacity + " · " + Rules.StateName(Rules.State(site, k, now)))), string.Empty) + "</select></label><label class=\"ar-field\"><span>Payment</span><select id=\"ar-apay\">" + Ui.Opts(new[] { ("paid", "Paid at the desk now"), ("hold", "Hold the seat, pay at the desk later"), ("wait", "Put on the waitlist") }, "paid") + "</select></label>" + (Access.Can(c.U, AreaKey, 'X') ? "<label class=\"ar-chk\"><input type=\"checkbox\" id=\"ar-aover\"> Override the seat limit, age range and registration date</label>" : string.Empty) + "<p class=\"ar-hint\">Uses the member's tier discount. Points are not used at the desk.</p>");
                }
            }
            sb.Append("<em class=\"ar-err\" id=\"ar-e-add\" role=\"alert\"></em></div><div class=\"ar-mfoot\"><button type=\"button\" class=\"ar-btn ar-btn-ghost\" onclick=\"AdminRegistrationsJs.closeModal()\">Cancel</button>" + (m != null && m.Status == "active" ? "<button type=\"button\" class=\"ar-btn ar-btn-accent\" onclick=\"AdminRegistrationsJs.add()\">Register</button>" : string.Empty) + "</div>");
            response.SetElementContents("ar-mbody", sb.ToString());
            response.ExecuteScript("AdminRegistrationsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Add()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRegistrationsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "register", GetDataValue("aclass"), "You can look but not register people.");
            }
            bool over = GetDataValue("aover") == "1";
            if (over && !Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "override", GetDataValue("aclass"), "Overrides need the Override limits right.");
            }
            string pay = GetDataValue("apay") is "hold" or "wait" ? GetDataValue("apay") : "paid";
            string err = string.Empty;
            Registration? made = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                DateTime now = Store.Now(site);
                DateTime nowU = DateTime.UtcNow;
                global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == GetDataValue("amem") && x.Type == "member" && x.Status == "active");
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == GetDataValue("aclass") && x.Status == "active");
                string person = Ui.Clip(GetDataValue("aperson"), 20);
                string name = string.Empty;
                DateTime? born = null;
                if (m != null && person == "self")
                {
                    name = Rules.FullName(m);
                    born = m.Born;
                }
                else if (m != null)
                {
                    FamilyMember? f = m.Family.FirstOrDefault(x => x.Id == person);
                    if (f != null)
                    {
                        name = (f.First + " " + f.Last).Trim();
                        born = f.Born;
                    }
                }
                Gate g = k != null && m != null ? Rules.CanJoin(site, k, m, now) : new Gate();
                if (m == null || k == null || name == string.Empty)
                {
                    err = "Choose a member, a participant and a class.";
                }
                else if (site.Registrations.Any(r => r.Class == k.Id && r.Member == m.Id && r.Person == person && r.Status is "confirmed" or "pending" or "offered" or "waitlist"))
                {
                    err = name + " is already registered for this class.";
                }
                else if (!over && !g.Ok)
                {
                    err = g.Why;
                }
                else if (!over && (born == null || !Rules.AgeFits(k, born.Value)))
                {
                    err = born == null ? "Add a date of birth for " + name + " first." : name + " is outside the class age range.";
                }
                else if (!over && pay != "wait" && Rules.Left(site, k) <= 0)
                {
                    err = "The class is full. Put them on the waitlist, or override.";
                }
                else if (now >= Rules.End(k))
                {
                    err = "This class has finished.";
                }
                if (err == string.Empty && m != null && k != null)
                {
                    Quote q = Rules.Price(site, k, m, 0);
                    Registration r = new Registration { Id = Store.NextId(site.Registrations.Select(x => x.Id), "R-", 1000), Code = Rules.NewCode(site), Class = k.Id, Member = m.Id, Person = person, Name = name, Born = born, Price = q.Price, Discount = q.Discount, Tier = q.Tier, Total = q.Total, Waiver = true, Created = nowU, By = c.U.Id };
                    site.Registrations.Add(r);
                    if (pay == "wait")
                    {
                        r.Status = "waitlist";
                        r.Total = 0;
                        r.Discount = 0;
                    }
                    else if (pay == "hold")
                    {
                        r.Status = "pending";
                        r.Method = "desk";
                        DateTime hold = nowU.AddHours(Math.Max(1, site.Settings.DeskHours));
                        DateTime startU = Store.Utc(site, Rules.Start(k));
                        r.HoldExp = hold > startU && startU > nowU ? startU : hold;
                    }
                    else
                    {
                        Rules.Confirm(site, r, "desk", string.Empty, string.Empty, c.U.Id, nowU);
                    }
                    made = r;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (made == null)
            {
                response.SetElementContents("ar-e-add", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "register", made.Class + " · " + made.Name + " · " + pay + (over ? " · override" : string.Empty), true);
            response.ExecuteScript("AdminRegistrationsJs.list(1);AdminRegistrationsJs.open('" + Ui.Js(made.Id) + "');AdminRegistrationsJs.toast('Registered');");
            return response;
        }
    }
}
