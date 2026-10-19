# Sport — SkyNet Sports Center Template

A responsive website for any sports center, club or academy: sports, classes with online registration and payment, instructors, member accounts with points and tiers, day passes, a media center, and a full admin with a class board, refunds and staff permissions, built for **ASP.NET Core (.NET 10)** on the **SkyNet Framework**.
It is universal: rename the center, set your sports, classes, prices, tiers and refund rules, and the whole site follows. Data is stored in JSON files, with no database.
Free and open source. **100% AI-driven coding — built by Claude.**

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/TheSkyLite.SkyNet?label=TheSkyLite.SkyNet)](https://www.nuget.org/packages/TheSkyLite.SkyNet)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![AI-driven](https://img.shields.io/badge/code-100%25%20AI--driven-1F7A74)](#100-ai-driven-coding)

**Live demo:** https://www.theskylite.com/Sport. Sign in as the sample member `olivia.grant@example.com` / `demo`, register for a class, then open **Staff sign in** in the footer and pick a role.

![Sport home page](docs/screenshot-home.png)

> **Fictional sports center.** Brightwater Sports Center, Marlow Heights, and every coach, member, class and price are invented. Card payments are a demo: nothing is charged. The photographs, portraits, videos and PDF documents are painted in code; there are no stock images.

---

## Features

### Website (15 pages)

- **Home:** a navy hero with a class finder (sport, who it is for, day), the eight sports, classes with seats starting soon, three ways to get started, the member tiers, the coaches and a day pass band. Every section is edited in **Pages**.
- **Open now / closed now** bar on every page, worked out in the center's own time zone.
- **Sports** and **Sport** pages: what the sport is, where it happens, two pictures and its open classes.
- **Classes:** filter by sport, age, level and day, and show finished or cancelled classes when you want them. Each card shows the schedule, seats left and the price.
- **Class:** sessions list, coach, what to bring, the cancellation rule and the registration panel.
  - **Who is taking the class:** you or a family member. Ages are checked against the class at its first session.
  - **Points:** spend 100 points for $1 off, up to 50% of the class. Your tier discount is applied first.
  - **Pay now by card** (confirmed right away) or **Pay at the front desk** (the seat is held for 48 hours).
  - **Waiver** checkbox and a **printable receipt**.
  - **Full class:** join the waitlist for free. When a seat opens, the next person gets an offer with 24 hours to pay.
  - **Gold and Diamond** members can register 48 hours before everyone else.
- **My registration:** look up a registration with its code and email, no account needed. Cancel online or ask for a refund.
- **Instructors** and **Instructor** pages: bio, certifications and their classes.
- **Media Center:** photo albums with a lightbox, videos and PDF documents (program guide, pool rules, waiver form).
- **About** (facts and figures from your data) and **Contact** (address, phone, opening hours and a message form).
- **Day pass:** buy a pass for today or another open day with just a name, email and phone, by card or at the desk.
- **Sign up**, **Sign in** and **Forgot password** (the reset code is shown on screen in this template).
- **Member area** with tabs:
  - **Overview:** next sessions, tier and points, quick links.
  - **My classes:** pay a held seat or a waitlist offer, cancel, leave the waitlist, ask for a refund, receipts.
  - **Family:** add children or a partner to book for them.
  - **Check-ins, Payments** (with receipts) and **Points** history.
  - **Profile:** contact details, billing address and password.
- **Light and dark theme.** Responsive layouts for desktop, tablet and phone.

### Members, tiers and points

- **One record per email.** A day pass buyer becomes an **open member** (name, phone, email, check-ins, no points). Signing up later with the same email turns the record into a **member** and keeps the history.
- **Billing address** is asked once, with the first card payment.
- **Tiers by lifetime points:** Bronze 0, Silver 500, Gold 1,500, Diamond 4,000, with 0 / 3 / 5 / 10% off every class.
- **Earning:** 1 point per $1 paid and 10 points for every session attended. Lifetime points never drop.
- **Refunds** take back the points the payment earned and return the points that were spent, in proportion.
- All numbers are in **Settings**.

### Registration statuses and refunds

`Pay at desk` → `Confirmed`, `Waitlist` → `Seat offered` → `Confirmed`, or `Cancelled` / `Expired` (an unpaid hold or offer). Each registration keeps what was paid and refunded.
Classes show `Open`, `Few seats`, `Full`, `In progress`, `Completed`, `Cancelled` or `Draft`, worked out from the dates and seats.

| When the member cancels | Refund |
|---|---|
| 48 hours or more before the first session | 100% |
| Between 48 and 24 hours before | 50% |
| Less than 24 hours before | online cancellation closes; they send a **refund request** |
| After the class started | 0% (staff can still refund by hand) |

Expired desk holds and waitlist offers are released on their own, and the freed seat is offered to the next person.

### Admin (17 pages)

- **Sign in** with your own password (PBKDF2). Failed attempts are throttled.
- **Dashboard:** sessions today, people in the building, revenue this month, new members, waitlist and refund requests; today's sessions (or the next ones); a **needs attention** list and the latest payments.
- **Sport types** and **Instructors:** text, icon, pictures from Media, order and publishing. An instructor can be linked to a staff login.
- **Classes:** schedule (days, time, length, first date, number of sessions), ages, level, price, seats, registration opening, pictures and a preview of the sessions. **Cancel class** refunds everyone in full and tells them why.
- **Class board:** pick a class and a session, then **check in**, **check out** and **undo**; attendance per session and the waitlist with **Offer seat**.
- **Registrations:** search and filter, register someone at the desk (member or family), take a desk payment, cancel with the refund rule, or override the limits.
- **Payments & refunds:** every charge and refund with totals, a manual refund, **refund requests** to approve (with an amount and a note, or keep them in the class) or decline, and **CSV export**.
- **Members:** filter by account type, tier and status. A record shows profile, points ledger, family, classes, check-ins and day passes.
  - Change the account type, **suspend** or reactivate, **adjust points** with a reason.
  - **Front desk check-in** by day pass code, the list of people in the building, and **sell a day pass** to a walk-in.
- **Messages** from the contact form: reply by email, mark replied or closed, notes.
- **Media:** photos, videos (chunked upload) and PDFs with tags and "used on". **Albums** pick photos from the library, with a cover and order.
- **Pages:** a block editor for every public page (text, pictures, buttons, lists of your data), with versions.
- **Users:** a permission matrix per person; **Settings**; **Activity log**; **My account**.

### Permissions

| Area | Rights | Extra right |
|---|---|---|
| Sport types, Instructors, Messages, Pages | None, View, Edit | |
| Classes | None, View, Edit | **Cancel class** |
| Class board | None, View, Edit | |
| Registrations | None, View, Edit | **Override limits** |
| Payments & refunds | None, View, Edit | **Refund** |
| Members | None, View, Edit | **Adjust points** |
| Media (and Albums) | None, View, Edit | **Delete** |
| Activity log | None, View | |
| Users, Settings | center managers only | |

- **Own classes only:** a staff login linked to an instructor sees only that instructor's classes on the Class board, in Classes and on the dashboard.
- The menu shows only the areas a person can see.
- Every check runs on the server. A request the user isn't allowed to make is refused and written to the activity log, even if someone crafts it by hand.

### Live demo mode

With `app.app.runmode | 0` the sign-in page lists the staff. Everyone signs in with the password `demo`.

| User | Role | Can manage |
|---|---|---|
| **morgan** | Center manager | everything, plus users and settings |
| **jamie** | Front desk lead | class board, registrations, payments, members and messages (edit); sport types, classes and instructors (view) |
| **nadia** | Program coordinator | sport types, classes (+ cancel class), class board, instructors, registrations (+ override limits) (edit); payments, members, media, pages and the activity log (view) |
| **leo** | Basketball coach | own classes only: class board (edit); classes and instructors (view) |
| **sam** | Media editor | media (+ delete) and pages (edit); sport types and instructors (view) |

- **Private copy:** each visitor gets their own copy of the center for one hour, shared by the website and the admin in the same browser. A registration made on the website shows on the class board straight away.
- **Sample member:** `olivia.grant@example.com` / `demo` (Gold, two children). Every sample member uses the password `demo`.
- **Test cards:** `4242 4242 4242 4242` is accepted, `4000 0000 0000 0002` is declined.
- **Dates:** classes and history move forward a whole week at a time, so weekdays stay right and there is always something running.

![Desktop, tablet and phone](docs/screenshot-responsive.png)

![Classes](docs/screenshot-classes.png)

![Sport page](docs/screenshot-sport.png)

![Register for a class](docs/screenshot-register.png)

![Member area](docs/screenshot-member.png)

![Points and tiers](docs/screenshot-member-points.png)

![Day pass](docs/screenshot-day-pass.png)

![Media Center](docs/screenshot-media.png)

![Staff sign in](docs/screenshot-admin-login.png)

![Dashboard](docs/screenshot-admin-dashboard.png)

![Class board](docs/screenshot-admin-board.png)

![Classes](docs/screenshot-admin-classes.png)

![Member record](docs/screenshot-admin-member.png)

![Refund request](docs/screenshot-admin-refund.png)

![Users: permissions by area](docs/screenshot-admin-permissions.png)

![Settings: tiers, points and refund rules](docs/screenshot-admin-settings.png)

![Pages editor](docs/screenshot-admin-pages.png)

![Album](docs/screenshot-admin-album.png)

- **Painted media:** 20 photographs of the center (pool, courts, climbing hall, studios, dojo, gym, café and the building), 8 coach portraits, 3 short videos with posters and 3 PDF documents, all generated in code by Claude.
- **No front-end build:** plain HTML, CSS and vanilla JavaScript; no npm, bundler or SPA framework.
- **No database:** everything lives in three JSON files under `data/`.

---

## Getting started

**Requirements:** .NET 10 SDK and Visual Studio (or any editor with the `dotnet` CLI).

```bash
git clone https://github.com/hkim6000/SkyNet-SportWebsite-Asp.net.Core-Full-Source.git
cd SkyNet-SportWebsite-Asp.net.Core-Full-Source
dotnet run
```

Or open `Sport.csproj` in Visual Studio and press **F5**.
The SkyNet package (`TheSkyLite.SkyNet` 1.0.6) restores automatically from NuGet.
The app opens on **Home**, the startup page set in `appConfig/application.cfg`.

**Admin:** click **Staff sign in** in the footer (or open `/AdminLogin`) and pick a role.

---

## Make it yours

1. **Settings:** name, town, address, phone, email, accent colour, theme, opening hours, time zone, tier thresholds and discounts, points, early registration, refund rules, desk hold and offer times, and the day pass.
2. **Sport types:** rename or replace the eight sports, their icons and pictures.
3. **Instructors:** your coaches, their photos and bios. Link each one to a staff login in **Users**.
4. **Classes:** your schedule, ages, prices and seats. Save as a draft until it is ready.
5. **Pages:** change every headline, paragraph, button and picture on the website.
6. **Pictures:** upload in **Media**, or put your own photos in `images/sport/full` (1600 px) and `images/sport/thumbs` (640 px) with the same file name, and portraits in `images/sport/people`.

---

## Run it for real (production mode)

1. **Create a password hash** for the first center manager. In PowerShell:

   ```powershell
   $p = Read-Host "Password"; [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($p))).Replace("-","").ToLower()
   ```

2. **Edit `appConfig/application.cfg`:**

   ```
   app.app.runmode | 1
   app.admin.user | admin
   app.admin.passhash | <the 64-character hash>
   ```

3. **Sign in as that user.** They become center manager. Open **Users** and set a real password for each person who needs access.
   - The sample staff are demo accounts and can't sign in until you set a new password.
   - Sample members can't sign in in production either.

4. **Clear the sample data:** start from your own `data/site.json` (settings, sports, instructors, classes and pages, with empty members, registrations, payments, passes, visits, points and messages).

5. **Connect a real payment provider.** The card form in this template is a demo that accepts the test card. Replace `Rules.Charge` and `Rules.Refund` with calls to your provider before taking money.

6. **Give the site write access** to these folders. On IIS, grant the application pool identity (for example `IIS AppPool\Sport`) **Modify** permission on:

   | Folder | What is written |
   |---|---|
   | `data/` | `site.json`, `staff.json`, `activity.json` |
   | `images/uploads/` | photos, videos and PDFs uploaded in Media |
   | `temp/` | staff and member sessions (`temp/admin`), uploads in progress and demo copies (`temp/sandbox`) |

7. **Use HTTPS.** Session tokens travel in cookies and in each request.

> **Redeploying:** publishing copies the files in `data/` from your project over the live ones. Download the live `data/` and `images/uploads/` folders first (or exclude them from the publish) so you keep your members and uploads. `temp/` is not published, but turn off "Remove additional files at destination" so it is not deleted.

In production, staff sessions last 8 hours and renew while you work; member sessions last 14 days. Changing a password signs that person out everywhere.

---

## Project structure

```
Sport/
├── appConfig/application.cfg        app settings, run mode, first center manager
├── codes/
│   ├── Models/SportModel.cs         data DTOs (SportType, Coach, SportClass, ClubMember, Registration, Payment, DayPass, ...)
│   ├── Core/Store.cs                JSON storage, demo copies, week shifting, center clock
│   ├── Core/Rules.cs                tiers, points, prices, seats, waitlist offers, refunds, receipts
│   ├── Core/Access.cs               areas and rights, passwords, test cards
│   ├── Core/Auth.cs                 staff and member sessions, admin menu
│   ├── Core/Ui.cs                   shared HTML helpers, statuses, period filter, paging
│   ├── Core/Blocks.cs               page blocks for the Pages editor
│   ├── Home.cs  Sports.cs  Sport.cs  Classes.cs  Class.cs  MyRegistration.cs  Instructors.cs  Instructor.cs
│   │   Media.cs  About.cs  Contact.cs  Pass.cs  Join.cs  SignIn.cs  Member.cs
│   └── AdminLogin.cs  Admin.cs  AdminSports.cs  AdminClasses.cs  AdminBoard.cs  AdminInstructors.cs
│       AdminRegistrations.cs  AdminPayments.cs  AdminMembers.cs  AdminMessages.cs  AdminMedia.cs
│       AdminAlbums.cs  AdminPages.cs  AdminUsers.cs  AdminSettings.cs  AdminActivity.cs  AdminAccount.cs
├── htmls/  scripts/  styles/        page markup, JavaScript and CSS
├── data/
│   ├── site.json                    settings, sports, instructors, classes, members, registrations, payments, ...
│   ├── staff.json                   users, password hashes and rights
│   └── activity.json                the activity log (latest 5,000)
├── images/sport/full/  sport/thumbs/  painted photos (1600 px and 640 px, WebP)
├── images/sport/people/             coach portraits (WebP)
├── images/sport/video/  sport/docs/ painted videos (MP4) and PDF documents
├── images/uploads/                  files uploaded in Media (created at run time)
├── temp/                            sessions and demo copies (created at run time)
└── Program.cs
```

### One page = four files, one name

| File | Holds |
|---|---|
| `codes/AdminBoard.cs` | the page class (`: WebPage`) and its server methods |
| `htmls/AdminBoard.html` | markup with `{plhd_*}` placeholders |
| `scripts/AdminBoard.js` | one IIFE namespace, `AdminBoardJs` |
| `styles/AdminBoard.css` | styles, every class prefixed (`ab-`) |

---

## SkyNet in action

The browser calls a C# method, the method returns an `ApiResponse`, and only those parts of the page change.

```js
// scripts/AdminBoard.js
api('Act', [{ key: 'kid', vlu: cur }, { key: 'bsess', vlu: String(sess) }, { key: 'bwhat', vlu: 'in' }, { key: 'rid', vlu: rid }]);
```

```csharp
// codes/AdminBoard.cs
if (!Access.Can(c.U, AreaKey, 'E'))
{
    return await Refuse(response, c, "board", GetDataValue("rid"), "You can look at the board but not check people in.");
}
...
site.Visits.Add(new Visit { Id = Store.NextId(site.Visits.Select(x => x.Id), "V-", 7000), Member = r.Member, Kind = "class", Reg = r.Id, Session = si, Name = r.Name, In = nowU, By = c.U.Id });
Rules.AddPoints(site, m, site.Settings.PerSession, "session", r.Id, k.Title + ", session " + (si + 1), c.U.Id, nowU);
...
response.SetElementContents("ab-board", Board(c, saved, k, si));
```

| Page | Requests |
|---|---|
| every public page | `Hydrate` (the visitor's demo copy), `Who` |
| Class | `Panel`, `Quote`, `Register` |
| MyRegistration | `Find`, `Cancel`, `Ask` |
| Member | `Load`, `PayForm`, `PayQuote`, `Pay`, `CancelReg`, `AskRefund`, `Receipt`, `PassReceipt`, `FamForm`, `FamSave`, `FamDrop`, `SaveProfile`, `SavePass`, `SignOutMember` |
| Pass / Join / SignIn / Contact | `Buy` / `CreateAccount` / `SignInMember`, `Reset` / `SendMsg` |
| every admin page | `Load`, `SignOut` (AdminLogin: `SignIn`) |
| AdminBoard | `Side`, `Board`, `Act` |
| AdminClasses | `List`, `Preview`, `Form`, `Save`, `CancelClass`, `Drop` |
| AdminRegistrations | `List`, `Open`, `Desk`, `Cancel`, `AddForm`, `Add` |
| AdminPayments | `List`, `Pay`, `Refund`, `Asks`, `AskOpen`, `Decide`, `Csv` |
| AdminMembers | `List`, `Desk`, `CheckIn`, `CheckOut`, `Sell`, `Open`, `Save`, `Suspend`, `Adjust` |
| AdminMessages | `List`, `Open`, `SetStatus`, `AddNote`, `Delete` |
| AdminMedia | `List`, `Open`, `SaveInfo`, `Upload`, `Chunk`, `VideoDone`, `DocDone`, `Remove` |
| AdminSports / AdminInstructors / AdminAlbums | `List`, `Form`, `Save`, `Drop`, `MediaPick`, `MediaUp` |
| AdminPages | `Show`, `AddBlock`, `Save`, `Versions`, `Restore`, `MediaPick`, `MediaUp` |
| AdminUsers / AdminSettings / AdminAccount / AdminActivity | `List`, `Form`, `Save` / `Save` / `Save` / `List` |

Learn more: [SkyNet Developer Guide](https://www.theskylite.com/documents/SkyNet_Developer_Guide.html)

---

## 100% AI-driven coding

Every file in this template was generated by Claude (Anthropic's AI) from short instructions, directed and reviewed by the author: C#, HTML, CSS, JavaScript, the painted photographs, portraits, videos and documents, and the data.
No line was written by hand.

| | |
|---|---|
| Pages | 32 (15 public, 17 admin) |
| Lines of code | ~43,000 (C#, HTML, CSS, JavaScript) |
| Media | 20 painted photos (40 WebP), 8 portraits, 3 videos, 3 PDF documents |
| Sports / instructors / classes | 8 / 8 / 27 |
| Sample data | 40 members, about 200 registrations, 670 check-ins and 950 points entries |
| Lines written by hand | 0 |

SkyNet's page model is small and predictable: one class, four files, `$ApiRequest` → `ApiResponse`.
With so few, consistent rules, AI can generate complete, working pages with very few errors.

---

## License

- **This template** (all source files, artwork and data in this repository): [MIT License](LICENSE). Free to use, modify and redistribute, including commercially.
- **SkyNet Framework** (`TheSkyLite.SkyNet` NuGet package): proprietary, free to use including commercial use; see the license included in the package.

---

## Links

- Live demo: https://www.theskylite.com/Sport
- SkyNet Framework: https://www.theskylite.com
- More website templates: https://www.theskylite.com/SkyNetTemp
- NuGet package: https://www.nuget.org/packages/TheSkyLite.SkyNet
- SkyNet Agent (build your own site with Claude Code): https://github.com/hkim6000/SkyNet-Agent
- Template #1 — Beauty store: https://github.com/hkim6000/SkyNet-BeautyWebsite-Asp.net.Core-Full-Source
- Template #2 — Pharma company: https://github.com/hkim6000/SkyNet-PharmaWebsite-Asp.net.Core-Full-Source
- Template #3 — Travel blog: https://github.com/hkim6000/SkyNet-TravelWebsite-Asp.net.Core-Full-Source
- Template #4 — Fast food restaurant: https://github.com/hkim6000/SkyNet-FastFoodWebsite-Asp.net.Core-Full-Source
- Template #5 — Fine dining restaurant: https://github.com/hkim6000/SkyNet-FineDiningWebsite-Asp.net.Core-Full-Source
- Template #6 — Car dealership: https://github.com/hkim6000/SkyNet-CarDealerWebsite-Asp.net.Core-Full-Source
- Template #7 — Web comic: https://github.com/hkim6000/SkyNet-WebComicWebsite-Asp.net.Core-Full-Source
- Template #8 — Art gallery: https://github.com/hkim6000/SkyNet-ArtGalleryWebsite-Asp.net.Core-Full-Source
- Template #9 — Artist blog: https://github.com/hkim6000/SkyNet-ArtistBlogWebsite-Asp.net.Core-Full-Source
- Template #10 — Newspaper: https://github.com/hkim6000/SkyNet-SkyLitePostWebsite-Asp.net.Core-Full-Source
- Template #11 — Hotel: https://github.com/hkim6000/SkyNet-SkyHotelWebsite-Asp.net.Core-Full-Source
- Template #12 — Clinic: https://github.com/hkim6000/SkyNet-ClinicWebsite-Asp.net.Core-Full-Source
- Template #13 — Real estate agency: https://github.com/hkim6000/SkyNet-RealEstateWebsite-Asp.net.Core-Full-Source
- Template #14 — Dog shelter: https://github.com/hkim6000/SkyNet-DogShelterWebsite-Asp.net.Core-Full-Source
- Template #15 — Manufacturing (AutoParts): https://github.com/hkim6000/SkyNet-AutoPartsWebsite-Asp.net.Core-Full-Source
- SkyNet project template: https://github.com/hkim6000/ASPNETCoreEmpty.SkyNet

© 2026 HC Kim
