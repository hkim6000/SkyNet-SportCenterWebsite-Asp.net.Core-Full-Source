using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sport.Core;
using Sport.Models;
using SkyNet;

namespace Sport.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite(string.Empty);
            HtmlDoc.SetTitle(site.Settings.Name + " admin | Settings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", Ui.SafeColor(site.Settings.Accent) ? "--st-accent:" + site.Settings.Accent : string.Empty).Replace("{plhd_name}", Ui.Enc(site.Settings.Name));
        }

        private static void Chrome(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("st-menu", Auth.Menu("st", "AdminSettings", c, site));
            response.SetElementContents("st-who", Auth.Who("st", c));
            response.SetElementContents("st-demo", Auth.DemoPill("st", c));
            response.SetElementContents("st-bname", Ui.Enc(site.Settings.Name));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.Sbx, c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string title)
        {
            return "<div class=\"st-ptop\"><div><h1>" + Ui.Enc(title) + "</h1></div></div><div class=\"st-panel\"><p>You don't have access to this area. Ask the center manager to give you access in Users.</p></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminSettingsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await Store.LoadSite(c.Sbx);
            Chrome(response, c, site);
            if (AreaKey != string.Empty && !Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.Sbx, c.U.Id, "view", AreaKey, false);
                response.SetElementContents("st-main", NoAccess("Settings"));
                return response;
            }
            response.SetElementContents("st-main", await MainHtml(c, site, GetDataValue("arg")));
            response.ExecuteScript("AdminSettingsJs.loaded();");
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
            StringBuilder sb = new StringBuilder("<div class=\"st-tr st-th\">");
            foreach ((string n, string cls) in cols)
            {
                sb.Append("<span" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + ">" + n + "</span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static readonly string[] Zones = { "America/New_York", "America/Chicago", "America/Denver", "America/Phoenix", "America/Los_Angeles", "America/Anchorage", "Pacific/Honolulu", "America/Toronto", "America/Vancouver", "Europe/London", "Europe/Paris", "Europe/Berlin", "Asia/Tokyo", "Asia/Seoul", "Australia/Sydney", "Pacific/Auckland", "UTC" };

        private static string Field(string id, string label, string value, int max, string type = "text", string extra = "")
        {
            return "<label class=\"st-field\"><span>" + label + "</span><input type=\"" + type + "\" id=\"st-s" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\"" + extra + "></label>";
        }

        private static string Num(string id, string label, decimal value)
        {
            return Field(id, label, value.ToString("0.##", Ui.Inv), 8, "text", " inputmode=\"decimal\"");
        }

        private static string TextArea(string id, string label, string value, int max, int rows)
        {
            return "<label class=\"st-field\"><span>" + label + "</span><textarea id=\"st-s" + id + "\" rows=\"" + rows + "\" maxlength=\"" + max + "\">" + Ui.Enc(value) + "</textarea></label>";
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site, string arg)
        {
            Settings s = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"st-ptop\"><div><h1>Settings</h1><p>Center details, opening hours, member tiers and points, booking and refund rules, and the day pass. Page text and pictures are in Pages.</p></div></div><nav class=\"st-setnav\"><a href=\"#s-center\">Center</a><a href=\"#s-hours\">Opening hours</a><a href=\"#s-tiers\">Tiers and points</a><a href=\"#s-rules\">Booking and refunds</a><a href=\"#s-pass\">Day pass</a></nav><form onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"st-panel\" id=\"s-center\"><h2>Center</h2><div class=\"st-form\"><div class=\"st-f3\">" + Field("name", "Name", s.Name, 40) + Field("town", "Town", s.Town, 40) + Field("tag", "Tagline", s.Tagline, 160) + "</div><div class=\"st-f3\">" + Field("addr", "Address", s.Address, 100) + Field("phone", "Phone", s.Phone, 30, "tel") + Field("email", "Email", s.Email, 80, "email") + "</div><div class=\"st-f4\"><label class=\"st-field\"><span>Accent colour</span><input type=\"color\" id=\"st-saccent\" value=\"" + Ui.Attr(Ui.SafeColor(s.Accent) ? s.Accent : "#A6E22E") + "\"></label><label class=\"st-field\"><span>Website theme</span><select id=\"st-stheme\">" + Ui.Opts(new[] { ("light", "Light"), ("dark", "Dark") }, s.Theme) + "</select></label></div></div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-hours\"><h2>Opening hours and time zone</h2><div id=\"st-hours\">");
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay o = s.Hours.FirstOrDefault(h => h.Day == d) ?? new OpenDay { Day = d };
                sb.Append("<div class=\"st-orow" + (o.Open ? string.Empty : " st-shut") + "\" data-day=\"" + d + "\"><b>" + Ui.DayNames[d] + "</b><label><input type=\"checkbox\"" + (o.Open ? " checked" : string.Empty) + " onchange=\"AdminSettingsJs.dayOn(this)\" aria-label=\"Open on " + Ui.DayNames[d] + "\"></label><input type=\"time\" step=\"900\" value=\"" + Ui.Attr(o.Start) + "\" aria-label=\"Opens\"><input type=\"time\" step=\"900\" value=\"" + Ui.Attr(o.End) + "\" aria-label=\"Closes\"></div>");
            }
            List<(string, string)> zones = Zones.Select(z => (z, z.Replace('_', ' '))).ToList();
            if (!Zones.Contains(s.TimeZone))
            {
                zones.Insert(0, (s.TimeZone, s.TimeZone));
            }
            sb.Append("</div><p class=\"st-hint\" style=\"margin:8px 0 12px\">Opening hours show on every page and decide which days a day pass can be bought for.</p><div class=\"st-f4\"><label class=\"st-field\"><span>Time zone (now " + Ui.Hm(site, Store.Now(site)) + ")</span><select id=\"st-stz\">" + Ui.Opts(zones, s.TimeZone) + "</select></label><label class=\"st-chk\" style=\"align-self:end;min-height:44px\"><input type=\"checkbox\" id=\"st-sh24\"" + (s.Clock24 ? " checked" : string.Empty) + "> 24-hour clock</label></div></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-tiers\"><h2>Member tiers and points</h2><h3>Lifetime points needed</h3><div class=\"st-f4\"><label class=\"st-field\"><span>Bronze</span><input type=\"text\" value=\"0, from sign-up\" readonly></label>" + Num("silver", "Silver", s.SilverAt) + Num("gold", "Gold", s.GoldAt) + Num("diamond", "Diamond", s.DiamondAt) + "</div><h3>Class discount (%)</h3><div class=\"st-f4\">" + Num("offb", "Bronze", s.OffBronze) + Num("offs", "Silver", s.OffSilver) + Num("offg", "Gold", s.OffGold) + Num("offd", "Diamond", s.OffDiamond) + "</div><h3>Early registration</h3><div class=\"st-f4\">" + Num("earlyh", "Hours before registration opens", s.EarlyHours) + "<label class=\"st-field\"><span>From which tier</span><select id=\"st-searlyf\">" + Ui.Opts(new[] { ("silver", "Silver and up"), ("gold", "Gold and up"), ("diamond", "Diamond only"), ("none", "Nobody") }, s.EarlyHours > 0 ? s.EarlyFrom : "none") + "</select></label></div><h3>Earning and spending</h3><div class=\"st-f4\">" + Num("perd", "Points per $1 paid", s.PerDollar) + Num("pers", "Points per session attended", s.PerSession) + Num("ppd", "Points for $1 off", s.PointsPerDollar) + Num("pmax", "Points can pay up to (% of a class)", s.PointsMaxPct) + "</div><p class=\"st-hint\">Tiers follow lifetime points and never drop. Open members (day pass only) don't earn points. Refunds take back the points the payment earned.</p></section>");
            sb.Append("<section class=\"st-panel\" id=\"s-rules\"><h2>Booking and refund rules</h2><div class=\"st-f4\">" + Num("fullh", "Full refund until (hours before start)", s.FullRefundHours) + Num("latep", "Refund after that (%)", s.LatePct) + Num("cancelh", "Online cancellation closes (hours before)", s.CancelHours) + Num("startp", "Refund after the class started (%)", s.StartedPct) + "</div><div class=\"st-f4\">" + Num("offerh", "Hours to pay a waitlist offer", s.OfferHours) + Num("deskh", "Hours a pay-at-desk seat is held", s.DeskHours) + Num("few", "Show Few seats at or below", s.FewSeats) + "</div><div class=\"st-preview\">Members see: <b>" + s.FullRefundHours + "+ hours</b> before the first session full refund, <b>" + s.CancelHours + "&ndash;" + s.FullRefundHours + " hours</b> " + s.LatePct + "%, after that they ask the front desk.</div>" + TextArea("waiver", "Waiver text members accept when they register", s.Waiver, 1500, 4) + "</section>");
            sb.Append("<section class=\"st-panel\" id=\"s-pass\"><h2>Day pass</h2><div class=\"st-f3\">" + Field("pname", "Name", s.PassName, 40) + Num("pprice", "Price ($)", s.PassPrice) + "</div>" + TextArea("pnote", "What it includes", s.PassNote, 300, 3) + "</section>");
            sb.Append("<div class=\"st-savebar\"><em class=\"st-err\" id=\"st-e-set\" role=\"alert\"></em><button type=\"submit\" class=\"st-btn st-btn-accent\">Save settings</button></div></form>");
            return sb.ToString();
        }

        private static decimal Dec(string v, decimal min, decimal max, decimal cur)
        {
            return decimal.TryParse((v ?? string.Empty).Replace(",", string.Empty).Replace("$", string.Empty).Replace("%", string.Empty).Trim(), NumberStyles.Number, Ui.Inv, out decimal d) ? Math.Clamp(d, min, max) : cur;
        }

        private static int Int(string v, int min, int max, int cur)
        {
            return (int)Math.Round(Dec(v, min, max, cur));
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "settings", "settings", "Only the center manager changes settings.");
            }
            string name = Regex.Replace(Ui.Clip(GetDataValue("sname"), 40), "\\s+", " ").Trim();
            string email = Ui.Clip(GetDataValue("semail"), 80).Trim();
            string tz = Ui.Clip(GetDataValue("stz"), 60);
            int silver = Int(GetDataValue("ssilver"), 1, 1000000, 500);
            int gold = Int(GetDataValue("sgold"), 1, 1000000, 1500);
            int diamond = Int(GetDataValue("sdiamond"), 1, 1000000, 4000);
            int fullh = Int(GetDataValue("sfullh"), 0, 720, 48);
            int cancelh = Int(GetDataValue("scancelh"), 0, 720, 24);
            string err = name.Length < 2 ? "Enter the center name." : email != string.Empty && !Ui.Email(email) ? "Check the email address." : !Store.KnownZone(tz) ? "Choose a time zone." : !(silver < gold && gold < diamond) ? "Tier points must go up: Silver below Gold below Diamond." : cancelh > fullh ? "Online cancellation must close at or after the full refund window ends." : string.Empty;
            List<OpenDay> hours = new List<OpenDay>();
            foreach (string row in GetDataValue("shours").Split('~'))
            {
                string[] x = row.Split('|');
                if (x.Length != 4 || !int.TryParse(x[0], out int d) || d < 0 || d > 6 || Store.Hm(x[2]) == null || Store.Hm(x[3]) == null)
                {
                    continue;
                }
                OpenDay o = new OpenDay { Day = d, Open = x[1] == "1", Start = x[2], End = x[3] };
                if (o.Open && string.CompareOrdinal(o.Start, o.End) >= 0 && err == string.Empty)
                {
                    err = Ui.DayNames[d] + ": closing time must be after opening time.";
                }
                hours.Add(o);
            }
            if (err != string.Empty)
            {
                response.SetElementContents("st-e-set", err);
                return response;
            }
            string accent = Ui.SafeColor(GetDataValue("saccent")) ? GetDataValue("saccent") : "#A6E22E";
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(c.Sbx);
                Settings s = site.Settings;
                s.Name = name;
                s.Town = Ui.Clip(GetDataValue("stown"), 40).Trim();
                s.Tagline = Ui.Clip(GetDataValue("stag"), 160).Trim();
                s.Address = Ui.Clip(GetDataValue("saddr"), 100).Trim();
                s.Phone = Ui.CleanPhone(GetDataValue("sphone"));
                s.Email = email;
                s.Accent = accent;
                s.Theme = GetDataValue("stheme") == "dark" ? "dark" : "light";
                s.TimeZone = tz;
                s.Clock24 = GetDataValue("sh24") == "1";
                if (hours.Count == 7)
                {
                    s.Hours = hours.OrderBy(h => h.Day).ToList();
                }
                s.SilverAt = silver;
                s.GoldAt = gold;
                s.DiamondAt = diamond;
                s.OffBronze = Dec(GetDataValue("soffb"), 0, 50, s.OffBronze);
                s.OffSilver = Dec(GetDataValue("soffs"), 0, 50, s.OffSilver);
                s.OffGold = Dec(GetDataValue("soffg"), 0, 50, s.OffGold);
                s.OffDiamond = Dec(GetDataValue("soffd"), 0, 50, s.OffDiamond);
                string ef = GetDataValue("searlyf");
                s.EarlyHours = ef == "none" ? 0 : Int(GetDataValue("searlyh"), 0, 336, s.EarlyHours);
                s.EarlyFrom = ef is "silver" or "gold" or "diamond" ? ef : s.EarlyFrom;
                s.PerDollar = Int(GetDataValue("sperd"), 0, 100, s.PerDollar);
                s.PerSession = Int(GetDataValue("spers"), 0, 1000, s.PerSession);
                s.PointsPerDollar = Int(GetDataValue("sppd"), 1, 100000, s.PointsPerDollar);
                s.PointsMaxPct = Int(GetDataValue("spmax"), 0, 100, s.PointsMaxPct);
                s.FullRefundHours = fullh;
                s.CancelHours = cancelh;
                s.LatePct = Int(GetDataValue("slatep"), 0, 100, s.LatePct);
                s.StartedPct = Int(GetDataValue("sstartp"), 0, 100, s.StartedPct);
                s.OfferHours = Int(GetDataValue("sofferh"), 1, 168, s.OfferHours);
                s.DeskHours = Int(GetDataValue("sdeskh"), 1, 336, s.DeskHours);
                s.FewSeats = Int(GetDataValue("sfew"), 0, 50, s.FewSeats);
                s.Waiver = Ui.Clip(GetDataValue("swaiver"), 1500).Trim();
                s.PassName = Ui.OneLine(GetDataValue("spname"), 40) is string pn && pn.Length >= 2 ? pn : s.PassName;
                s.PassPrice = Dec(GetDataValue("spprice"), 0, 1000, s.PassPrice);
                s.PassNote = Ui.Clip(GetDataValue("spnote"), 300).Trim();
                await Store.SaveSite(site, c.Sbx);
            }
            await Store.Log(c.Sbx, c.U.Id, "settings", name, true);
            response.ExecuteScript("AdminSettingsJs.toast('Settings saved');document.getElementById('st-site').style.setProperty('--st-accent','" + accent + "');");
            return response;
        }
    }
}
