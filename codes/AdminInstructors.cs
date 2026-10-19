using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminInstructors : WebPage
    {
        private const string AreaKey = "instructors";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Instructors");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--ai-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ai-menu", Auth.Menu("ai", "AdminInstructors", c, site));
            response.SetElementContents("ai-who", Auth.Who("ai", c));
            response.SetElementContents("ai-demo", Auth.DemoPill("ai", c));
            response.SetElementContents("ai-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminInstructorsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"ai-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"ai-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminInstructorsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInstructorsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("ai-main", NoAccess("Instructors"));
                return response;
            }
            response.SetElementContents("ai-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminInstructorsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"ai-tr ai-th\">");
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
            return "<button type=\"button\" class=\"ai-pki\" data-src=\"" + Ui.Attr(m.Full) + "\" data-thumb=\"" + Ui.Attr(m.Thumb) + "\" data-kind=\"" + m.Kind + "\" data-title=\"" + Ui.Attr(m.Title) + "\" onclick=\"AdminInstructorsJs.pkToggle(this)\" title=\"" + Ui.Attr(m.Title + (m.Tags.Count > 0 ? " · " + string.Join(", ", m.Tags) : string.Empty)) + "\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<b class=\"ai-vbadge\">" + (m.Seconds / 60) + ":" + (m.Seconds % 60).ToString("00", Ui.Inv) + "</b>" : string.Empty) + "<span>" + Ui.Enc(m.Title) + "</span><i>&#10003;</i></button>";
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInstructorsJs.expired();");
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
                sb.Append("<p class=\"ai-none\" style=\"grid-column:1/-1\">Nothing matches. Upload a photo, or add videos in Media.</p>");
            }
            string tags = page == 1 ? JsonSerializer.Serialize(site.Media.SelectMany(m => m.Tags).Distinct().OrderBy(t => t).ToList()) : "null";
            response.ExecuteScript("AdminInstructorsJs.pkShown(" + JsonSerializer.Serialize(sb.ToString()) + "," + (all.Count > page * size ? "true" : "false") + "," + tags + "," + (page > 1 ? "true" : "false") + ");");
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
                response.ExecuteScript("AdminInstructorsJs.expired();");
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
                response.ExecuteScript("AdminInstructorsJs.pkUploaded(false,'That file is not a JPG, PNG or WebP photo, or it is too large.');");
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
            response.ExecuteScript("AdminInstructorsJs.pkUploaded(true,'');");
            return response;
        }

        private static string PicField(SiteData site, string id, string label, string src, bool ro, bool square = false)
        {
            MediaItem? m = site.Media.FirstOrDefault(x => x.Full == src);
            string thumb = src == string.Empty ? string.Empty : m?.Thumb ?? src;
            return "<div class=\"ai-pic\"><span>" + label + "</span><input type=\"hidden\" id=\"" + id + "\" value=\"" + Ui.Attr(src) + "\"><button type=\"button\" class=\"ai-picb" + (square ? " ai-sq" : string.Empty) + "\" id=\"" + id + "-b\"" + (ro ? " disabled" : " onclick=\"AdminInstructorsJs.picPick('" + id + "')\"") + ">" + (thumb != string.Empty ? "<img src=\"" + Ui.Attr(thumb) + "\" alt=\"\">" : "<span>Choose a photo</span>") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"ai-picx\" onclick=\"AdminInstructorsJs.picClear('" + id + "')\">Remove</button>") + "</div>";
        }

        private static string PicOk(SiteData site, string src)
        {
            string v = (src ?? string.Empty).Trim();
            return v != string.Empty && site.Media.Any(m => m.Full == v && m.Kind == "photo") ? v : string.Empty;
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            return "<div class=\"ai-ptop\"><div><h1>Instructors</h1><p>Coach profiles on the website. Link a coach to a staff login in Users so they see their own classes on the Class board.</p></div><div class=\"ai-acts\">" + (canE ? "<button type=\"button\" class=\"ai-btn ai-btn-accent\" onclick=\"AdminInstructorsJs.edit('')\">Add an instructor</button>" : string.Empty) + "</div></div><div class=\"ai-bar\"><label>Search<input type=\"search\" id=\"ai-q\" placeholder=\"Name or sport\" maxlength=\"40\" oninput=\"AdminInstructorsJs.search()\"></label></div><div class=\"ai-cogrid\" id=\"ai-list\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInstructorsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            List<Staff> staff = await Store.LoadStaff(c.Sbx);
            DateTime now = Store.Now(site);
            string term = Ui.Clip(GetDataValue("term"), 40);
            StringBuilder sb = new StringBuilder();
            foreach (global::Sport.Models.Coach x in site.Instructors.Where(i => term == string.Empty || i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || i.Sports.Any(s => Ui.SportName(site, s).Contains(term, StringComparison.OrdinalIgnoreCase))).OrderBy(i => i.Order))
            {
                int active = site.Classes.Count(k => k.Instructor == x.Id && Rules.State(site, k, now) is "open" or "few" or "full" or "progress");
                Staff? login = staff.FirstOrDefault(s => s.Instructor == x.Id);
                sb.Append("<button type=\"button\" class=\"ai-co\" onclick=\"AdminInstructorsJs.edit('" + Ui.Attr(x.Id) + "')\">" + Ui.Face(x.Photo, string.Empty, string.Empty) + "<span><b>" + Ui.Enc(x.Name) + "</b><small>" + Ui.Enc(x.Title) + "</small><em>" + Ui.Enc(string.Join(", ", x.Sports.Select(s => Ui.SportName(site, s)))) + " &middot; " + Ui.Plural(active, "class", "classes") + "</em><small>" + (login != null ? "Staff login: " + Ui.Enc(login.Login) : "No staff login") + "</small><span class=\"ai-st ai-st-" + (x.Published ? "live\">Published" : "off\">Hidden") + "</span></span></button>");
            }
            response.SetElementContents("ai-list", sb.Length == 0 ? "<p class=\"ai-none\">No instructors match.</p>" : sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInstructorsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "instructor form", GetDataValue("iid"), "You don't have access to instructors.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            global::Sport.Models.Coach? x = site.Instructors.FirstOrDefault(i => i.Id == GetDataValue("iid"));
            global::Sport.Models.Coach v = x ?? new global::Sport.Models.Coach { Order = site.Instructors.Count + 1, Since = Store.Today(site).Year };
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string ro = canE ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<h2>" + (x == null ? "Add an instructor" : Ui.Enc(x.Name)) + "</h2><div class=\"ai-form\"><div style=\"display:grid;grid-template-columns:220px minmax(0,1fr);gap:16px\" class=\"ai-cof\">" + PicField(site, "ai-iphoto", "Portrait", v.Photo, !canE, true) + "<div class=\"ai-form\"><div class=\"ai-f2\"><label class=\"ai-field\"><span>Name</span><input type=\"text\" id=\"ai-iname\" maxlength=\"60\" value=\"" + Ui.Attr(v.Name) + "\"" + ro + "></label><label class=\"ai-field\"><span>Title</span><input type=\"text\" id=\"ai-ititle\" maxlength=\"60\" value=\"" + Ui.Attr(v.Title) + "\"" + ro + " placeholder=\"Swim coach\"></label></div><div class=\"ai-f3\"><label class=\"ai-field\"><span>Email</span><input type=\"email\" id=\"ai-imail\" maxlength=\"80\" value=\"" + Ui.Attr(v.Email) + "\"" + ro + "></label><label class=\"ai-field\"><span>Coaching here since</span><input type=\"number\" id=\"ai-isince\" min=\"1950\" max=\"2100\" value=\"" + v.Since + "\"" + ro + "></label><label class=\"ai-field\"><span>Order</span><input type=\"number\" id=\"ai-iorder\" min=\"0\" max=\"99\" value=\"" + v.Order + "\"" + ro + "></label></div><div><span class=\"ai-hint\">Sports</span><div class=\"ai-spchk\" id=\"ai-isports\">");
            foreach (SportType s in site.Sports.OrderBy(s => s.Order))
            {
                sb.Append("<label class=\"ai-chk\"><input type=\"checkbox\" value=\"" + Ui.Attr(s.Id) + "\"" + (v.Sports.Contains(s.Id) ? " checked" : string.Empty) + (canE ? string.Empty : " disabled") + "> " + Ui.Enc(s.Name) + "</label>");
            }
            sb.Append("</div></div></div></div><label class=\"ai-field\"><span>Short bio</span><textarea id=\"ai-ibio\" rows=\"4\" maxlength=\"800\"" + ro + ">" + Ui.Enc(v.Bio) + "</textarea></label><label class=\"ai-field\"><span>Certifications, one per line</span><textarea id=\"ai-icerts\" rows=\"3\" maxlength=\"600\"" + ro + ">" + Ui.Enc(string.Join("\n", v.Certs)) + "</textarea></label><label class=\"ai-chk\"><input type=\"checkbox\" id=\"ai-ipub\"" + (x == null || v.Published ? " checked" : string.Empty) + (canE ? string.Empty : " disabled") + "> Show on the website</label><em class=\"ai-err\" id=\"ai-e-co\" role=\"alert\"></em></div><div class=\"ai-mfoot\"><button type=\"button\" class=\"ai-btn ai-btn-ghost\" onclick=\"AdminInstructorsJs.closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"ai-btn ai-btn-accent\" onclick=\"AdminInstructorsJs.save('" + Ui.Attr(x?.Id ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("ai-mbody", sb.ToString());
            response.ExecuteScript("AdminInstructorsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInstructorsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "instructor", GetDataValue("iid"), "You can look but not change instructors.");
            }
            string name = Ui.OneLine(GetDataValue("iname"), 60);
            string mail = Ui.Clip(GetDataValue("imail"), 80).Trim();
            if (name.Length < 2)
            {
                response.SetElementContents("ai-e-co", "Enter the instructor's name.");
                return response;
            }
            if (mail != string.Empty && !Ui.Email(mail))
            {
                response.SetElementContents("ai-e-co", "Check the email address.");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                global::Sport.Models.Coach? x = site.Instructors.FirstOrDefault(i => i.Id == GetDataValue("iid"));
                if (x == null)
                {
                    string key = "i-" + Regex.Replace(name.Split(' ')[0].ToLowerInvariant(), "[^a-z0-9]", string.Empty);
                    while (site.Instructors.Any(i => i.Id == key))
                    {
                        key += "x";
                    }
                    x = new global::Sport.Models.Coach { Id = key };
                    site.Instructors.Add(x);
                }
                x.Name = name;
                x.Title = Ui.OneLine(GetDataValue("ititle"), 60);
                x.Bio = Ui.Clip(GetDataValue("ibio"), 800).Trim();
                x.Email = mail;
                x.Certs = Ui.SplitLines(GetDataValue("icerts"), 8, 80);
                x.Since = int.TryParse(GetDataValue("isince"), out int y) ? Math.Clamp(y, 1950, 2100) : x.Since;
                x.Order = int.TryParse(GetDataValue("iorder"), out int o) ? Math.Clamp(o, 0, 99) : x.Order;
                x.Photo = PicOk(site, GetDataValue("iphoto"));
                x.Sports = GetDataValue("isports").Split(',').Where(s => site.Sports.Any(t => t.Id == s)).Distinct().ToList();
                x.Published = GetDataValue("ipub") == "1";
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "instructor", name, true);
            response.ExecuteScript("AdminInstructorsJs.closeModal();AdminInstructorsJs.list(1);AdminInstructorsJs.toast('Saved');");
            return response;
        }
    }
}
