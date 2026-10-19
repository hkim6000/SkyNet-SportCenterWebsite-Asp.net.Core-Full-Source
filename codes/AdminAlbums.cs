using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminAlbums : WebPage
    {
        private const string AreaKey = "media";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Albums");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--aa-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("aa-menu", Auth.Menu("aa", "AdminAlbums", c, site));
            response.SetElementContents("aa-who", Auth.Who("aa", c));
            response.SetElementContents("aa-demo", Auth.DemoPill("aa", c));
            response.SetElementContents("aa-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminAlbumsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"aa-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"aa-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAlbumsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAlbumsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("aa-main", NoAccess("Albums"));
                return response;
            }
            response.SetElementContents("aa-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminAlbumsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"aa-tr aa-th\">");
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
            return "<button type=\"button\" class=\"aa-pki\" data-src=\"" + Ui.Attr(m.Full) + "\" data-thumb=\"" + Ui.Attr(m.Thumb) + "\" data-kind=\"" + m.Kind + "\" data-title=\"" + Ui.Attr(m.Title) + "\" onclick=\"AdminAlbumsJs.pkToggle(this)\" title=\"" + Ui.Attr(m.Title + (m.Tags.Count > 0 ? " · " + string.Join(", ", m.Tags) : string.Empty)) + "\"><img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<b class=\"aa-vbadge\">" + (m.Seconds / 60) + ":" + (m.Seconds % 60).ToString("00", Ui.Inv) + "</b>" : string.Empty) + "<span>" + Ui.Enc(m.Title) + "</span><i>&#10003;</i></button>";
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAlbumsJs.expired();");
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
                sb.Append("<p class=\"aa-none\" style=\"grid-column:1/-1\">Nothing matches. Upload a photo, or add videos in Media.</p>");
            }
            string tags = page == 1 ? JsonSerializer.Serialize(site.Media.SelectMany(m => m.Tags).Distinct().OrderBy(t => t).ToList()) : "null";
            response.ExecuteScript("AdminAlbumsJs.pkShown(" + JsonSerializer.Serialize(sb.ToString()) + "," + (all.Count > page * size ? "true" : "false") + "," + tags + "," + (page > 1 ? "true" : "false") + ");");
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
                response.ExecuteScript("AdminAlbumsJs.expired();");
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
                response.ExecuteScript("AdminAlbumsJs.pkUploaded(false,'That file is not a JPG, PNG or WebP photo, or it is too large.');");
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
            response.ExecuteScript("AdminAlbumsJs.pkUploaded(true,'');");
            return response;
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            bool canE = Access.Can(c.U, AreaKey, 'E');
            return "<div class=\"aa-ptop\"><div><h1>Albums</h1><p>Photo albums on the Media Center page. Each album picks photos from the Media library; deleting an album keeps the photos.</p></div><div class=\"aa-acts\"><a class=\"aa-btn aa-btn-ghost\" href=\"AdminMedia\">Back to Media</a>" + (canE ? "<button type=\"button\" class=\"aa-btn aa-btn-accent\" onclick=\"AdminAlbumsJs.edit('')\">New album</button>" : string.Empty) + "<a class=\"aa-btn aa-btn-ghost\" href=\"Media\" target=\"_blank\" rel=\"noopener\">View on site</a></div></div><div class=\"aa-albs\" id=\"aa-list\"></div>";
        }

        private static string Thumb(SiteData site, string src)
        {
            return site.Media.FirstOrDefault(m => m.Full == src)?.Thumb ?? Ui.Thumb(src);
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAlbumsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            StringBuilder sb = new StringBuilder();
            if (site.Albums.Count == 0)
            {
                sb.Append("<p class=\"aa-none\">No albums yet.</p>");
            }
            foreach (Album a in site.Albums.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id.Length).ThenByDescending(a => a.Id, StringComparer.Ordinal))
            {
                string cov = a.Cover != string.Empty ? a.Cover : a.Items.FirstOrDefault() ?? string.Empty;
                sb.Append("<button type=\"button\" class=\"aa-albc\" onclick=\"AdminAlbumsJs.edit('" + Ui.Attr(a.Id) + "')\"><span>" + (cov != string.Empty ? "<img src=\"" + Ui.Attr(Thumb(site, cov)) + "\" alt=\"\" loading=\"lazy\">" : string.Empty) + "<em>" + Ui.Plural(a.Items.Count, "photo", "photos") + "</em></span><span><b>" + Ui.Enc(a.Title) + "</b><small>" + Ui.Day(a.Date) + "</small><span><span class=\"aa-st aa-st-" + (a.Published ? "live\">Published" : "off\">Hidden") + "</span></span></span></button>");
            }
            response.SetElementContents("aa-list", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAlbumsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "album form", GetDataValue("alid"), "You don't have access to media.");
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Album? a = site.Albums.FirstOrDefault(x => x.Id == GetDataValue("alid"));
            Album v = a ?? new Album { Date = Store.Today(site), Published = true };
            bool canE = Access.Can(c.U, AreaKey, 'E');
            string ro = canE ? string.Empty : " readonly";
            var list = v.Items.Select(src => new { src, thumb = Thumb(site, src), title = site.Media.FirstOrDefault(m => m.Full == src)?.Title ?? string.Empty }).ToList();
            StringBuilder sb = new StringBuilder("<h2>" + (a == null ? "New album" : Ui.Enc(a.Title)) + "</h2><div class=\"aa-form\"><div class=\"aa-f2\"><label class=\"aa-field\"><span>Title</span><input type=\"text\" id=\"aa-altitle\" maxlength=\"60\" value=\"" + Ui.Attr(v.Title) + "\"" + ro + "></label><label class=\"aa-field\"><span>Date</span><input type=\"date\" id=\"aa-aldate\" value=\"" + Ui.Ymd(v.Date) + "\"" + ro + "></label></div><label class=\"aa-field\"><span>Short line</span><input type=\"text\" id=\"aa-altext\" maxlength=\"160\" value=\"" + Ui.Attr(v.Text) + "\"" + ro + "></label><label class=\"aa-chk\"><input type=\"checkbox\" id=\"aa-alpub\"" + (v.Published ? " checked" : string.Empty) + (canE ? string.Empty : " disabled") + "> Show on the website</label><div class=\"aa-ph\"><h3 style=\"margin:6px 0 0\">Photos &middot; <span id=\"aa-acount\"></span></h3>" + (canE ? "<button type=\"button\" class=\"aa-btn aa-btn-sm\" onclick=\"AdminAlbumsJs.addPhotos()\">Add photos</button>" : string.Empty) + "</div><div class=\"aa-agrid\" id=\"aa-aitems\"></div><em class=\"aa-err\" id=\"aa-e-album\" role=\"alert\"></em></div><div class=\"aa-mfoot\">");
            if (canE && a != null)
            {
                sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-danger aa-left\" onclick=\"AdminAlbumsJs.drop('" + Ui.Attr(a.Id) + "','" + Ui.Js(a.Title) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-ghost\" onclick=\"AdminAlbumsJs.closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"aa-btn aa-btn-accent\" onclick=\"AdminAlbumsJs.save('" + Ui.Attr(a?.Id ?? string.Empty) + "')\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("aa-mbody", sb.ToString());
            response.ExecuteScript("AdminAlbumsJs.setup(" + JsonSerializer.Serialize(list) + ",'" + Ui.Js(v.Cover) + "'," + (canE ? "true" : "false") + ");AdminAlbumsJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAlbumsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "album", GetDataValue("alid"), "You can look but not change albums.");
            }
            string id = Ui.Clip(GetDataValue("alid"), 20);
            string title = Ui.OneLine(GetDataValue("altitle"), 60);
            List<string> srcs;
            try
            {
                srcs = JsonSerializer.Deserialize<List<string>>(Ui.Clip(GetDataValue("alitems"), 60000)) ?? new List<string>();
            }
            catch (JsonException)
            {
                srcs = new List<string>();
            }
            string err = title.Length < 2 ? "Enter the album's title." : string.Empty;
            string saved = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                List<string> ok = srcs.Where(s => site.Media.Any(m => m.Full == s && m.Kind == "photo")).Distinct().Take(200).ToList();
                if (err == string.Empty && ok.Count == 0 && GetDataValue("alpub") == "1")
                {
                    err = "Add at least one photo, or untick Show on the website.";
                }
                Album? a = site.Albums.FirstOrDefault(x => x.Id == id);
                if (err == string.Empty)
                {
                    if (a == null)
                    {
                        a = new Album { Id = Store.NextId(site.Albums.Select(x => x.Id), "A-", 0), By = c.U.Id };
                        site.Albums.Add(a);
                    }
                    a.Title = title;
                    a.Text = Ui.OneLine(GetDataValue("altext"), 160);
                    a.Date = Ui.ParseDay(GetDataValue("aldate")) ?? a.Date;
                    a.Published = GetDataValue("alpub") == "1";
                    a.Items = ok;
                    a.Cover = ok.Contains(GetDataValue("alcover")) ? GetDataValue("alcover") : string.Empty;
                    saved = a.Title;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("aa-e-album", err);
                return response;
            }
            await Store.Log(c.Sbx, c.U.Id, "album", saved, true);
            response.ExecuteScript("AdminAlbumsJs.closeModal();AdminAlbumsJs.list(1);AdminAlbumsJs.toast('Saved');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAlbumsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete album", GetDataValue("alid"), "You can look but not change albums.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Album? a = site.Albums.FirstOrDefault(x => x.Id == GetDataValue("alid"));
                if (a != null)
                {
                    site.Albums.Remove(a);
                    name = a.Title;
                    await Store.SaveSite(site, c.Sbx);
                }
            }
            await Store.Log(c.Sbx, c.U.Id, "delete album", name, true);
            response.ExecuteScript("AdminAlbumsJs.closeModal();AdminAlbumsJs.list(1);AdminAlbumsJs.toast('Deleted');");
            return response;
        }
    }
}
