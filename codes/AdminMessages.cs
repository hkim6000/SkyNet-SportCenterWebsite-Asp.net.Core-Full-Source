using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminMessages : WebPage
    {
        private const string AreaKey = "messages";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Messages");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--xg-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("xg-menu", Auth.Menu("xg", "AdminMessages", c, site));
            response.SetElementContents("xg-who", Auth.Who("xg", c));
            response.SetElementContents("xg-demo", Auth.DemoPill("xg", c));
            response.SetElementContents("xg-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminMessagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"xg-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"xg-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminMessagesJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("xg-main", NoAccess("Messages"));
                return response;
            }
            response.SetElementContents("xg-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminMessagesJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"xg-tr xg-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private const int Size = 20;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            string st = Ui.MsgStatuses.Any(s => s.Key == arg) ? arg : string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"xg-ptop\"><div><h1>Messages</h1><p>Messages from the contact form. Reply by email, then mark them replied or closed.</p></div></div>");
            sb.Append("<div class=\"xg-bar\"><label>Search<input type=\"search\" id=\"xg-q\" list=\"xg-qlist\" placeholder=\"Name, email or words\" maxlength=\"60\" oninput=\"AdminMessagesJs.search()\"></label>" + Ui.DataList("xg-qlist", site.Messages.Select(m => m.Name)));
            sb.Append("<label>Status<select id=\"xg-mst\" onchange=\"AdminMessagesJs.list(1)\"><option value=\"\">Open</option>" + Ui.Opts(Ui.MsgStatuses, st) + "<option value=\"all\">Everything</option></select></label>");
            sb.Append("<label>Topic<select id=\"xg-mtopic\" onchange=\"AdminMessagesJs.list(1)\"><option value=\"\">Any topic</option>" + Ui.Opts(Ui.Topics, string.Empty) + "</select></label></div>");
            sb.Append("<div class=\"xg-bar\" id=\"xg-perbar\">" + Ui.PeriodBar("xg", "AdminMessagesJs.per", "all", string.Empty, string.Empty) + "</div><div class=\"xg-tbl xg-mstbl\" id=\"xg-list\"></div><div id=\"xg-pager\"></div>");
            return sb.ToString();
        }

        private static string NotesHtml(List<Note> notes, Dictionary<string, Staff> staff, bool canE)
        {
            StringBuilder sb = new StringBuilder("<h3>Notes</h3><div class=\"xg-notes\">");
            if (notes.Count == 0)
            {
                sb.Append("<p class=\"xg-hint\">No notes yet.</p>");
            }
            foreach (Note n in notes.OrderByDescending(n => n.When))
            {
                sb.Append("<div class=\"xg-note\">" + Ui.Enc(n.Text) + "<small>" + Ui.Enc(Ui.StaffName(staff, n.By)) + " &middot; " + Ui.Time(n.When) + "</small></div>");
            }
            sb.Append("</div>");
            if (canE)
            {
                sb.Append("<div class=\"xg-form\" style=\"margin-top:10px\"><label class=\"xg-field\"><span>Add a note</span><textarea id=\"xg-enote\" rows=\"2\" maxlength=\"500\" placeholder=\"Called back, booked a trial swim.\"></textarea></label><div><button type=\"button\" class=\"xg-btn xg-btn-sm\" onclick=\"AdminMessagesJs.note()\">Save note</button></div></div>");
            }
            return sb.ToString();
        }

        private static string MemberLink(SiteData site, Message m)
        {
            global::Sport.Models.ClubMember? x = site.Members.FirstOrDefault(p => p.Email.Equals(m.Email, StringComparison.OrdinalIgnoreCase));
            return x == null ? string.Empty : "<dt>Record</dt><dd><a href=\"AdminMembers?id=" + Uri.EscapeDataString(x.Id) + "\">" + Ui.Enc(Rules.FullName(x)) + "</a> &middot; " + (x.Type == "member" ? Rules.TierName(Rules.TierOf(site.Settings, x.Lifetime)) + " member" : "Open member") + "</dd>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string term = Ui.Clip(GetDataValue("mq"), 60);
            string st = GetDataValue("mst");
            string topic = GetDataValue("mtopic");
            string per = Per(GetDataValue("per"));
            (DateTime? from, DateTime? to) = Ui.Period(Store.Today(site), per, GetDataValue("pfrom"), GetDataValue("pto"));
            IEnumerable<Message> q = site.Messages.Where(m => (st == "all" || (st == string.Empty ? m.Status != "closed" : m.Status == st)) && (topic == string.Empty || m.Topic == topic) && Ui.InPeriod(Store.Local(site, m.Created), from, to));
            if (term != string.Empty)
            {
                q = q.Where(m => (m.Name + " " + m.Email + " " + m.Phone + " " + m.Text).Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            List<Message> all = q.OrderByDescending(m => m.Created).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("From", string.Empty), ("Topic", string.Empty), ("Message", string.Empty), ("Received", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"xg-tr\"><span class=\"xg-none xg-wide\">No messages match.</span></div>");
            }
            foreach (Message m in all.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<div class=\"xg-tr" + (m.Status == "new" ? " xg-fresh" : string.Empty) + "\"><span class=\"xg-wide\"><b>" + Ui.Enc(m.Name) + "</b><small>" + Ui.Enc(m.Email) + "</small></span><span data-l=\"Topic\">" + Ui.Enc(Ui.NameOf(Ui.Topics, m.Topic)) + "</span><span data-l=\"Message\" class=\"xg-wide\"><span class=\"xg-ex\">" + Ui.Enc(m.Text) + "</span></span><span data-l=\"Received\">" + Ui.Time(m.Created) + "</span><span data-l=\"Status\">" + Ui.Pill("xg", Ui.MsgStatuses, m.Status) + "</span><span><button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"AdminMessagesJs.open('" + Ui.Attr(m.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("xg-list", sb.ToString());
            response.SetElementContents("xg-pager", Ui.Pager("xg", page, Size, all.Count, "message", "messages", "AdminMessagesJs.list"));
            response.SetElementContents("xg-perbar", Ui.PeriodBar("xg", "AdminMessagesJs.per", per, GetDataValue("pfrom"), GetDataValue("pto")));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "message", GetDataValue("mid"), "You don't have access to messages.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Message? m = site.Messages.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("mid"), 20));
            if (m == null)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message no longer exists.');");
                return response;
            }
            Dictionary<string, Staff> staff = Ui.StaffMap(await Store.LoadStaff(c.Sbx));
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string subject = "Your message to " + site.Settings.Name;
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(m.Name) + " <small style=\"font-family:var(--xg-text);font-size:14px;color:var(--xg-ink2)\">" + Ui.Enc(m.Id) + "</small></h2><dl class=\"xg-dl\"><dt>Status</dt><dd>" + Ui.Pill("xg", Ui.MsgStatuses, m.Status) + "</dd><dt>Received</dt><dd>" + Ui.Time(m.Created, "full") + "</dd><dt>Topic</dt><dd>" + Ui.Enc(Ui.NameOf(Ui.Topics, m.Topic)) + "</dd><dt>Email</dt><dd><a href=\"mailto:" + Ui.Attr(m.Email) + "\">" + Ui.Enc(m.Email) + "</a></dd>" + (m.Phone != string.Empty ? "<dt>Phone</dt><dd><a href=\"tel:" + Ui.Attr(Ui.Digits(m.Phone)) + "\">" + Ui.Enc(m.Phone) + "</a></dd>" : string.Empty) + MemberLink(site, m) + "</dl><h3>Message</h3><div class=\"xg-lmsg\">" + Ui.Enc(m.Text) + "</div>");
            if (canE)
            {
                sb.Append("<h3>Reply</h3><div class=\"xg-dec\"><a class=\"xg-btn xg-btn-sm xg-btn-accent\" href=\"mailto:" + Ui.Attr(m.Email) + "?subject=" + Uri.EscapeDataString(subject) + "\">Reply by email</a>" + (m.Status != "replied" ? "<button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"AdminMessagesJs.status('replied')\">Mark replied</button>" : string.Empty) + (m.Status != "closed" ? "<button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"AdminMessagesJs.status('closed')\">Mark closed</button>" : "<button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"AdminMessagesJs.status('new')\">Reopen</button>") + "</div>");
            }
            sb.Append(NotesHtml(m.Notes, staff, canE));
            sb.Append("<div class=\"xg-mfoot\">" + (canE ? "<button type=\"button\" class=\"xg-btn xg-btn-danger xg-left\" onclick=\"AdminMessagesJs.remove('" + Ui.Js(m.Name) + "')\">Delete</button>" : string.Empty) + "<button type=\"button\" class=\"xg-btn xg-btn-ghost\" onclick=\"AdminMessagesJs.closeModal()\">Close</button></div>");
            response.SetElementContents("xg-mbody", sb.ToString());
            response.ExecuteScript("AdminMessagesJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> SetStatus()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "message status", GetDataValue("mid"), "You can look but not change messages.");
            }
            string to = Ui.MsgStatuses.Any(s => s.Key == GetDataValue("mst2")) ? GetDataValue("mst2") : string.Empty;
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Message? m = site.Messages.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("mid"), 20));
                if (m == null || to == string.Empty)
                {
                    return response;
                }
                m.Status = to;
                if (to == "replied")
                {
                    m.Notes.Add(new Note { When = DateTime.UtcNow, By = c.U.Id, Text = "Replied." });
                }
                name = m.Name;
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "message " + to, name, true);
            response.ExecuteScript("AdminMessagesJs.changed('" + (to == "new" ? "Reopened." : "Marked " + Ui.Js(Ui.NameOf(Ui.MsgStatuses, to).ToLowerInvariant()) + ".") + "');");
            return response;
        }

        public async Task<ApiResponse> AddNote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "message note", GetDataValue("mid"), "You can look but not change messages.");
            }
            string text = Ui.OneLine(GetDataValue("enote"), 500);
            if (text.Length < 2)
            {
                response.ExecuteScript("AdminMessagesJs.toast('Write the note first.');");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Message? m = site.Messages.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("mid"), 20));
                if (m == null)
                {
                    return response;
                }
                m.Notes.Add(new Note { When = DateTime.UtcNow, By = c.U.Id, Text = text });
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "message note", GetDataValue("mid"), true);
            response.ExecuteScript("AdminMessagesJs.changed('Note saved.');");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete message", GetDataValue("mid"), "You can look but not change messages.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Message? m = site.Messages.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("mid"), 20));
                if (m == null)
                {
                    return response;
                }
                name = m.Name;
                site.Messages.Remove(m);
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "delete message", name, true);
            response.ExecuteScript("AdminMessagesJs.deleted();");
            return response;
        }
    }
}
