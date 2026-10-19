using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminPayments : WebPage
    {
        private const string AreaKey = "payments";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Payments & refunds");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ap-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ap-menu", Auth.Menu("ap", "AdminPayments", c, site));
            response.SetElementContents("ap-who", Auth.Who("ap", c));
            response.SetElementContents("ap-demo", Auth.DemoPill("ap", c));
            response.SetElementContents("ap-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminPaymentsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ap-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ap-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminPaymentsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ap-main", NoAccess("Payments & refunds"));
                return response;
            }
            response.SetElementContents("ap-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminPaymentsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ap-tr ap-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 30;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            int open = site.Registrations.Count(r => r.Ask != null && r.Ask.Status == "open");
            return "<div class=\"ap-ptop\"><div><h1>Payments &amp; refunds</h1><p>Every charge and refund, refund requests from members, and a CSV export for the books. All card payments in this template are demo payments.</p></div></div><div class=\"ap-tabs\"><button type=\"button\" class=\"ap-tab ap-act\" data-t=\"pay\" onclick=\"AdminPaymentsJs.show('pay')\">Payments</button><button type=\"button\" class=\"ap-tab\" data-t=\"asks\" onclick=\"AdminPaymentsJs.show('asks')\">Refund requests" + (open > 0 ? "<em>" + open + "</em>" : string.Empty) + "</button></div><div id=\"ap-paybox\"><div class=\"ap-bar\"><label>Search<input type=\"search\" id=\"ap-q\" placeholder=\"Name, code or payment ID\" maxlength=\"60\" oninput=\"AdminPaymentsJs.search()\"></label><label>Type<select id=\"ap-pkind\" onchange=\"AdminPaymentsJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Charges and refunds"), ("charge", "Charges"), ("refund", "Refunds"), ("pass", "Day passes") }, string.Empty) + "</select></label><label>Method<select id=\"ap-pmeth\" onchange=\"AdminPaymentsJs.list(1)\">" + Ui.Opts(new[] { (string.Empty, "Any"), ("card", "Card"), ("desk", "Front desk") }, string.Empty) + "</select></label><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminPaymentsJs.csv()\">Export CSV</button></div><div class=\"ap-bar\" id=\"ap-perbar\">" + Ui.PeriodBar("ap", "AdminPaymentsJs.per", "30", string.Empty, string.Empty) + "</div><div class=\"ap-totals\" id=\"ap-totals\"></div><div class=\"ap-tbl ap-pytbl\" id=\"ap-list\"></div><div id=\"ap-pager\"></div></div><div id=\"ap-askbox\" hidden><div class=\"ap-bar\"><label>Show<select id=\"ap-astat\" onchange=\"AdminPaymentsJs.list(1)\">" + Ui.Opts(new[] { ("open", "Waiting for a decision"), ("approved", "Approved"), ("declined", "Declined"), ("all", "All requests") }, "open") + "</select></label></div><div class=\"ap-tbl ap-aktbl\" id=\"ap-asks\"></div></div>";
        }

        private (List<Payment> List, string Per) Filter(SiteData site)
        {
            string term = Ui.Clip(GetDataValue("term"), 60);
            string kind = Ui.Clip(GetDataValue("pkind"), 10);
            string meth = Ui.Clip(GetDataValue("pmeth"), 10);
            string per = GetDataValue("per") == string.Empty ? "30" : Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(Store.Today(site), per, GetDataValue("pfrom"), GetDataValue("pto"));
            Dictionary<string, global::Sport.Models.ClubMember> mem = site.Members.ToDictionary(m => m.Id);
            Dictionary<string, Registration> regs = site.Registrations.ToDictionary(r => r.Id);
            List<Payment> list = site.Payments.Where(p => p.Status == "paid" && (kind == string.Empty || (kind == "pass" ? p.Pass != string.Empty : p.Kind == kind)) && (meth == string.Empty || p.Method == meth) && Ui.InPeriod(Store.Local(site, p.When), from, to) && (term == string.Empty || p.Id.Equals(term, StringComparison.OrdinalIgnoreCase) || (mem.TryGetValue(p.Member, out global::Sport.Models.ClubMember? m) && (Rules.FullName(m).Contains(term, StringComparison.OrdinalIgnoreCase) || m.Email.Contains(term, StringComparison.OrdinalIgnoreCase))) || (regs.TryGetValue(p.Reg, out Registration? r) && (r.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Name.Contains(term, StringComparison.OrdinalIgnoreCase))))).OrderByDescending(p => p.When).ToList();
            return (list, per);
        }

        private static string What(SiteData site, Payment p)
        {
            if (p.Pass != string.Empty)
            {
                DayPass? d = site.Passes.FirstOrDefault(x => x.Id == p.Pass);
                return Ui.Enc(site.Settings.PassName) + (d != null ? " &middot; " + Ui.Short(d.Day) + " &middot; " + Ui.Enc(d.Code) : string.Empty);
            }
            Registration? r = site.Registrations.FirstOrDefault(x => x.Id == p.Reg);
            SportClass? k = r != null ? site.Classes.FirstOrDefault(x => x.Id == r.Class) : null;
            return Ui.Enc(k?.Title ?? p.Reg) + (r != null ? " &middot; " + Ui.Enc(r.Name) + " &middot; " + Ui.Enc(r.Code) : string.Empty);
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            (List<Payment> all, string per) = Filter(site);
            Dictionary<string, global::Sport.Models.ClubMember> mem = site.Members.ToDictionary(m => m.Id);
            decimal inn = all.Where(p => p.Kind == "charge").Sum(p => p.Amount);
            decimal outt = all.Where(p => p.Kind == "refund").Sum(p => p.Amount);
            response.SetElementContents("ap-totals", "<div><small>Charges</small><b>" + Ui.Money(inn, true) + "</b></div><div><small>Refunds</small><b class=\"ap-neg\">&minus;" + Ui.Money(outt, true) + "</b></div><div><small>Net</small><b>" + Ui.Money(inn - outt, true) + "</b></div><div><small>Payments</small><b>" + Ui.Num(all.Count) + "</b></div>");
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("When", string.Empty), ("Type", string.Empty), ("For", string.Empty), ("Member", string.Empty), ("Method", string.Empty), ("Amount", "ap-num"), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"ap-tr\"><span class=\"ap-none ap-wide\">No payments in this period.</span></div>");
            }
            foreach (Payment p in all.Skip((page - 1) * Size).Take(Size))
            {
                DateTime w = Store.Local(site, p.When);
                sb.Append("<div class=\"ap-tr\"><span data-l=\"When\">" + w.ToString("MMM d", Ui.Inv) + ", " + Ui.Hm(site, w) + "</span><span data-l=\"Type\">" + (p.Kind == "refund" ? "<span class=\"ap-st ap-st-refund\">Refund</span>" : "<span class=\"ap-st ap-st-paid\">Charge</span>") + "</span><span data-l=\"For\" class=\"ap-wide\">" + What(site, p) + "</span><span data-l=\"Member\">" + Ui.Enc(mem.TryGetValue(p.Member, out global::Sport.Models.ClubMember? m) ? Rules.FullName(m) : p.Member) + "</span><span data-l=\"Method\">" + (p.Method == "card" ? Ui.Enc(p.Brand) + " &bull;&bull;" + Ui.Enc(p.Last4) : "Front desk") + "</span><span data-l=\"Amount\" class=\"ap-num" + (p.Kind == "refund" ? " ap-neg" : string.Empty) + "\">" + (p.Kind == "refund" ? "&minus;" : string.Empty) + Ui.Money(p.Amount, true) + "</span><span><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminPaymentsJs.pay('" + Ui.Attr(p.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("ap-list", sb.ToString());
            response.SetElementContents("ap-pager", Ui.Pager("ap", page, Size, all.Count, "payment", "payments", "AdminPaymentsJs.list"));
            response.SetElementContents("ap-perbar", Ui.PeriodBar("ap", "AdminPaymentsJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        private static string PayDetail(Ctx c, SiteData site, Payment p, Dictionary<string, Staff> staff)
        {
            global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == p.Member);
            bool canX = Access.Can(c.U, AreaKey, 'X');
            StringBuilder sb = new StringBuilder("<h2>" + (p.Kind == "refund" ? "Refund " : "Payment ") + Ui.Enc(p.Id) + "</h2><dl class=\"ap-dl\"><dt>Amount</dt><dd><b>" + (p.Kind == "refund" ? "&minus;" : string.Empty) + Ui.Money(p.Amount, true) + "</b></dd><dt>For</dt><dd>" + What(site, p) + "</dd><dt>Member</dt><dd>" + (m != null ? "<a href=\"AdminMembers?id=" + Uri.EscapeDataString(m.Id) + "\">" + Ui.Enc(Rules.FullName(m)) + "</a> &middot; " + Ui.Enc(m.Email) : Ui.Enc(p.Member)) + "</dd><dt>Method</dt><dd>" + (p.Method == "card" ? Ui.Enc(p.Brand) + " ending " + Ui.Enc(p.Last4) + " (demo)" : "Front desk") + "</dd><dt>When</dt><dd>" + Ui.Time(p.When, "full") + "</dd><dt>By</dt><dd>" + Ui.Enc(Who(staff, p.By)) + "</dd>" + (p.Note != string.Empty ? "<dt>Note</dt><dd>" + Ui.Enc(p.Note) + "</dd>" : string.Empty) + (p.Of != string.Empty ? "<dt>Refund of</dt><dd>" + Ui.Enc(p.Of) + "</dd>" : string.Empty) + "</dl>");
            Registration? r = site.Registrations.FirstOrDefault(x => x.Id == p.Reg);
            DayPass? d = site.Passes.FirstOrDefault(x => x.Id == p.Pass);
            if (r != null)
            {
                sb.Append("<div style=\"margin-top:14px\">" + Rules.Receipt(site, r, "ap") + "</div><div style=\"margin-top:8px;display:flex;gap:8px;flex-wrap:wrap\"><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminPaymentsJs.printBox('ap-rcpt-" + Ui.Attr(r.Code) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M7 9V3h10v6M7 17H4v-7h16v7h-3\"/><path d=\"M7 14h10v7H7z\"/></svg> Print receipt</button><a class=\"ap-btn ap-btn-sm ap-btn-ghost\" href=\"AdminRegistrations?id=" + Uri.EscapeDataString(r.Id) + "\">Open registration</a></div>");
            }
            else if (d != null)
            {
                sb.Append("<div style=\"margin-top:14px\">" + Rules.PassReceipt(site, d, "ap") + "</div>");
            }
            decimal left = p.Kind == "charge" ? (r != null ? Rules.Refundable(r) : d != null && d.Status != "refunded" ? p.Amount - site.Payments.Where(x => x.Of == p.Id && x.Kind == "refund").Sum(x => x.Amount) : 0m) : 0m;
            if (p.Kind == "charge" && left > 0)
            {
                if (canX)
                {
                    sb.Append("<div class=\"ap-dec\"><h3>Refund</h3><p class=\"ap-hint\">Up to " + Ui.Money(left, true) + " can still be refunded. " + (r != null ? "This keeps the registration; to take someone off the class, cancel it in Registrations." : "The day pass is marked refunded.") + " Points the payment earned are taken back.</p><div class=\"ap-f2\"><label class=\"ap-field\"><span>Amount ($)</span><input type=\"text\" inputmode=\"decimal\" id=\"ap-famt\" maxlength=\"10\" value=\"" + left.ToString("0.00", Ui.Inv) + "\"></label><label class=\"ap-field\"><span>Reason (required)</span><input type=\"text\" id=\"ap-fwhy\" maxlength=\"200\" placeholder=\"Goodwill: missed two sessions\"></label></div><em class=\"ap-err\" id=\"ap-e-ref\" role=\"alert\"></em><div><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-danger\" onclick=\"AdminPaymentsJs.refund('" + Ui.Attr(p.Id) + "')\">Refund</button></div></div>");
                }
                else
                {
                    sb.Append("<p class=\"ap-hint\" style=\"margin-top:12px\">Refunds need the Refund right.</p>");
                }
            }
            sb.Append("<div class=\"ap-mfoot\"><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminPaymentsJs.closeModal()\">Close</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "payment", GetDataValue("pid"), "You don't have access to payments.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Payment? p = site.Payments.FirstOrDefault(x => x.Id == GetDataValue("pid"));
            if (p == null)
            {
                return response;
            }
            response.SetElementContents("ap-mbody", PayDetail(c, site, p, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminPaymentsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Refund()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "refund", GetDataValue("pid"), "Refunds need the Refund right.");
            }
            decimal amt = decimal.TryParse(GetDataValue("famt").Replace("$", string.Empty).Trim(), NumberStyles.Number, Ui.Inv, out decimal a) ? Math.Round(a, 2) : 0m;
            string why = Ui.OneLine(GetDataValue("fwhy"), 200);
            if (amt <= 0)
            {
                response.SetElementContents("ap-e-ref", "Enter an amount above zero.");
                return response;
            }
            if (why.Length < 3)
            {
                response.SetElementContents("ap-e-ref", "Write the reason for the refund.");
                return response;
            }
            string err = string.Empty;
            SiteData? saved = null;
            Payment? pay = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Payment? p = site.Payments.FirstOrDefault(x => x.Id == GetDataValue("pid") && x.Kind == "charge" && x.Status == "paid");
                DateTime nowU = DateTime.UtcNow;
                if (p == null)
                {
                    err = "That payment can't be refunded.";
                }
                else if (p.Reg != string.Empty)
                {
                    Registration? r = site.Registrations.FirstOrDefault(x => x.Id == p.Reg);
                    if (r == null || amt > Rules.Refundable(r))
                    {
                        err = "The refund can't be more than " + Ui.Plain(r != null ? Rules.Refundable(r) : 0m) + ".";
                    }
                    else
                    {
                        Rules.Refund(site, r, amt, c.U.Id, nowU, why);
                    }
                }
                else
                {
                    DayPass? d = site.Passes.FirstOrDefault(x => x.Id == p.Pass);
                    decimal left = p.Amount - site.Payments.Where(x => x.Of == p.Id && x.Kind == "refund").Sum(x => x.Amount);
                    if (d == null || amt > left)
                    {
                        err = "The refund can't be more than " + Ui.Plain(left) + ".";
                    }
                    else
                    {
                        site.Payments.Add(new Payment { Id = Store.NextId(site.Payments.Select(x => x.Id), "P-", 5000), Kind = "refund", Pass = d.Id, Member = p.Member, Amount = amt, Method = p.Method, Brand = p.Brand, Last4 = p.Last4, Status = "paid", When = nowU, By = c.U.Id, Note = why, Of = p.Id });
                        d.Status = "refunded";
                        global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == p.Member);
                        if (m != null && m.Type == "member")
                        {
                            Rules.AddPoints(site, m, -Rules.EarnFor(site.Settings, amt), "refund", string.Empty, "Refund " + d.Code, c.U.Id, nowU);
                        }
                    }
                }
                if (err == string.Empty && p != null)
                {
                    await Store.SaveSite(site, c.Sbx);
                    saved = site;
                    pay = p;
                }
            }
            if (err != string.Empty || saved == null || pay == null)
            {
                response.SetElementContents("ap-e-ref", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "refund", pay.Id + " · " + Ui.Plain(amt) + " · " + why, true);
            response.SetElementContents("ap-mbody", PayDetail(c, saved, pay, Ui.StaffMap(await Store.LoadStaff(c.Sbx))));
            response.ExecuteScript("AdminPaymentsJs.list(1);AdminPaymentsJs.toast('Refunded " + Ui.Js(Ui.Plain(amt)) + "');");
            return response;
        }

        public async Task<ApiResponse> Asks()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string st = GetDataValue("astat") is "approved" or "declined" or "all" ? GetDataValue("astat") : "open";
            List<Registration> list = site.Registrations.Where(r => r.Ask != null && (st == "all" || r.Ask.Status == st)).OrderByDescending(r => r.Ask!.When).ToList();
            StringBuilder sb = new StringBuilder(Head(new[] { ("Asked", string.Empty), ("Participant", string.Empty), ("Reason", string.Empty), ("Policy amount", "ap-num"), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (list.Count == 0)
            {
                sb.Append("<div class=\"ap-tr\"><span class=\"ap-none ap-wide\">" + (st == "open" ? "No requests waiting. All clear." : "Nothing here.") + "</span></div>");
            }
            foreach (Registration r in list)
            {
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
                sb.Append("<div class=\"ap-tr\"><span data-l=\"Asked\">" + Ui.Day(Store.Local(site, r.Ask!.When), false) + "</span><span class=\"ap-wide\"><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(k?.Title ?? r.Class) + " &middot; " + Ui.Enc(r.Code) + "</small></span><span data-l=\"Reason\" class=\"ap-reason ap-wide\">" + Ui.Enc(r.Ask.Reason) + "</span><span data-l=\"Policy amount\" class=\"ap-num\">" + Ui.Money(r.Ask.Amount, true) + "</span><span data-l=\"Status\"><span class=\"ap-st ap-st-" + (r.Ask.Status == "open" ? "pending\">Waiting" : r.Ask.Status == "approved" ? "approved\">Approved" : "declined\">Declined") + "</span></span><span><button type=\"button\" class=\"ap-btn ap-btn-sm " + (r.Ask.Status == "open" ? "ap-btn-accent" : "ap-btn-ghost") + "\" onclick=\"AdminPaymentsJs.askOpen('" + Ui.Attr(r.Id) + "')\">" + (r.Ask.Status == "open" ? "Decide" : "Open") + "</button></span></div>");
            }
            response.SetElementContents("ap-asks", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> AskOpen()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "refund request", GetDataValue("rid"), "You don't have access to payments.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Ask != null);
            if (r == null)
            {
                return response;
            }
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            SportClass? k = site.Classes.FirstOrDefault(x => x.Id == r.Class);
            global::Sport.Models.ClubMember? m = site.Members.FirstOrDefault(x => x.Id == r.Member);
            bool canX = Access.Can(c.U, AreaKey, 'X');
            DateTime now = Store.Now(site);
            StringBuilder sb = new StringBuilder("<h2>Refund request</h2><dl class=\"ap-dl\"><dt>Participant</dt><dd>" + Ui.Enc(r.Name) + "</dd><dt>Account</dt><dd>" + (m != null ? "<a href=\"AdminMembers?id=" + Uri.EscapeDataString(m.Id) + "\">" + Ui.Enc(Rules.FullName(m)) + "</a> &middot; " + Ui.Enc(m.Email) : string.Empty) + "</dd><dt>Class</dt><dd>" + Ui.Enc(k?.Title ?? r.Class) + (k != null ? " &middot; " + Ui.Range(k) : string.Empty) + "</dd><dt>Asked</dt><dd>" + Ui.Time(r.Ask!.When, "full") + "</dd><dt>Reason</dt><dd>" + Ui.Enc(r.Ask.Reason) + "</dd><dt>Paid</dt><dd>" + Ui.Money(r.Paid, true) + (r.Refunded > 0 ? ", " + Ui.Money(r.Refunded, true) + " already refunded" : string.Empty) + "</dd>");
            if (k != null && r.Ask.Status == "open")
            {
                (int pct, bool self, string rule) = Rules.Policy(site, k, now);
                sb.Append("<dt>Policy now</dt><dd>" + Ui.Enc(rule) + "</dd>");
            }
            if (r.Ask.Status != "open")
            {
                sb.Append("<dt>Decision</dt><dd>" + (r.Ask.Status == "approved" ? "Approved, " + Ui.Money(r.Ask.Amount, true) : "Declined") + " by " + Ui.Enc(Who(staff, r.Ask.By)) + (r.Ask.Decided != null ? " &middot; " + Ui.Time(r.Ask.Decided.Value, "full") : string.Empty) + (r.Ask.Note != string.Empty ? "<br><i>" + Ui.Enc(r.Ask.Note) + "</i>" : string.Empty) + "</dd>");
            }
            sb.Append("</dl>");
            if (r.Ask.Status == "open")
            {
                if (canX)
                {
                    sb.Append("<div class=\"ap-dec\"><h3>Decide</h3><div class=\"ap-f2\"><label class=\"ap-field\"><span>Refund ($)</span><input type=\"text\" inputmode=\"decimal\" id=\"ap-damt\" maxlength=\"10\" value=\"" + r.Ask.Amount.ToString("0.00", Ui.Inv) + "\"></label><label class=\"ap-field\"><span>Note to the member</span><input type=\"text\" id=\"ap-dnote\" maxlength=\"200\" placeholder=\"Sorry to see you go. Refund on its way.\"></label></div><label class=\"ap-chk\"><input type=\"checkbox\" id=\"ap-dkeep\"> Keep them in the class (partial goodwill refund)</label><em class=\"ap-err\" id=\"ap-e-dec\" role=\"alert\"></em><div style=\"display:flex;gap:8px;flex-wrap:wrap\"><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-accent\" onclick=\"AdminPaymentsJs.decide('" + Ui.Attr(r.Id) + "',true)\">Approve and refund</button><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-danger\" onclick=\"AdminPaymentsJs.decide('" + Ui.Attr(r.Id) + "',false)\">Decline</button></div><p class=\"ap-hint\">Approving without Keep takes them off the class, and the seat goes to the waitlist.</p></div>");
                }
                else
                {
                    sb.Append("<p class=\"ap-hint\" style=\"margin-top:12px\">Deciding needs the Refund right.</p>");
                }
            }
            sb.Append("<div class=\"ap-mfoot\"><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminPaymentsJs.closeModal()\">Close</button></div>");
            response.SetElementContents("ap-mbody", sb.ToString());
            response.ExecuteScript("AdminPaymentsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Decide()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "refund decision", GetDataValue("rid"), "Deciding refunds needs the Refund right.");
            }
            bool ok = GetDataValue("dok") == "1";
            bool keep = GetDataValue("dkeep") == "1";
            decimal amt = decimal.TryParse(GetDataValue("damt").Replace("$", string.Empty).Trim(), NumberStyles.Number, Ui.Inv, out decimal a) ? Math.Round(a, 2) : -1m;
            string note = Ui.OneLine(GetDataValue("dnote"), 200);
            if (ok && amt < 0)
            {
                response.SetElementContents("ap-e-dec", "Enter the refund as a number.");
                return response;
            }
            if (!ok && note.Length < 3)
            {
                response.SetElementContents("ap-e-dec", "Tell the member why in the note.");
                return response;
            }
            string err = string.Empty;
            string target = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Registration? r = site.Registrations.FirstOrDefault(x => x.Id == GetDataValue("rid") && x.Ask != null && x.Ask.Status == "open");
                DateTime nowU = DateTime.UtcNow;
                if (r == null)
                {
                    err = "This request was already decided.";
                }
                else if (ok && amt > Rules.Refundable(r))
                {
                    err = "The refund can't be more than " + Ui.Plain(Rules.Refundable(r)) + ".";
                }
                else
                {
                    r.Ask!.Status = ok ? "approved" : "declined";
                    r.Ask.Decided = nowU;
                    r.Ask.By = c.U.Id;
                    r.Ask.Note = note;
                    if (ok)
                    {
                        r.Ask.Amount = amt;
                        if (keep || r.Status != "confirmed")
                        {
                            Rules.Refund(site, r, amt, c.U.Id, nowU, "Refund request approved" + (note != string.Empty ? ": " + note : string.Empty));
                        }
                        else
                        {
                            Rules.EndReg(site, r, "cancelled", amt, c.U.Id, nowU, "Refund request approved. " + Ui.Plain(amt) + " refunded." + (note != string.Empty ? " " + note : string.Empty));
                        }
                    }
                    target = r.Id + " · " + (ok ? "approved " + Ui.Plain(amt) + (keep ? " · kept in class" : string.Empty) : "declined");
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("ap-e-dec", Ui.Enc(err));
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "refund request", target, true);
            response.ExecuteScript("AdminPaymentsJs.closeModal();AdminPaymentsJs.list(1);AdminPaymentsJs.toast('" + (ok ? "Approved and refunded" : "Declined") + "');");
            return response;
        }

        public async Task<ApiResponse> Csv()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "export", "payments", "You don't have access to payments.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            (List<Payment> all, string per) = Filter(site);
            Dictionary<string, global::Sport.Models.ClubMember> mem = site.Members.ToDictionary(m => m.Id);
            StringBuilder sb = new StringBuilder("Date,Time,Payment,Type,Amount,Method,Card,Member,Email,For,Code,Note\r\n");
            foreach (Payment p in all)
            {
                DateTime w = Store.Local(site, p.When);
                Registration? r = site.Registrations.FirstOrDefault(x => x.Id == p.Reg);
                DayPass? d = site.Passes.FirstOrDefault(x => x.Id == p.Pass);
                SportClass? k = r != null ? site.Classes.FirstOrDefault(x => x.Id == r.Class) : null;
                mem.TryGetValue(p.Member, out global::Sport.Models.ClubMember? m);
                string[] row = { Ui.Ymd(w), w.ToString("HH:mm", Ui.Inv), p.Id, p.Kind, ((p.Kind == "refund" ? -1 : 1) * p.Amount).ToString("0.00", Ui.Inv), p.Method, p.Method == "card" ? p.Brand + " " + p.Last4 : string.Empty, m != null ? Rules.FullName(m) : p.Member, m?.Email ?? string.Empty, d != null ? site.Settings.PassName + " " + Ui.Ymd(d.Day) : (k?.Title ?? p.Reg) + (r != null ? " - " + r.Name : string.Empty), r?.Code ?? d?.Code ?? string.Empty, p.Note };
                sb.Append(string.Join(",", row.Select(Ui.Csv)) + "\r\n");
            }
            await Store.Log(c.Sbx, c.U.Id, "export", "payments CSV · " + all.Count + " rows", true);
            string b64 = Convert.ToBase64String(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray());
            response.ExecuteScript("AdminPaymentsJs.download('payments-" + Ui.Ymd(Store.Today(site)) + ".csv','" + b64 + "');");
            return response;
        }
    }
}
