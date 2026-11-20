# HolidayApp — yearly vacation planner

A calendar with all 12 months on one page to book vacation days, see the Portuguese
public holidays, keep track of the working-day balance and plan each trip.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (17.14+) / Visual Studio 2026, Rider or VS Code with C# Dev Kit

## Run

```bash
dotnet run --project src/HolidayApp.Web
```

Open http://localhost:5180. The SQLite database (`holidayapp.db`) is created
automatically on the first run.

In Visual Studio: open `HolidayApp.slnx`, set **HolidayApp.Web** as the startup
project and press F5.

Tests (calendar rules and validation, UI with bUnit and API integration tests
with a temporary SQLite database):

```bash
dotnet test
```

## Home server (Windows)

On a Windows PC that is always on (e.g. the same one as Home Assistant). It needs Git
and the .NET 10 SDK (`winget install --id Microsoft.DotNet.SDK.10 -e`).

**Install (once):**

```bash
cd $HOME\Documents
git clone https://github.com/rDg06/HolidayApp.git
```

Then, for **every update**, run `scripts\update.bat` (double-click): it gets the
new version, publishes the app to `C:\HolidayApp\app`, copies `scripts\start.bat`
to `C:\HolidayApp\start.bat` and starts it on port 5180. The data lives in
`C:\HolidayApp\dados` and is not touched.

> Coming from the version with Portuguese script names (`atualizar.bat` / `iniciar.bat`):
> run `git pull` in the repository folder once, then `scripts\update.bat`. A shortcut
> to `C:\HolidayApp\iniciar.bat` keeps working (it now starts `start.bat`).

Once, in an administrator PowerShell, open the port on the private network:

```bash
New-NetFirewallRule -DisplayName "HolidayApp" -Direction Inbound -Protocol TCP -LocalPort 5180 -Action Allow -Profile Private
```

On the home network it opens at `http://SERVER-IP:5180`.

**Accounts.** Without the file `C:\HolidayApp\dados\invite-code.txt` the app opens with
no sign-in (home network only). With it, each person has **their own account** (email
and password, 30-day session on each device) and only sees their own data; the file's
content is the **invite code** needed to create an account. The first account created
gets the data that already existed. On "Profile and settings": change name and
password, sign out, download my data and delete the account. The older `senha.txt`
file still works. Create or change the code (no accents; restart the app afterwards):

```bash
Set-Content C:\HolidayApp\dados\invite-code.txt -Value "the-invite-code" -Encoding ascii -NoNewline
```

**Share with friends outside home (Tailscale Funnel).** Only after creating the invite code:

```bash
tailscale funnel --bg 5180
```

It shows the public address (`https://NAME.xxxx.ts.net`) to send with the invite code.
To close it: `tailscale funnel --bg 5180 off`. If two people edit the same trip,
whoever saves second is told to reload, instead of wiping the other person's work.

## Publish (Azure App Service)

Outside development the API **only accepts allowed users**. Sign-in is done by
App Service *Authentication* (Easy Auth), which sits in front of the app:

1. Publish the app (Visual Studio → right-click **HolidayApp.Web** → **Publish**
   → **Azure App Service (Linux)**).
2. In the Azure portal, under **Authentication**, add a provider (Microsoft or
   Google) and choose *Require authentication*.
3. Under **Configuration → Application settings** add the allowed emails:
   `Auth__AllowedEmails__0` = `your@email.com` (and `__1`, `__2`… for more people).
4. Point the database to the persistent folder:
   `ConnectionStrings__Default` = `Data Source=/home/data/holidayapp.db`.

Without *Authentication* turned on the API rejects every request (401), and with an
account that is not on the list it answers 403. Each allowed person only sees their
own data. Leaflet and the Inter font are served by the app itself (`wwwroot/lib`),
with no requests to CDNs or Google Fonts.

## What it does

- Year view with all 12 months (weeks start on Monday)
- Click a day or **drag** over several to create an entry
- Click an entry to edit or delete it
- Types: Vacation, Personal, Other (only "Vacation" counts against the balance)
- National holidays calculated automatically for any year
  (including Good Friday, Easter and Corpus Christi)
- Optional Carnival and municipal holiday (**Profile and settings** page)
- Working-day count: weekends and holidays do not count
- Yearly balance (22 days by default) and **bridge** suggestions booked with one click
- **Plan vacation**: on each vacation entry, the "Plan vacation" button opens a page
  with tabs below the dates: Places (destination and map), Bookings (references,
  links and cost), Tasks, Budget (planned vs. spent, including booking costs, and
  the "Where I spent most" chart) and Day by day
- **Vacations page** with every trip: days, bookings, places, tasks and budget
- **Map (Where I've been)**, the home page: every trip (one point per lodging, or the
  centre of the places, or the destination looked up by name), past and upcoming;
  trips taken, days travelling and countries. Countries come from Nominatim
  (OpenStreetMap) and are stored in the browser. In "Other places I've been" you add
  places and the year by hand (trips outside the calendar), which go on the map and
  into the countries
- **Places map** ([Leaflet](https://leafletjs.com) and
  [OpenStreetMap](https://www.openstreetmap.org) tiles, no API key):
  - pin places by clicking the map or searching by name, drag markers
  - day of the visit (shows in the day-by-day plan), "visited", notes and link
  - **kinds** with their own icon: visit, lodging, airport, metro, bus, train;
    airports and stations without a day show on every day
  - **lodgings** with a first and last day, for trips through several cities or
    countries; lodging bookings have "Put on map"
  - **day filter** on the map and in the list; during the trip it opens on today
  - **street route** of the day (lodging → places in list order → new lodging)
    with the distance and time of each leg, on foot, by car or by bike; drag the
    places to change the order
  - services used: search and the destination centre use Nominatim and routes use
    the routing service ([FOSSGIS](https://routing.openstreetmap.de)), both from
    OpenStreetMap, so those texts and the coordinates of the day's places are sent
    to them; without a connection the straight line stays
- **Share as PDF**: in the planner, "Share (PDF)" opens a trip summary (map, day by
  day, other places, bookings) ready to save as PDF from the browser's print dialog.
  Amounts, booking references/links and notes are only included if chosen
- **Accounts** (with an invite code): email and password, each person only sees
  their own data; profile with name and password, data download and account deletion
- Automatic light/dark mode and a layout adapted to phones
- **Installable and offline** (PWA): on a phone, "Add to home screen". Offline, it
  opens the last version seen on that device (read-only: saving needs internet;
  OpenStreetMap tiles are not stored)

## Architecture

```
src/
  HolidayApp.Shared/          DTOs, enums, validation and working-day calculation
                             (shared between server and browser)
  HolidayApp.Domain/          Entities and business rules: Portuguese holidays,
                             Easter, bridge suggestions. No external dependencies.
  HolidayApp.Infrastructure/  EF Core (SQLite), application services
  HolidayApp.Web/             ASP.NET Core server: REST API at /api + hosts Blazor
  HolidayApp.Client/          Blazor WebAssembly UI (runs in the browser)
tests/
  HolidayApp.Domain.Tests/    xUnit tests of the calendar rules and validation
  HolidayApp.Api.Tests/       API integration tests (WebApplicationFactory + SQLite)
  HolidayApp.Client.Tests/    UI tests (bUnit): map, filter, lodgings, bookings
```

Global styles (colours, buttons, fields, cards) are in
`HolidayApp.Web/wwwroot/css/app.css`; each page's and component's styles sit next
to the file, in `.razor.css` (Blazor CSS isolation). Display texts (dates, amounts,
labels) are in `HolidayApp.Client/Formatting/Display.cs`.

The UI talks to the server **only through the API**, so later you can add a mobile
app (MAUI, React Native…) that uses the same endpoints.

### Pages

| Route | Page |
|---|---|
| `/` (also `/map`) | Where I've been (map of every trip) |
| `/vacations` | Every vacation |
| `/vacations/{id}` | Plan vacation (`?from=vacations` or `?from=map` for the back link) |
| `/vacations/{id}/share` | Share / PDF summary |
| `/calendar` | Year calendar |
| `/settings` | Profile and settings |
| `/login`, `/register`, `/logout` | Sign-in pages (server pages, with accounts on) |

### API

| Method | Route | Description |
|---|---|---|
| GET | `/api/events?year=2026` | Entries of the year |
| GET | `/api/events/{id}` | One entry |
| POST | `/api/events` | Create an entry |
| PUT | `/api/events/{id}` | Update an entry |
| DELETE | `/api/events/{id}` | Delete an entry (and its plan) |
| GET / PUT | `/api/events/{id}/plan` | Vacation plan |
| GET | `/api/vacations` | Every vacation with its plan summary |
| GET | `/api/calendar/{year}/holidays` | Holidays of the year |
| GET | `/api/calendar/{year}/summary` | Vacation balance and bridges |
| GET / PUT | `/api/settings` | User settings |
| GET / PUT / DELETE | `/api/me` | My account (with accounts on) / rename / delete the account and data |
| POST | `/api/me/password` | Change the password |
| GET | `/api/me/export` | Download my data (JSON) |
| GET / POST | `/api/visited` | Places I've been (added by hand) |
| DELETE | `/api/visited/{id}` | Delete one of those places |

In development, the OpenAPI document is at `/openapi/v1.json`.

## Ready to scale

The project already has the pieces in place to grow without a rewrite:

**1. Several users.** Every table has a `UserId` and `AppDbContext` applies a global
filter so each user only sees their own data. The user comes from the account
(`Identity/Accounts.cs`), from the App Service sign-in
(`Identity/AppServiceAuthentication.cs`, with the `Auth:AllowedEmails` list) or, with
neither, is `local`. For another kind of sign-in (Entra ID directly…) just swap the
authentication scheme: `HttpCurrentUser` reads the `NameIdentifier` claim and does
not need to change.

**2. Another database.** The provider comes from `appsettings.json`
(`Database:Provider`). For PostgreSQL: install
`Npgsql.EntityFrameworkCore.PostgreSQL` in the Infrastructure project, uncomment the
`UseNpgsql` line in `DependencyInjection.cs` and change the connection string.
The current migrations were generated for SQLite: when switching provider, generate
them again.

**3. Migrations.** The database is created and updated with EF Core migrations
(`Infrastructure/Persistence/Migrations`), applied automatically on startup.
Older databases, created with `EnsureCreated`, are recognised and keep their data.
The four oldest migrations keep their original IDs in the `[Migration("…")]` attribute
(e.g. `20261028224624_Inicial`), because those IDs are stored in every existing
database; only their file and class names are in English. When you change the model (entities or
configuration), add a new migration:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add NameOfTheChange --project src/HolidayApp.Infrastructure --startup-project src/HolidayApp.Web --output-dir Persistence/Migrations
```

**4. Sharing with a team/family.** Natural next step: share a trip between
accounts, and a view that shows everyone's vacations on the same calendar.

## Ideas for the next iteration

- "What's nearby" the lodging (restaurants, supermarkets, pharmacies…)
- Automatic place info (Wikipedia summary and photo, opening hours)
- Share trips between accounts
- Balance carried over from the previous year
- Holidays of other countries (a new `IHolidayProvider` implementation)
