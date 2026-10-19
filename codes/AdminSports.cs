using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminSports : WebPage
    {
        private const string AreaKey = "sports";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Sport types");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--as-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("as-menu", Auth.Menu("as", "AdminSports", c, site));
            response.SetElementContents("as-who", Auth.Who("as", c));
            response.SetElementContents("as-demo", Auth.DemoPill("as", c));
            response.SetElementContents("as-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminSportsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"as-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"as-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminSportsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSportsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("as-main", NoAccess("Sport types"));
                return response;
            }
            response.SetElementContents("as-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminSportsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"as-tr as-th\">");
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
            return "<button type=\"button\" class=\"as-pki\" data-src=\"" + Ui.Attr(m.Full) + "\" data-thumb=\"" + Ui.Attr(m.Thumb) + "\" data-kind=\"" + m.Kind + "\" data-title=\"" + Ui.Attr(m.Title) + "\" onclick=\"AdminSportsJs.pkToggle(this)\" title=\"" + Ui.Attr(m.Title + (m.Tags.Count > 0 ? " · " + string.Join(", ", m.Tags) : string.Empty)) + "\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<b class=\"as-vbadge\">" + (m.Seconds / 60) + ":" + (m.Seconds % 60).ToString("00", Ui.Inv) + "</b>" : string.Empty) + "<span>" + Ui.Enc(m.Title) + "</span><i>&#10003;</i></button>";
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSportsJs.expired();");
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
                sb.Append("<p class=\"as-none\" style=\"grid-column:1/-1\">Nothing matches. Upload a photo, or add videos in Media.</p>");
            }
            string tags = page == 1 ? JsonSerializer.Serialize(site.Media.SelectMany(m => m.Tags).Distinct().OrderBy(t => t).ToList()) : "null";
            response.ExecuteScript("AdminSportsJs.pkShown(" + JsonSerializer.Serialize(sb.ToString()) + "," + (all.Count > page * size ? "true" : "false") + "," + tags + "," + (page > 1 ? "true" : "false") + ");");
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
                response.ExecuteScript("AdminSportsJs.expired();");
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
                response.ExecuteScript("AdminSportsJs.pkUploaded(false,'That file is not a JPG, PNG or WebP photo, or it is too large.');");
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
            response.ExecuteScript("AdminSportsJs.pkUploaded(true,'');");
            return response;
        }

        private static string PicField(SiteData site, string id, string label, string src, bool ro, bool square = false)
        {
            MediaItem? m = site.Media.FirstOrDefault(x => x.Full == src);
            string thumb = src == string.Empty ? string.Empty : m?.Thumb ?? src;
            return "<div class=\"as-pic\"><span>" + label + "</span><input type=\"hidden\" id=\"" + id + "\" value=\"" + Ui.Attr(src) + "\"><button type=\"button\" class=\"as-picb" + (square ? " as-sq" : string.Empty) + "\" id=\"" + id + "-b\"" + (ro ? " disabled" : " onclick=\"AdminSportsJs.picPick('" + id + "')\"") + ">" + (thumb != string.Empty ? "<img src=\"" + Ui.Attr(thumb) + "\" alt=\"\">" : "<span>Choose a photo</span>") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"as-picx\" onclick=\"AdminSportsJs.picClear('" + id + "')\">Remove</button>") + "</div>";
        }

        private static string PicOk(SiteData site, string src)
        {
            string v = (src ?? string.Empty).Trim();
            return v != string.Empty && site.Media.Any(m => m.Full == v && m.Kind == "photo") ? v : string.Empty;
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            return "<div class=\"as-ptop\"><div><h1>Sport types</h1><p>The sports on the website. Each class belongs to one sport. Drag order is the Order number: lower comes first.</p></div><div class=\"as-acts\">" + (canE ? "<button type=\"button\" class=\"as-btn as-btn-accent\" onclick=\"AdminSportsJs.edit('')\">Add a sport</button>" : string.Empty) + "<a class=\"as-btn as-btn-ghost\" href=\"Sports\" target=\"_blank\" rel=\"noopener\">View on site</a></div></div><div class=\"as-tbl as-sptbl\" id=\"as-list\"></div>";
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSportsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            DateTime now = Store.Now(site);
            bool canE = Access.Can(c.U, AreaKey, 'E');
            StringBuilder sb = new StringBuilder(Head(new[] { ("Order", string.Empty), ("Picture", string.Empty), ("Sport", string.Empty), ("Where", string.Empty), ("Classes", string.Empty), ("Status", string.Empty), (string.Empty, string.Empty) }));
            foreach (SportType x in site.Sports.OrderBy(x => x.Order))
            {
                int open = site.Classes.Count(k => k.Sport == x.Id && Rules.State(site, k, now) is "open" or "few" or "full");
                int all = site.Classes.Count(k => k.Sport == x.Id);
                sb.Append("<div class=\"as-tr\"><span data-l=\"Order\">" + x.Order + "</span><span>" + (x.Image != string.Empty ? "<img class=\"as-thumbc\" src=\"" + Ui.Attr(Ui.Thumb(x.Image)) + "\" alt=\"\">" : string.Empty) + "</span><span class=\"as-wide\"><b>" + Ui.Enc(x.Name) + "</b><small>" + Ui.Enc(x.Blurb) + "</small></span><span data-l=\"Where\">" + Ui.Enc(x.Place) + "</span><span data-l=\"Classes\">" + open + " open &middot; " + all + " total</span><span data-l=\"Status\"><span class=\"as-st as-st-" + (x.Published ? "live\">Published" : "off\">Hidden") + "</span></span><span><button type=\"button\" class=\"as-btn as-btn-sm as-btn-ghost\" onclick=\"AdminSportsJs.edit('" + Ui.Attr(x.Id) + "')\">" + (canE ? "Edit" : "View") + "</button></span></div>");
            }
            response.SetElementContents("as-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSportsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "sport form", GetDataValue("sid"), "You don't have access to sport types.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            SportType? x = site.Sports.FirstOrDefault(s => s.Id == GetDataValue("sid"));
            SportType v = x ?? new SportType { Order = site.Sports.Count == 0 ? 1 : site.Sports.Max(s => s.Order) + 1 };
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string ro = canE ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<h2>" + (x == null ? "Add a sport" : Ui.Enc(x.Name)) + "</h2><div class=\"as-form\"><div class=\"as-f3\"><label class=\"as-field\"><span>Name</span><input type=\"text\" id=\"as-sname\" maxlength=\"40\" value=\"" + Ui.Attr(v.Name) + "\"" + ro + "></label><label class=\"as-field\"><span>Web address key</span><input type=\"text\" id=\"as-skey\" maxlength=\"30\" value=\"" + Ui.Attr(v.Id) + "\"" + (x != null ? " readonly" : ro) + " placeholder=\"for example pickleball\"></label><label class=\"as-field\"><span>Icon</span><select id=\"as-sicon\"" + (canE ? string.Empty : " disabled") + ">" + Ui.Opts(Blocks.Icons.Select(i => (i.Key, i.Name)), v.Icon == string.Empty ? "star" : v.Icon) + "</select></label></div><label class=\"as-field\"><span>Short line</span><input type=\"text\" id=\"as-sblurb\" maxlength=\"160\" value=\"" + Ui.Attr(v.Blurb) + "\"" + ro + "></label><label class=\"as-field\"><span>About this sport (paragraphs, simple HTML allowed: p, b, i, a)</span><textarea id=\"as-stext\" rows=\"5\" maxlength=\"4000\"" + ro + ">" + Ui.Enc(v.Text) + "</textarea></label><div class=\"as-f3\"><label class=\"as-field\"><span>Where it happens</span><input type=\"text\" id=\"as-splace\" maxlength=\"60\" value=\"" + Ui.Attr(v.Place) + "\"" + ro + "></label><label class=\"as-field\"><span>Order</span><input type=\"number\" id=\"as-sorder\" min=\"0\" max=\"99\" value=\"" + v.Order + "\"" + ro + "></label><label class=\"as-chk\" style=\"align-self:end;min-height:44px\"><input type=\"checkbox\" id=\"as-spub\"" + (x == null || v.Published ? " checked" : string.Empty) + (canE ? string.Empty : " disabled") + "> Show on the website</label></div><div class=\"as-pics\">" + PicField(site, "as-simg", "Main picture", v.Image, !canE) + PicField(site, "as-simg2", "Second picture", v.Image2, !canE) + "</div><em class=\"as-err\" id=\"as-e-sport\" role=\"alert\"></em></div><div class=\"as-mfoot\">");
            if (canE && x != null)
            {
                sb.Append("<button type=\"button\" class=\"as-btn as-btn-danger as-left\" onclick=\"AdminSportsJs.drop('" + Ui.Attr(x.Id) + "','" + Ui.Js(x.Name) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"as-btn as-btn-ghost\" onclick=\"AdminSportsJs.closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"as-btn as-btn-accent\" onclick=\"AdminSportsJs.save('" + Ui.Attr(x?.Id ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("as-mbody", sb.ToString());
            response.ExecuteScript("AdminSportsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSportsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "sport", GetDataValue("sid"), "You can look but not change sport types.");
            }
            string id = Ui.Clip(GetDataValue("sid"), 30);
            string key = Regex.Replace(Ui.Clip(GetDataValue("skey"), 30).ToLowerInvariant(), "[^a-z0-9-]", "-").Trim('-');
            string name = Ui.OneLine(GetDataValue("sname"), 40);
            string err = name.Length < 2 ? "Enter the sport's name." : string.Empty;
            string saved = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                SportType? x = site.Sports.FirstOrDefault(s => s.Id == id);
                if (err == string.Empty && x == null)
                {
                    if (key == string.Empty)
                    {
                        key = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                    }
                    if (key.Length < 2)
                    {
                        err = "Choose a web address key of 2 letters or more.";
                    }
                    else if (site.Sports.Any(s => s.Id == key))
                    {
                        err = "That key is already used by another sport.";
                    }
                    else
                    {
                        x = new SportType { Id = key };
                        site.Sports.Add(x);
                    }
                }
                if (err == string.Empty && x != null)
                {
                    x.Name = name;
                    x.Blurb = Ui.OneLine(GetDataValue("sblurb"), 160);
                    x.Text = Blocks.Sanitize(GetDataValue("stext"), 6000);
                    x.Icon = Blocks.Icons.Any(i => i.Key == GetDataValue("sicon")) ? GetDataValue("sicon") : "star";
                    x.Place = Ui.OneLine(GetDataValue("splace"), 60);
                    x.Image = PicOk(site, GetDataValue("simg"));
                    x.Image2 = PicOk(site, GetDataValue("simg2"));
                    x.Order = int.TryParse(GetDataValue("sorder"), out int o) ? Math.Clamp(o, 0, 99) : x.Order;
                    x.Published = GetDataValue("spub") == "1";
                    saved = x.Name;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("as-e-sport", err);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "sport", saved, true);
            response.ExecuteScript("AdminSportsJs.closeModal();AdminSportsJs.list(1);AdminSportsJs.toast('Saved');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSportsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete sport", GetDataValue("sid"), "You can look but not change sport types.");
            }
            string msg = string.Empty;
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                SportType? x = site.Sports.FirstOrDefault(s => s.Id == GetDataValue("sid"));
                if (x != null)
                {
                    if (site.Classes.Any(k => k.Sport == x.Id))
                    {
                        msg = x.Name + " has classes. Unpublish it instead.";
                    }
                    else
                    {
                        site.Sports.Remove(x);
                        name = x.Name;
                        await Store.SaveSite(site, c.Sbx);
                    }
                }
            }
            if (msg != string.Empty)
            {
                response.SetElementContents("as-e-sport", msg);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "delete sport", name, true);
            response.ExecuteScript("AdminSportsJs.closeModal();AdminSportsJs.list(1);AdminSportsJs.toast('Deleted');");
            return response;
        }
    }
}
