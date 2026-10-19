namespace Sport.Models
{
    public class SiteData
    {
        public DateTime Anchor { get; set; }
        public Settings Settings { get; set; } = new Settings();
        public List<SportType> Sports { get; set; } = new List<SportType>();
        public List<Coach> Instructors { get; set; } = new List<Coach>();
        public List<SportClass> Classes { get; set; } = new List<SportClass>();
        public List<ClubMember> Members { get; set; } = new List<ClubMember>();
        public List<Registration> Registrations { get; set; } = new List<Registration>();
        public List<Payment> Payments { get; set; } = new List<Payment>();
        public List<DayPass> Passes { get; set; } = new List<DayPass>();
        public List<Visit> Visits { get; set; } = new List<Visit>();
        public List<PointEntry> Points { get; set; } = new List<PointEntry>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();
        public List<Album> Albums { get; set; } = new List<Album>();
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class Settings
    {
        public string Name { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Accent { get; set; } = "#A6E22E";
        public string Theme { get; set; } = "light";
        public bool Clock24 { get; set; }
        public string TimeZone { get; set; } = "America/Chicago";
        public List<OpenDay> Hours { get; set; } = new List<OpenDay>();
        public string PassName { get; set; } = "Day pass";
        public decimal PassPrice { get; set; } = 18m;
        public string PassNote { get; set; } = string.Empty;
        public int SilverAt { get; set; } = 500;
        public int GoldAt { get; set; } = 1500;
        public int DiamondAt { get; set; } = 4000;
        public decimal OffBronze { get; set; }
        public decimal OffSilver { get; set; } = 3m;
        public decimal OffGold { get; set; } = 5m;
        public decimal OffDiamond { get; set; } = 10m;
        public int EarlyHours { get; set; } = 48;
        public string EarlyFrom { get; set; } = "gold";
        public int PerDollar { get; set; } = 1;
        public int PerSession { get; set; } = 10;
        public int PointsPerDollar { get; set; } = 100;
        public int PointsMaxPct { get; set; } = 50;
        public int OfferHours { get; set; } = 24;
        public int DeskHours { get; set; } = 48;
        public int CancelHours { get; set; } = 24;
        public int FullRefundHours { get; set; } = 48;
        public int LatePct { get; set; } = 50;
        public int StartedPct { get; set; }
        public int FewSeats { get; set; } = 3;
        public string Waiver { get; set; } = string.Empty;
    }

    public class OpenDay
    {
        public int Day { get; set; }
        public bool Open { get; set; }
        public string Start { get; set; } = "06:00";
        public string End { get; set; } = "22:00";
    }

    public class SportType
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Image2 { get; set; } = string.Empty;
        public string Place { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool Published { get; set; } = true;
    }

    public class Coach
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Sports { get; set; } = new List<string>();
        public List<string> Certs { get; set; } = new List<string>();
        public int Since { get; set; }
        public int Order { get; set; }
        public bool Published { get; set; } = true;
    }

    public class SportClass
    {
        public string Id { get; set; } = string.Empty;
        public string Sport { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Level { get; set; } = "all";
        public int AgeMin { get; set; }
        public int AgeMax { get; set; } = 99;
        public string Instructor { get; set; } = string.Empty;
        public string Place { get; set; } = string.Empty;
        public List<int> Days { get; set; } = new List<int>();
        public string Start { get; set; } = "18:00";
        public int Minutes { get; set; } = 60;
        public DateTime First { get; set; }
        public int Count { get; set; } = 8;
        public List<DateTime> Dates { get; set; } = new List<DateTime>();
        public DateTime? Opens { get; set; }
        public decimal Price { get; set; }
        public int Capacity { get; set; } = 12;
        public bool Published { get; set; } = true;
        public string Status { get; set; } = "active";
        public DateTime? Cancelled { get; set; }
        public string CancelReason { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Bring { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }

    public class ClubMember
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "open";
        public string Status { get; set; } = "active";
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime? Born { get; set; }
        public string Hash { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? Joined { get; set; }
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public List<FamilyMember> Family { get; set; } = new List<FamilyMember>();
        public int Lifetime { get; set; }
        public int Balance { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime? Suspended { get; set; }
        public string SuspendReason { get; set; } = string.Empty;
        public string Reset { get; set; } = string.Empty;
        public DateTime? ResetExp { get; set; }
        public bool Sample { get; set; }
    }

    public class FamilyMember
    {
        public string Id { get; set; } = string.Empty;
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public DateTime Born { get; set; }
        public string Relation { get; set; } = "child";
    }

    public class Registration
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public string Member { get; set; } = string.Empty;
        public string Person { get; set; } = "self";
        public string Name { get; set; } = string.Empty;
        public DateTime? Born { get; set; }
        public string Status { get; set; } = "confirmed";
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public string Tier { get; set; } = string.Empty;
        public int Points { get; set; }
        public decimal PointsOff { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Refunded { get; set; }
        public string Method { get; set; } = string.Empty;
        public bool Waiver { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Confirmed { get; set; }
        public DateTime? Offered { get; set; }
        public DateTime? OfferExp { get; set; }
        public DateTime? HoldExp { get; set; }
        public DateTime? Ended { get; set; }
        public string EndedBy { get; set; } = string.Empty;
        public string EndNote { get; set; } = string.Empty;
        public RefundAsk? Ask { get; set; }
        public string By { get; set; } = "web";
    }

    public class RefundAsk
    {
        public DateTime When { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "open";
        public decimal Amount { get; set; }
        public DateTime? Decided { get; set; }
        public string By { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class Payment
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "charge";
        public string Reg { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string Member { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = "card";
        public string Brand { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public string Status { get; set; } = "paid";
        public DateTime When { get; set; }
        public string By { get; set; } = "web";
        public string Note { get; set; } = string.Empty;
        public string Of { get; set; } = string.Empty;
    }

    public class DayPass
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Member { get; set; } = string.Empty;
        public DateTime Day { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = "valid";
        public DateTime Bought { get; set; }
        public string Method { get; set; } = "card";
        public string By { get; set; } = "web";
    }

    public class Visit
    {
        public string Id { get; set; } = string.Empty;
        public string Member { get; set; } = string.Empty;
        public string Kind { get; set; } = "class";
        public string Reg { get; set; } = string.Empty;
        public int Session { get; set; }
        public string Pass { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime In { get; set; }
        public DateTime? Out { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PointEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Member { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public int Points { get; set; }
        public string Kind { get; set; } = "pay";
        public string Reg { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string By { get; set; } = "web";
    }

    public class Message
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Status { get; set; } = "new";
        public DateTime Created { get; set; }
        public List<Note> Notes { get; set; } = new List<Note>();
    }

    public class Note
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool Demo { get; set; }
        public string Instructor { get; set; } = string.Empty;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Activity
    {
        public DateTime When { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string Sbx { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Kind { get; set; } = "staff";
        public DateTime Exp { get; set; }
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Title { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string Full { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public int Seconds { get; set; }
        public int Pages { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Bytes { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime When { get; set; }
    }

    public class Album
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Cover { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new List<string>();
        public DateTime Date { get; set; }
        public bool Published { get; set; } = true;
        public string By { get; set; } = string.Empty;
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public string Extra { get; set; } = string.Empty;
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
