using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminClasses : WebPage
    {
        private const string AreaKey = "classes";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Classes");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ak-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ak-menu", Auth.Menu("ak", "AdminClasses", c, site));
            response.SetElementContents("ak-who", Auth.Who("ak", c));
            response.SetElementContents("ak-demo", Auth.DemoPill("ak", c));
            response.SetElementContents("ak-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminClassesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ak-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ak-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminClassesJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ak-main", NoAccess("Classes"));
                return response;
            }
            response.SetElementContents("ak-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminClassesJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ak-tr ak-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string PickItem(MediaItem m)
        {
            bool vid = m.Kind == "video";
            return "<button type=\"button\" class=\"ak-pki\" data-src=\"" + Ui.Attr(m.Full) + "\" data-thumb=\"" + Ui.Attr(m.Thumb) + "\" data-kind=\"" + m.Kind + "\" data-title=\"" + Ui.Attr(m.Title) + "\" onclick=\"AdminClassesJs.pkToggle(this)\" title=\"" + Ui.Attr(m.Title + (m.Tags.Count > 0 ? " · " + string.Join(", ", m.Tags) : string.Empty)) + "\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<b class=\"ak-vbadge\">" + (m.Seconds / 60) + ":" + (m.Seconds % 60).ToString("00", Ui.Inv) + "</b>" : string.Empty) + "<span>" + Ui.Enc(m.Title) + "</span><i>&#10003;</i></button>";
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            string allow = GetDataValue("pallow") is "photo" or "video" or "doc" ? GetDataValue("pallow") : string.Empty;
            string kind = allow != string.Empty ? allow : GetDataValue("pkind") is "photo" or "video" or "doc" ? GetDataValue("pkind") : string.Empty;
            string term = Ui.Clip(GetDataValue("pterm"), 60).Trim();
            string tag = Ui.Clip(GetDataValue("ptag"), 40);
            List<string> ctx = Ui.Clip(GetDataValue("pctx"), 80).ToLowerInvariant().Split(',').Select(x => x.Trim()).Where(x => x != string.Empty).ToList();
            int page = int.TryParse(GetDataValue("ppg"), out int pv) ? Math.Clamp(pv, 1, 200) : 1;
            const int size = 30;
            List<MediaItem> all = site.Media.Where(m => (kind == string.Empty ? m.Kind != "doc" : m.Kind == kind) && (tag == string.Empty || m.Tags.Contains(tag)) && (term == string.Empty || (m.Title + " " + m.Alt + " " + string.Join(" ", m.Tags)).Contains(term, StringComparison.OrdinalIgnoreCase))).OrderByDescending(m => m.Tags.Count(t => ctx.Contains(t))).ThenByDescending(m => m.When).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (MediaItem m in all.Skip((page - 1) * size).Take(size))
            {
                sb.Append(PickItem(m));
            }
            if (all.Count == 0)
            {
                sb.Append("<p class=\"ak-none\" style=\"grid-column:1/-1\">Nothing matches. Upload a photo, or add videos in Media.</p>");
            }
            string tags = page == 1 ? JsonSerializer.Serialize(site.Media.SelectMany(m => m.Tags).Distinct().OrderBy(t => t).ToList()) : "null";
            response.ExecuteScript("AdminClassesJs.pkShown(" + JsonSerializer.Serialize(sb.ToString()) + "," + (all.Count > page * size ? "true" : "false") + "," + tags + "," + (page > 1 ? "true" : "false") + ");");
            return response;
        }

        private static string PickExt(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            {
                return "jpg";
            }
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                return "png";
            }
            if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            {
                return "webp";
            }
            return string.Empty;
        }

        private static byte[]? PickData(string data, int max)
        {
            Match m = Regex.Match(data ?? string.Empty, "^data:image/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success)
            {
                return null;
            }
            try
            {
                byte[] b = Convert.FromBase64String(m.Groups[2].Value);
                return b.Length <= max && PickExt(b) != string.Empty ? b : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "upload", "photo", "You can look but not upload.");
            }
            byte[]? big = PickData(GetDataValue("full"), 8 * 1024 * 1024);
            byte[]? small = PickData(GetDataValue("thumb"), 3 * 1024 * 1024);
            if (big == null || small == null || PickExt(big) != PickExt(small))
            {
                response.ExecuteScript("AdminClassesJs.pkUploaded(false,'That file is not a JPG, PNG or WebP photo, or it is too large.');");
                return response;
            }
            string ext = PickExt(big);
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);
            string dir = Path.Combine(Store.Root, Store.Up(c.Sbx).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.Combine(dir, "full"));
            Directory.CreateDirectory(Path.Combine(dir, "thumbs"));
            await File.WriteAllBytesAsync(Path.Combine(dir, "full", id + "." + ext), big);
            await File.WriteAllBytesAsync(Path.Combine(dir, "thumbs", id + "." + ext), small);
            string title = Regex.Replace(Ui.Clip(GetDataValue("pname"), 80), "\\s+", " ").Trim();
            List<string> ctx = Ui.Clip(GetDataValue("pctx"), 80).ToLowerInvariant().Split(',').Select(x => Regex.Replace(x, "[^a-z0-9 -]", string.Empty).Trim()).Where(x => x != string.Empty).Distinct().Take(4).ToList();
            int w = int.TryParse(GetDataValue("pw"), out int wv) ? Math.Clamp(wv, 0, 20000) : 0;
            int h = int.TryParse(GetDataValue("ph"), out int hv) ? Math.Clamp(hv, 0, 20000) : 0;
            double r = Math.Min(1.0, 1600.0 / Math.Max(1, Math.Max(w, h)));
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                site.Media.Add(new MediaItem { Id = id, Kind = "photo", Title = title.Length >= 2 ? title : "Photo " + id.Substring(0, 6), Tags = ctx, Full = Store.Up(c.Sbx) + "/full/" + id + "." + ext, Thumb = Store.Up(c.Sbx) + "/thumbs/" + id + "." + ext, Width = (int)Math.Round(w * r), Height = (int)Math.Round(h * r), Bytes = big.Length, By = c.U.Id, When = DateTime.UtcNow });
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "upload", "photo · " + title, true);
            response.ExecuteScript("AdminClassesJs.pkUploaded(true,'');");
            return response;
        }

        private static string PicField(SiteData site, string id, string label, string src, bool ro, bool square = false)
        {
            MediaItem? m = site.Media.FirstOrDefault(x => x.Full == src);
            string thumb = src == string.Empty ? string.Empty : m?.Thumb ?? src;
            return "<div class=\"ak-pic\"><span>" + label + "</span><input type=\"hidden\" id=\"" + id + "\" value=\"" + Ui.Attr(src) + "\"><button type=\"button\" class=\"ak-picb" + (square ? " ak-sq" : string.Empty) + "\" id=\"" + id + "-b\"" + (ro ? " disabled" : " onclick=\"AdminClassesJs.picPick('" + id + "')\"") + ">" + (thumb != string.Empty ? "<img src=\"" + Ui.Attr(thumb) + "\" alt=\"\">" : "<span>Choose a photo</span>") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"ak-picx\" onclick=\"AdminClassesJs.picClear('" + id + "')\">Remove</button>") + "</div>";
        }

        private static string PicOk(SiteData site, string src)
        {
            string v = (src ?? string.Empty).Trim();
            return v != string.Empty && site.Media.Any(m => m.Full == v && m.Kind == "photo") ? v : string.Empty;
        }

        private const int Size = 25;

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E') && !Access.OwnOnly(c.U);
            List<(string, string)> states = Rules.States.Select(s => (s.Key, s.Name)).ToList();
            return "<div class=\"ak-ptop\"><div><h1>" + (Access.OwnOnly(c.U) ? "My classes" : "Classes") + "</h1><p>" + (Access.OwnOnly(c.U) ? "The classes you teach." : "Schedules, seats and prices. The website status (Open, Few seats, Full, In progress, Completed, Cancelled) follows the dates and seats automatically.") + "</p></div><div class=\"ak-acts\">" + (canE ? "<button type=\"button\" class=\"ak-btn ak-btn-accent\" onclick=\"AdminClassesJs.edit('')\">Add a class</button>" : string.Empty) + "<a class=\"ak-btn ak-btn-ghost\" href=\"AdminBoard\">Class board</a></div></div><div class=\"ak-bar\"><label>Search<input type=\"search\" id=\"ak-q\" placeholder=\"Class, place or ID\" maxlength=\"40\" oninput=\"AdminClassesJs.search()\"></label><label>Sport<select id=\"ak-ksport\" onchange=\"AdminClassesJs.list(1)\"><option value=\"\">All sports</option>" + Ui.Opts(site.Sports.OrderBy(s => s.Order).Select(s => (s.Id, s.Name)), string.Empty) + "</select></label><label>Status<select id=\"ak-kstate\" onchange=\"AdminClassesJs.list(1)\"><option value=\"\">Running and upcoming</option><option value=\"all\">Everything</option>" + Ui.Opts(states, string.Empty) + "</select></label>" + (Access.OwnOnly(c.U) ? "<input type=\"hidden\" id=\"ak-kcoach\" value=\"\">" : "<label>Instructor<select id=\"ak-kcoach\" onchange=\"AdminClassesJs.list(1)\"><option value=\"\">Anyone</option>" + Ui.Opts(site.Instructors.OrderBy(i => i.Name).Select(i => (i.Id, i.Name)), string.Empty) + "</select></label>") + "</div><div class=\"ak-tbl ak-ktbl\" id=\"ak-list\"></div><div id=\"ak-pager\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime now = Store.Now(site);
            string term = Ui.Clip(GetDataValue("term"), 40);
            string sport = Ui.Clip(GetDataValue("fsport"), 40);
            string state = Ui.Clip(GetDataValue("fstate"), 20);
            string coach = Ui.Clip(GetDataValue("fcoach"), 30);
            List<SportClass> all = site.Classes.Where(k => Access.Mine(c.U, k) && (sport == string.Empty || k.Sport == sport) && (coach == string.Empty || k.Instructor == coach) && (term == string.Empty || k.Title.Contains(term, StringComparison.OrdinalIgnoreCase) || k.Place.Contains(term, StringComparison.OrdinalIgnoreCase) || k.Id.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            all = all.Where(k =>
            {
                string st = Rules.State(site, k, now);
                return state switch
                {
                    "" => st is "open" or "few" or "full" or "progress" or "draft",
                    "all" => true,
                    _ => st == state
                };
            }).OrderBy(k => Rules.State(site, k, now) == "progress" ? 0 : 1).ThenBy(k => Rules.Start(k)).ToList();
            int page = Ui.PageOf(GetDataValue("pg"), Size, all.Count);
            StringBuilder sb = new StringBuilder(Head(new[] { ("Class", string.Empty), ("When", string.Empty), ("Instructor", string.Empty), ("Seats", string.Empty), ("Price", "ak-num"), ("Status", string.Empty), (string.Empty, string.Empty) }));
            if (all.Count == 0)
            {
                sb.Append("<div class=\"ak-tr\"><span class=\"ak-none ak-wide\">No classes match.</span></div>");
            }
            foreach (SportClass k in all.Skip((page - 1) * Size).Take(Size))
            {
                string st = Rules.State(site, k, now);
                int taken = Rules.Taken(site, k);
                int wait = Rules.Waiting(site, k);
                sb.Append("<div class=\"ak-tr\"><span class=\"ak-wide\"><b>" + Ui.Enc(k.Title) + "</b><small>" + Ui.Enc(k.Id) + " &middot; " + Ui.Enc(Ui.SportName(site, k.Sport)) + " &middot; " + Rules.AgeRule(k) + "</small></span><span data-l=\"When\">" + Ui.ClassWhen(site, k) + "<small>" + Ui.Range(k) + "</small></span><span data-l=\"Instructor\">" + Ui.Enc(Ui.CoachName(site, k.Instructor)) + "</span><span data-l=\"Seats\">" + taken + " / " + k.Capacity + (wait > 0 ? " &middot; " + wait + " waiting" : string.Empty) + "<span class=\"ak-seat\"><i style=\"width:" + (k.Capacity > 0 ? Math.Min(100, taken * 100 / k.Capacity) : 0) + "%\"></i></span></span><span data-l=\"Price\" class=\"ak-num\">" + Ui.Money(k.Price) + "</span><span data-l=\"Status\">" + Ui.StatePill("ak", st) + "</span><span><button type=\"button\" class=\"ak-btn ak-btn-sm ak-btn-ghost\" onclick=\"AdminClassesJs.edit('" + Ui.Attr(k.Id) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("ak-list", sb.ToString());
            response.SetElementContents("ak-pager", Ui.Pager("ak", page, Size, all.Count, "class", "classes", "AdminClassesJs.list"));
            return response;
        }

        private static string SchedText(SiteData site, List<DateTime> dates, int minutes)
        {
            if (dates.Count == 0)
            {
                return "Pick at least one day and a number of sessions.";
            }
            return "<b>" + Ui.Plural(dates.Count, "session", "sessions") + "</b>: " + Ui.Day(dates[0], false) + " to " + Ui.Day(dates[^1]) + " at " + Ui.Hm(site, dates[0]) + " &ndash; " + Ui.Hm(site, dates[0].AddMinutes(minutes));
        }

        public async Task<ApiResponse> Preview()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime? first = Ui.ParseDay(GetDataValue("kfirst"));
            List<int> days = GetDataValue("kdays").Split(',').Select(x => int.TryParse(x, out int d) ? d : -1).Where(d => d >= 0 && d <= 6).ToList();
            int count = int.TryParse(GetDataValue("kcount"), out int n) ? Math.Clamp(n, 1, 60) : 8;
            int minutes = int.TryParse(GetDataValue("kmin"), out int mm) ? Math.Clamp(mm, 15, 480) : 60;
            List<DateTime> dates = first == null ? new List<DateTime>() : Rules.Schedule(first.Value, days, Store.Hm(GetDataValue("kstart")) != null ? GetDataValue("kstart") : "18:00", count);
            response.SetElementContents("ak-ksched", SchedText(site, dates, minutes));
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "class form", GetDataValue("kid"), "You don't have access to classes.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime now = Store.Now(site);
            SportClass? x = site.Classes.FirstOrDefault(k => k.Id == GetDataValue("kid"));
            if (x != null && !Access.Mine(c.U, x))
            {
                return await Refuse(response, c, "class form", x.Id, "You can only open your own classes.");
            }
            SportClass v = x ?? new SportClass { First = now.Date.AddDays(14 - (int)now.DayOfWeek + 1), Days = new List<int> { 1 }, Count = 8, Capacity = 12, Price = 120, Sport = site.Sports.OrderBy(s => s.Order).FirstOrDefault()?.Id ?? string.Empty };
            bool canE = Access.Can(c.U, AreaKey, 'E') && !Access.OwnOnly(c.U);
            bool canX = Access.Can(c.U, AreaKey, 'X') && !Access.OwnOnly(c.U);
            bool started = x != null && now >= Rules.Start(x);
            bool locked = !canE || started || (x != null && x.Status == "cancelled");
            string ro = canE && (x == null || x.Status != "cancelled") ? string.Empty : " readonly";
            string rs = locked ? " readonly" : string.Empty;
            string ds = locked ? " disabled" : string.Empty;
            int taken = x != null ? Rules.Taken(site, x) : 0;
            int regs = x != null ? site.Registrations.Count(r => r.Class == x.Id) : 0;
            StringBuilder sb = new StringBuilder("<h2>" + (x == null ? "Add a class" : Ui.Enc(x.Title)) + "</h2>");
            if (x != null)
            {
                string st = Rules.State(site, x, now);
                sb.Append("<p class=\"ak-hint\" style=\"margin:-6px 0 12px\">" + Ui.StatePill("ak", st) + " &nbsp;" + Ui.Enc(x.Id) + " &middot; " + taken + " of " + x.Capacity + " seats taken &middot; " + Rules.Waiting(site, x) + " waiting &middot; <a class=\"ak-more\" href=\"AdminBoard?id=" + Uri.EscapeDataString(x.Id) + "\" style=\"color:var(--ak-link);font-weight:700\">Open the class board</a></p>");
                if (x.Status == "cancelled")
                {
                    sb.Append("<div class=\"ak-warn-box\" style=\"margin-bottom:12px\"><b>Cancelled " + (x.Cancelled != null ? Ui.Day(Store.Local(site, x.Cancelled.Value)) : string.Empty) + ".</b> " + Ui.Enc(x.CancelReason) + "</div>");
                }
                else if (started && canE)
                {
                    sb.Append("<div class=\"ak-info-box\" style=\"margin-bottom:12px\">The class has started, so the schedule is locked. You can still change the text, picture and seats.</div>");
                }
            }
            sb.Append("<div class=\"ak-form\"><div class=\"ak-f3\"><label class=\"ak-field\" style=\"grid-column:span 2\"><span>Title</span><input type=\"text\" id=\"ak-ktitle\" maxlength=\"80\" value=\"" + Ui.Attr(v.Title) + "\"" + ro + "></label><label class=\"ak-field\"><span>Sport</span><select id=\"ak-ksp\"" + (ro != string.Empty ? " disabled" : string.Empty) + ">" + Ui.Opts(site.Sports.OrderBy(s => s.Order).Select(s => (s.Id, s.Name)), v.Sport) + "</select></label></div>");
            sb.Append("<div class=\"ak-f4\"><label class=\"ak-field\"><span>Level</span><select id=\"ak-klevel\"" + (ro != string.Empty ? " disabled" : string.Empty) + ">" + Ui.Opts(Rules.Levels.Select(l => (l.Key, l.Name)), v.Level) + "</select></label><label class=\"ak-field\"><span>Youngest age</span><input type=\"number\" id=\"ak-kamin\" min=\"0\" max=\"99\" value=\"" + v.AgeMin + "\"" + rs + "></label><label class=\"ak-field\"><span>Oldest age (99 = no limit)</span><input type=\"number\" id=\"ak-kamax\" min=\"0\" max=\"99\" value=\"" + v.AgeMax + "\"" + rs + "></label><label class=\"ak-field\"><span>Instructor</span><select id=\"ak-kcoach2\"" + (ro != string.Empty ? " disabled" : string.Empty) + "><option value=\"\">To be announced</option>" + Ui.Opts(site.Instructors.OrderBy(i => i.Name).Select(i => (i.Id, i.Name)), v.Instructor) + "</select></label></div>");
            sb.Append("<div><span class=\"ak-hint\">Days</span><div class=\"ak-days\" id=\"ak-kdays\">");
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                sb.Append("<label><input type=\"checkbox\" value=\"" + d + "\"" + (v.Days.Contains(d) ? " checked" : string.Empty) + ds + " onchange=\"AdminClassesJs.preview()\"><span>" + Ui.DayShort[d] + "</span></label>");
            }
            sb.Append("</div></div><div class=\"ak-f4\"><label class=\"ak-field\"><span>First session</span><input type=\"date\" id=\"ak-kfirst\" value=\"" + Ui.Ymd(v.First) + "\"" + rs + " onchange=\"AdminClassesJs.preview()\"></label><label class=\"ak-field\"><span>Start time</span><input type=\"time\" id=\"ak-kstart\" step=\"300\" value=\"" + Ui.Attr(v.Start) + "\"" + rs + " onchange=\"AdminClassesJs.preview()\"></label><label class=\"ak-field\"><span>Minutes</span><input type=\"number\" id=\"ak-kmin\" min=\"15\" max=\"480\" step=\"5\" value=\"" + v.Minutes + "\"" + rs + " onchange=\"AdminClassesJs.preview()\"></label><label class=\"ak-field\"><span>Sessions</span><input type=\"number\" id=\"ak-kcount\" min=\"1\" max=\"60\" value=\"" + v.Count + "\"" + rs + " onchange=\"AdminClassesJs.preview()\"></label></div><div class=\"ak-sched\" id=\"ak-ksched\">" + SchedText(site, x != null ? x.Dates : Rules.Schedule(v.First, v.Days, v.Start, v.Count), v.Minutes) + "</div>");
            sb.Append("<div class=\"ak-f4\"><label class=\"ak-field\"><span>Price for all sessions ($)</span><input type=\"text\" inputmode=\"decimal\" id=\"ak-kprice\" maxlength=\"8\" value=\"" + v.Price.ToString("0.##", Ui.Inv) + "\"" + rs + "></label><label class=\"ak-field\"><span>Seats</span><input type=\"number\" id=\"ak-kcap\" min=\"1\" max=\"200\" value=\"" + v.Capacity + "\"" + ro + "></label><label class=\"ak-field\" style=\"grid-column:span 2\"><span>Registration opens (empty = now)</span><input type=\"datetime-local\" id=\"ak-kopens\" value=\"" + (v.Opens != null ? Ui.Stamp(v.Opens.Value) : string.Empty) + "\"" + rs + "></label></div><label class=\"ak-field\"><span>Where</span><input type=\"text\" id=\"ak-kplace\" maxlength=\"60\" value=\"" + Ui.Attr(v.Place) + "\"" + ro + " placeholder=\"Teaching pool\"></label>");
            sb.Append("<div class=\"ak-pics\">" + PicField(site, "ak-kimg", "Picture (empty = the sport's picture)", v.Image, ro != string.Empty) + "<div class=\"ak-form\"><label class=\"ak-field\"><span>About the class (simple HTML allowed)</span><textarea id=\"ak-ktext\" rows=\"5\" maxlength=\"3000\"" + ro + ">" + Ui.Enc(v.Text) + "</textarea></label><label class=\"ak-field\"><span>What to bring</span><input type=\"text\" id=\"ak-kbring\" maxlength=\"200\" value=\"" + Ui.Attr(v.Bring) + "\"" + ro + "></label></div></div><label class=\"ak-chk\"><input type=\"checkbox\" id=\"ak-kpub\"" + (x == null ? string.Empty : v.Published ? " checked" : string.Empty) + (ro != string.Empty ? " disabled" : string.Empty) + "> Published on the website (unpublished classes show as Draft)</label><em class=\"ak-err\" id=\"ak-e-class\" role=\"alert\"></em></div><div class=\"ak-mfoot\">");
            if (x != null && x.Status != "cancelled" && now < Rules.End(x) && canX)
            {
                int active = site.Registrations.Count(r => r.Class == x.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist");
                sb.Append("<button type=\"button\" class=\"ak-btn ak-btn-danger ak-left\" onclick=\"AdminClassesJs.cancelClass('" + Ui.Attr(x.Id) + "','" + Ui.Js(x.Title) + "'," + active + ")\">Cancel class</button>");
            }
            else if (x != null && regs == 0 && canE)
            {
                sb.Append("<button type=\"button\" class=\"ak-btn ak-btn-danger ak-left\" onclick=\"AdminClassesJs.drop('" + Ui.Attr(x.Id) + "','" + Ui.Js(x.Title) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"ak-btn ak-btn-ghost\" onclick=\"AdminClassesJs.closeModal()\">" + (ro == string.Empty ? "Cancel" : "Close") + "</button>" + (ro == string.Empty ? "<button type=\"button\" class=\"ak-btn ak-btn-accent\" onclick=\"AdminClassesJs.save('" + Ui.Attr(x?.Id ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("ak-mbody", sb.ToString());
            response.ExecuteScript("AdminClassesJs.openModal();");
            return response;
        }

        private static decimal Dec(string v, decimal cur)
        {
            return decimal.TryParse((v ?? string.Empty).Replace("$", string.Empty).Replace(",", string.Empty).Trim(), NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Clamp(Math.Round(d, 2), 0, 100000) : cur;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "class", GetDataValue("kid"), "You can look but not change classes.");
            }
            if (Access.OwnOnly(c.U))
            {
                return await Refuse(response, c, "class", GetDataValue("kid"), "Instructors can't change class settings.");
            }
            string title = Ui.OneLine(GetDataValue("ktitle"), 80);
            DateTime? first = Ui.ParseDay(GetDataValue("kfirst"));
            List<int> days = GetDataValue("kdays").Split(',').Select(x => int.TryParse(x, out int d) ? d : -1).Where(d => d >= 0 && d <= 6).Distinct().ToList();
            string start = Store.Hm(GetDataValue("kstart")) != null ? GetDataValue("kstart") : string.Empty;
            int count = int.TryParse(GetDataValue("kcount"), out int n) ? n : 0;
            int minutes = int.TryParse(GetDataValue("kmin"), out int mm) ? mm : 0;
            int cap = int.TryParse(GetDataValue("kcap"), out int cp) ? cp : 0;
            int amin = int.TryParse(GetDataValue("kamin"), out int a1) ? Math.Clamp(a1, 0, 99) : 0;
            int amax = int.TryParse(GetDataValue("kamax"), out int a2) ? Math.Clamp(a2, 0, 99) : 99;
            string opensRaw = GetDataValue("kopens");
            DateTime? opens = Ui.ParseStart(opensRaw);
            string err = title.Length < 3 ? "Enter a class title." : cap < 1 || cap > 200 ? "Seats must be between 1 and 200." : amin > amax ? "The youngest age must be below the oldest." : opensRaw != string.Empty && opens == null ? "Check the registration opening time." : string.Empty;
            string savedId = string.Empty;
            int offered = 0;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                DateTime now = Store.Now(site);
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == GetDataValue("kid"));
                if (k != null && k.Status == "cancelled")
                {
                    err = "Cancelled classes can't be changed.";
                }
                bool started = k != null && now >= Rules.Start(k);
                if (err == string.Empty && !started)
                {
                    if (first == null || days.Count == 0 || start == string.Empty || count < 1 || count > 60 || minutes < 15 || minutes > 480)
                    {
                        err = "Set the first session, at least one day, a start time, 15 to 480 minutes and 1 to 60 sessions.";
                    }
                    else if (k == null && first.Value < now.Date)
                    {
                        err = "The first session can't be in the past.";
                    }
                }
                if (err == string.Empty && !site.Sports.Any(s => s.Id == GetDataValue("ksp")) && !started)
                {
                    err = "Choose a sport.";
                }
                if (err == string.Empty && k != null && cap < Rules.Taken(site, k))
                {
                    err = Rules.Taken(site, k) + " seats are already taken. Seats can't go below that.";
                }
                if (err == string.Empty)
                {
                    if (k == null)
                    {
                        k = new SportClass { Id = Store.NextId(site.Classes.Select(x => x.Id), "C-", 100), Created = DateTime.UtcNow };
                        site.Classes.Add(k);
                    }
                    k.Title = title;
                    k.Capacity = cap;
                    k.Place = Ui.OneLine(GetDataValue("kplace"), 60);
                    k.Image = PicOk(site, GetDataValue("kimg"));
                    k.Text = Blocks.Sanitize(GetDataValue("ktext"), 5000);
                    k.Bring = Ui.OneLine(GetDataValue("kbring"), 200);
                    k.Published = GetDataValue("kpub") == "1";
                    k.Instructor = site.Instructors.Any(i => i.Id == GetDataValue("kcoach2")) ? GetDataValue("kcoach2") : string.Empty;
                    k.Level = Rules.Levels.Any(l => l.Key == GetDataValue("klevel")) ? GetDataValue("klevel") : "all";
                    if (!started)
                    {
                        k.Sport = GetDataValue("ksp");
                        k.AgeMin = amin;
                        k.AgeMax = amax;
                        k.First = first!.Value;
                        k.Days = days;
                        k.Start = start;
                        k.Minutes = minutes;
                        k.Count = count;
                        k.Dates = Rules.Schedule(k.First, k.Days, k.Start, k.Count);
                        k.Price = Dec(GetDataValue("kprice"), k.Price);
                        k.Opens = opens;
                    }
                    offered = Rules.OfferNext(site, k, DateTime.UtcNow).Count;
                    savedId = k.Id;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("ak-e-class", err);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "class", savedId + " · " + title, true);
            response.ExecuteScript("AdminClassesJs.closeModal();AdminClassesJs.list(1);AdminClassesJs.toast('" + (offered > 0 ? "Saved. " + Ui.Plural(offered, "seat was", "seats were") + " offered to the waitlist." : "Saved") + "');");
            return response;
        }

        public async Task<ApiResponse> CancelClass()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "cancel class", GetDataValue("kid"), "Cancelling a class needs the Cancel class right.");
            }
            string why = Ui.OneLine(GetDataValue("kwhy"), 300);
            if (why.Length < 5)
            {
                response.ExecuteScript("AdminClassesJs.toast('Write a short reason.');");
                return response;
            }
            string title = string.Empty;
            int people = 0;
            decimal back = 0m;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == GetDataValue("kid"));
                if (k != null && k.Status != "cancelled" && Access.Mine(c.U, k))
                {
                    DateTime nowU = DateTime.UtcNow;
                    k.Status = "cancelled";
                    k.Cancelled = nowU;
                    k.CancelReason = why;
                    foreach (Registration r in site.Registrations.Where(r => r.Class == k.Id && r.Status is "confirmed" or "pending" or "offered" or "waitlist").ToList())
                    {
                        decimal amt = Rules.Refundable(r);
                        back += amt;
                        people++;
                        Rules.EndReg(site, r, "cancelled", amt, c.U.Id, nowU, "Class cancelled: " + why);
                    }
                    title = k.Id + " · " + k.Title;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (title == string.Empty)
            {
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "cancel class", title + " · " + people + " people · " + Ui.Plain(back) + " refunded", true);
            response.ExecuteScript("AdminClassesJs.closeModal();AdminClassesJs.list(1);AdminClassesJs.toast('" + Ui.Js("Class cancelled. " + Ui.Plural(people, "person", "people") + " taken off, " + Ui.Plain(back) + " refunded.") + "');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete class", GetDataValue("kid"), "You can look but not change classes.");
            }
            string title = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                SportClass? k = site.Classes.FirstOrDefault(x => x.Id == GetDataValue("kid"));
                if (k != null && !Access.OwnOnly(c.U) && !site.Registrations.Any(r => r.Class == k.Id))
                {
                    site.Classes.Remove(k);
                    title = k.Id + " · " + k.Title;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (title == string.Empty)
            {
                response.SetElementContents("ak-e-class", "This class has registrations. Cancel it instead.");
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "delete class", title, true);
            response.ExecuteScript("AdminClassesJs.closeModal();AdminClassesJs.list(1);AdminClassesJs.toast('Deleted');");
            return response;
        }
    }
}
