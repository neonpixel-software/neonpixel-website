# Spec: NeonPixel Website

## Assumptions
These weren't specified explicitly — correct any that are wrong before we move to Plan:
1. NeonPixel is a creative/digital business (agency/studio) — the Home page will use placeholder copy until real brand content is supplied.
2. Only the Home page (plus the 404 error page, since it's a system-level page rather than new site content) ships at launch; About/Services/Contact are deliberately out of scope for now and will be separate future specs (added as new document types + templates later).
3. Umbraco 18 targets .NET 10 (following Umbraco's pattern of tracking current .NET LTS/STS) — **must be verified against official Umbraco 18 release notes/requirements before Plan is finalized**, since it postdates this assistant's knowledge cutoff.
4. Deployment shape: Kestrel behind an nginx reverse proxy, running as a systemd service on the Ubuntu 26.04 VPS, HTTPS via Let's Encrypt/certbot.
5. No custom membership/auth beyond Umbraco's own backoffice login at this stage (no separate public user accounts).
6. Test framework: xUnit, used lightly given there's no custom application code at launch (see Testing Strategy).
7. Umbraco Cloud is not in use — this is a self-hosted, self-managed instance on the VPS (matches the "Ubuntu VPS" hosting answer).
8. The VPS already has (or will have, as a prerequisite outside this spec) a dedicated deploy user, an SSH key pair for GitHub Actions to authenticate with, and the .NET runtime + nginx + systemd unit already provisioned — this spec's CI/CD covers what happens *after* that base provisioning, not the initial server setup.
9. GitHub Actions secrets (`VPS_HOST`, `VPS_DEPLOY_KEY`, `VPS_USER`, deploy path) hold the connection details — none of this is hardcoded in the workflow file.
10. GitFlow is the branching model: `main` (production, always deployable), `develop` (integration branch), `feature/*` (branched from and merged back into `develop`), `release/*` (cut from `develop`, merged into both `main` and `develop` to ship), `hotfix/*` (cut from `main` for urgent fixes, merged into both `main` and `develop`). There's a single VPS/environment (production), so only `main` triggers a deploy — `develop` and feature branches run CI (build+test) but don't deploy anywhere, unless a staging environment is added later (see Open Questions).
11. uSync (the Jumoo `uSync` NuGet package) is configured to export Umbraco's content/schema (document types, data types, templates, and content itself) to disk as version-controlled files, and to import them automatically on startup (`uSync.BackOffice` `ImportAtStartup`/`ImportOnFirstBoot`-style config). This is how content structure and content values move between local dev and production *without* the SQLite `.db` file ever being committed — the `.db` is rebuilt/reconciled from the `uSync` folder on each environment.
12. The repository is public. This raises the bar on the existing secrets boundary: nothing environment-specific or sensitive (VPS hostname/IP, credentials, internal paths) may appear in code, config, or in anything uSync exports to disk.
13. The site's front-end is based on a purchased HTML/CSS/JS template (plain markup, framework unconfirmed), converted into Umbraco Razor templates/partials rather than used as static pages directly.
14. **Resolved:** the purchased template's license does *not* permit redistribution. Its markup/CSS/media (or a close Razor derivative of them) can never be committed to this public repository, in any form, at any point — not just until a license is checked. This is now a permanent architectural constraint, not a temporary blocker.
15. The purchased template includes its own 404 page design, which will be converted to Umbraco's custom error-page mechanism (a dedicated Umbraco error/404 content node + template, wired up via `Umbraco:CMS:Content:Error404Collection` or equivalent) rather than left as ASP.NET Core's generic status-code page. It is subject to the same non-redistribution constraint as the rest of the template.
16. Template files live at `docs/HTML/` on disk (`index.html`, `404.html`, `css/`, `js/`, `img/`, `fonts/`, `video/`, and a `source-files/` folder bundling third-party libraries — Bootstrap, jQuery, GSAP, Swiper, Lenis, Matter.js, typed.js, imagesloaded, Ukiyo.js, Ajax Chimp, Phosphor Icons — each apparently under its own permissive license, unaffected by the template's own non-redistribution term). `docs/HTML/` stays gitignored permanently — it is a local reference for converting markup, never a source the public repo tracks.
17. **Architecture: template-derived front-end lives in a private companion repo, referenced as a git submodule.** A new private repo (name assumed `neonpixel-theme`, owner/URL TBD — see Open Questions) holds the converted Razor views and static assets (CSS/JS/img/fonts/video) derived from `docs/HTML/`. It's added to this public repo as a git submodule at `theme/`. Critically, its contents are **not copied** into any publicly-tracked directory (`Views/`, `wwwroot/`) — instead, the Umbraco app is configured (a small addition in `Program.cs`) to load Razor views from `theme/Views` and serve static files from `theme/wwwroot` directly. A git submodule only stores a commit-hash reference in the parent repo, never the submodule's file contents, so the theme's actual code never enters this repo's history. Anyone cloning this public repo without access to the private `neonpixel-theme` repo gets a working Umbraco backend with no front-end presentation — expected and correct given the license constraint.
18. This means the earlier assumption that Home ships as "pure CMS content" with no custom code needs a small amendment: the `theme/` view-location and static-file wiring in `Program.cs` is minimal, necessary custom code — infrastructure plumbing, not business logic, so it doesn't change the Testing Strategy's conclusion that there's no controller/service logic to unit test at launch.
19. Both the public repo's CI (build+test) and the deploy workflow need read access to the private `neonpixel-theme` repo to check out the submodule. **The `neonpixel-software` org has deploy keys disabled org-wide** (`deploy_keys_enabled_for_repositories: false`, confirmed via the GitHub API — a deliberate admin policy, not something this spec should try to override). Access is instead via a fine-grained Personal Access Token scoped to only `neonpixel-theme` with read-only Contents permission, created through GitHub's web UI (no API exists to mint fine-grained PATs programmatically) and stored as a GitHub Actions secret (`THEME_REPO_PAT`) on this repo, separate from the VPS deploy key. `actions/checkout` consumes it via its `token` input rather than `ssh-key`. Local developers authenticate with their own GitHub credentials/SSH key when running `git submodule update --init`.

## Objective
Stand up the first version of the NeonPixel business website as an Umbraco 18 CMS site, with the front-end based on a purchased HTML/CSS/JS template (converted into Umbraco Razor templates, but never redistributed): editors manage the Home page entirely through the Umbraco backoffice, backed by Umbraco's SQLite persistence, with content/schema kept in sync across environments via uSync (not the database file itself), deployable to a self-managed Ubuntu VPS. The template-derived Razor/CSS/JS lives in a private `neonpixel-theme` repo, pulled in as a git submodule, and is never copied into this public repo's tracked files. Launch scope is the Home page and a custom 404 error page, both matching the purchased template's design. Success is a working `dotnet run` locally (with the theme submodule checked out) with a functioning backoffice, pages that visually match the purchased template, and a documented path to running the same app under nginx + systemd on the VPS.

## Tech Stack
- Umbraco CMS 18 (built on ASP.NET Core, .NET 10 — confirm exact version against official Umbraco 18 requirements)
- Umbraco's built-in SQLite persistence (no separate hand-rolled EF Core DbContext/entities — content lives in Umbraco's content tree, not custom tables)
- uSync (Jumoo) for exporting/importing content, document types, data types, and templates as version-controlled files, keeping the SQLite database itself out of the repo
- Front-end: purchased HTML/CSS/JS template, converted from a local reference copy at `docs/HTML/` (permanently gitignored) into Razor views + static assets that live in a **separate private repo** (`neonpixel-theme`), pulled into this repo as a git submodule at `theme/` and loaded directly from there (not copied) via a small Razor-view-location/static-file addition in `Program.cs`; bundles Bootstrap, jQuery, GSAP, Swiper, Lenis, Matter.js, typed.js, imagesloaded, Ukiyo.js, Ajax Chimp, and Phosphor Icons
- Server-rendered Razor views via Umbraco templates (no SPA framework)
- xUnit for any custom code (see Testing Strategy — expected to be minimal at launch)
- nginx (reverse proxy) + systemd (process manager) on Ubuntu 26.04

## Commands
```
Install templates:  dotnet new install Umbraco.Templates
Scaffold project:   dotnet new umbraco -n NeonPixel.Web -o src/NeonPixel.Web
Add uSync:          dotnet add src/NeonPixel.Web package uSync
Add theme submodule: git submodule add <neonpixel-theme repo URL> theme
Init submodules (fresh clone): git submodule update --init --recursive
Build:              dotnet build
Run:                dotnet run --project src/NeonPixel.Web
Test:                dotnet test
Publish:             dotnet publish src/NeonPixel.Web -c Release -o out
```
Backoffice (content/document type editing) is reached at `/umbraco` after first run, where the initial admin account is created. uSync's own sync/report/import actions are triggered from its section in the backoffice, or automatically on startup per its configuration (see Git Workflow / CI-CD notes below). The `theme` submodule requires access to the private `neonpixel-theme` repo — without it, the app runs but has no front-end views/assets to render.

## Project Structure
```
src/NeonPixel.Web/          → Umbraco CMS site (standard `dotnet new umbraco` layout)
  Views/                     → Only Umbraco's own default/scaffold views, if any — no template-derived views live here
  App_Plugins/                → Any custom backoffice extensions (none expected at launch)
  wwwroot/                    → Only non-theme static assets, if any — theme CSS/JS/images are served from theme/wwwroot, not copied here
  umbraco/                    → Umbraco's own runtime data — not hand-edited, not committed
  uSync/                       → uSync's exported content/schema files (version-controlled — this is what syncs environments, not the .db)
  Program.cs                  → Umbraco startup; includes the addition that points Razor view resolution and static file serving at theme/
  appsettings.json            → Config (connection strings, non-secret settings)
  appsettings.Local.json      → Local overrides, gitignored, holds any secrets
theme/                        → Git submodule → private `neonpixel-theme` repo (Views/ and wwwroot/ for the purchased template's Razor conversion). Only a commit-hash reference lives in this repo; the private repo's content is never checked into this one.
tests/NeonPixel.Web.Tests/  → xUnit tests, only added once custom code exists
.github/workflows/ci.yml     → Build+test on push to develop and PRs into develop/main (checks out theme/ via THEME_REPO_PAT)
.github/workflows/deploy.yml → Build, test, publish, deploy to VPS on push to main (also checks out theme/)
tasks/                       → Plan and task list (added in Phase 2/3)
```
Content structure itself (the "Home" document type and its fields) is defined inside the Umbraco backoffice, not as C# entities — this is the key structural difference from a hand-rolled EF Core approach. uSync serializes that structure (and content values) to the `uSync/` folder, which *is* committed — that's the mechanism that keeps local dev and production in sync without ever committing the SQLite database. Front-end presentation (Razor markup, CSS, JS, images derived from the purchased template) is a completely separate concern kept out of this repo entirely, via the `theme/` submodule.

## Git Workflow
GitFlow branching model:
- `main` — production, always deployable, protected. Only receives merges from `release/*` or `hotfix/*` branches.
- `develop` — integration branch. Only receives merges from `feature/*` branches (and `release/*`/`hotfix/*` branches merge back into it too, to keep it current).
- `feature/*` — one branch per feature, branched from `develop`, merged back into `develop` via PR.
- `release/*` — cut from `develop` when preparing a release; stabilization only (no new features); merged into both `main` and `develop`, then tagged.
- `hotfix/*` — cut from `main` for urgent production fixes; merged into both `main` and `develop`.

## CI/CD
GitHub Actions, two triggers on the same build/test job, one additional deploy job. Every job checks out this repo **and** the `theme/` submodule (`actions/checkout` with `submodules: true` and `token: ${{ secrets.THEME_REPO_PAT }}` — a fine-grained PAT scoped read-only to `neonpixel-theme`, since the org disables deploy keys — separate from the VPS deploy key):
1. **CI (build & test)** — runs on pushes to `develop` and on every pull request targeting `develop` or `main`: checkout (with submodule) → `dotnet build` → `dotnet test`. This is the gate for merging feature/release/hotfix branches — it doesn't deploy anywhere.
2. **CD (deploy)** — runs only on push to `main` (i.e., after a `release/*` or `hotfix/*` branch is merged in), and only if build+test pass:
   a. **Publish** — `dotnet publish src/NeonPixel.Web -c Release -o out` (the `theme/` submodule content is resolved at build time via the `Program.cs` view-location/static-file wiring, and needs to be present on the runner/build machine for this to produce a working publish output).
   b. **Deploy** — copy `out/` (plus the resolved `theme/` content it depends on) to the VPS over SSH (`rsync`/`scp`) into the app's deploy directory, authenticating with a deploy key stored in the `VPS_DEPLOY_KEY` GitHub secret.
   c. **Restart** — SSH into the VPS and run `sudo systemctl restart neonpixel-web` (or equivalent unit name) to pick up the new build.

Notes:
- Umbraco's SQLite database and `umbraco/Data` runtime folder live outside the deploy directory (or are explicitly excluded from the rsync) so a deploy never overwrites live content. The committed `uSync/` folder *is* deployed — it's what uSync reads on startup to reconcile the production database's content/schema with what's in source control.
- The deploy job only runs after build+test pass on `main` — a red build never reaches the VPS, and `develop`/feature work never triggers a deploy at all since there's only one (production) environment.
- The systemd `restart` in step 2c is part of the automated deploy flow and is *not* the same as the "modifying nginx/systemd config" boundary below, which refers to editing the actual unit/config files, not the routine restart the pipeline performs on every deploy.
- Workflow restarting the app is also what triggers uSync's `ImportAtStartup` (or equivalent) to apply any `uSync/` changes shipped in that deploy — no separate manual import step.
- The `THEME_REPO_PAT` secret is a real access path into the private theme repo's content from within a public repository's workflow files — anyone able to modify workflow files in this public repo could exfiltrate the private theme's content via that key. Mitigate by requiring PR review on workflow-file changes specifically (branch protection can be scoped to paths) and keeping the key strictly read-only.

## Code Style
At launch there is expected to be little to no custom C# beyond the Umbraco template scaffold — content and layout are configured via the backoffice and Razor templates. If/when custom code is needed (e.g. a future contact form), follow standard C#/.NET conventions: PascalCase for classes/methods/properties, camelCase for locals/parameters, `_camelCase` for private fields, and prefer Umbraco's supported extension points (surface controllers, view components) over bypassing the CMS pipeline.

Example Razor template for the Home document type:
```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<ContentModels.Home>
@{
    Layout = "master.cshtml";
}
<h1>@Model.Value("heroTitle")</h1>
<p>@Model.Value("heroBody")</p>
```

## Testing Strategy
- No custom application logic ships at launch (pure CMS content), so there is no controller/service code to unit test initially.
- Verification at launch is smoke-level: `dotnet build` succeeds, `dotnet run` starts Umbraco, the backoffice at `/umbraco` is reachable, the Home template renders without error, and requesting an unknown URL returns the custom 404 template with an actual HTTP 404 status code (not a 200 or Umbraco's default error page).
- If/when custom code is added (contact form handlers, custom components), it gets xUnit tests under `tests/NeonPixel.Web.Tests`, mirroring `src`, at that time — not scaffolded prematurely.

## Boundaries
- **Always do:** run `dotnet build` and `dotnet test` before considering a task done; verify the site boots and the backoffice loads; follow the project structure above; keep secrets (backoffice admin credentials, connection strings, deploy keys, any API keys) out of `appsettings.json` and out of the workflow file itself, using GitHub Actions secrets and `appsettings.Local.json` (gitignored) instead; review uSync's exported files before committing to confirm nothing environment-specific or sensitive got serialized into them — this repo is **public**; keep every template-derived file (Views, CSS, JS, images, fonts, video) inside the `theme/` submodule, never in a path this repo tracks directly.
- **Ask first:** adding new NuGet packages or Umbraco marketplace packages beyond the core CMS and uSync; changing the database engine (e.g. moving off SQLite to SQL Server); editing nginx config or the systemd unit *definition* on the VPS; changing the CI/CD trigger, target branch, or deploy destination; any change to the public URL/domain or DNS; creating additional document types/pages beyond Home; merging directly into `main` or `develop` outside the GitFlow feature/release/hotfix flow; changing uSync's sync scope/handlers (what content types it does or doesn't serialize); changing the `theme/` submodule wiring in `Program.cs`; granting the `THEME_REPO_PAT` any permission beyond read-only.
- **Never do:** commit secrets, backoffice credentials, deploy keys, or connection strings with real values, anywhere — including inside `uSync/` export files; commit the SQLite `.db` file or the `umbraco/Data` runtime folder; let the deploy job run when build or test has failed; overwrite the live SQLite database/content during a deploy; commit directly to `main` or `develop` (all changes come in via PR from a `feature/*`, `release/*`, or `hotfix/*` branch); assume the repo is private when deciding what's safe to commit — it is public; **ever** commit any of the purchased template's original files (HTML/CSS/JS/images/fonts/video) or Razor/CSS closely derived from them into this repo's tracked files — its license forbids redistribution, permanently, not pending confirmation; copy `theme/` submodule content into a directory this repo tracks (e.g. as a "just this once" build workaround) — the whole point of the submodule is that content never lands here.

## Success Criteria
- `dotnet build` and `dotnet test` succeed from a clean checkout.
- `dotnet run` starts the site locally, the Umbraco backoffice at `/umbraco` is reachable, and an admin account can be created on first run.
- A "Home" document type + template exists and renders content that's editable through the backoffice.
- A custom 404 page (matching the template's design) is served with a genuine HTTP 404 status for any unmatched route, configured through Umbraco's error-page mechanism rather than hardcoded routing.
- Umbraco's SQLite database initializes cleanly on first run with no manual DB setup steps.
- Creating/editing the Home document type and its content locally produces uSync export files under `uSync/` that, when committed and deployed, reproduce the same structure and content on the VPS without ever transferring the `.db` file.
- A fresh clone + `dotnet run` on a machine with no prior database reconstructs the expected content/schema purely from the committed `uSync/` folder — with the `theme/` submodule checked out; without it, the same fresh clone still builds and runs, just with no front-end presentation, and that's correct behavior, not a bug.
- The rendered Home page visually matches the purchased template (layout, styling, responsive behavior), served through Umbraco/Razor rather than as static files, with the actual template code living only in the private `neonpixel-theme` repo.
- `git log` / `git show` on this public repo, at every point in its history, contains zero bytes of the purchased template's own markup, CSS, JS, images, video, or fonts — only a submodule commit-hash reference to the private repo.
- A PR from a `feature/*` branch into `develop` triggers CI (build+test) automatically.
- A push to `main` (from a merged `release/*` or `hotfix/*` branch) triggers the deploy workflow, which builds, tests, publishes, and deploys to the VPS, restarting the service, without manual intervention.
- A failing build or test run does not reach the VPS — the live site is left untouched.
- A documented, reproducible path exists to run the published app under nginx + systemd on Ubuntu 26.04 (this spec covers the app and its CI/CD; initial VPS provisioning is a prerequisite captured in the Plan phase).

## Open Questions
1. Exact Umbraco 18 → .NET version compatibility and any prerequisite packages — must be confirmed against official Umbraco docs at Plan time (this postdates the assistant's training data).
2. What does the Home page actually need to say/show (hero copy, imagery, brand colors)? No design/brand direction was given yet.
3. Domain name and DNS — not yet decided, needed before HTTPS/certbot steps can be finalized.
4. Is SQLite acceptable long-term for Umbraco, or is this a stepping stone to SQL Server once the site grows?
5. Backoffice admin account — who is the initial admin user, and how are credentials handled outside of source control?
6. VPS prerequisites (deploy user, SSH key provisioning, .NET runtime/nginx/systemd unit installed) — is this already done, or does the Plan phase need to include initial server setup steps?
7. Deploy directory path and systemd service/unit name on the VPS — not yet specified, needed to write the actual workflow file.
8. Rollback strategy if a deploy succeeds but the new build is broken in production — not yet defined (e.g. keep last N releases, symlink swap, manual revert).
9. Is there a staging environment (deployed from `develop` or `release/*`), or is production the only environment for now? Currently assumed single-environment.
10. Branch protection rules for `main`/`develop` (required reviews, required status checks) — not yet specified, likely a GitHub repo setting decided at Plan/setup time rather than in this spec.
11. uSync version compatible with Umbraco 18 — postdates this assistant's training data, must be confirmed against official uSync/Umbraco compatibility docs at Plan time.
12. uSync sync mode — does it export automatically on every content save (so `uSync/` is always current), or does an editor/developer need to manually trigger "Report/Export" in the backoffice before committing? Affects the day-to-day workflow for content edits.
13. Since the repo is public, does the *initial* history need scrubbing/squashing before going public (e.g. if any placeholder secrets were ever committed during scaffolding), or is this a fresh repo from the start?
14. **Resolved:** the template may not be redistributed — see Assumption 14/17. Front-end code lives in a private `neonpixel-theme` submodule instead of this repo. Follow-on questions below are new, not replacements for this one.
15. What's the exact template name/marketplace/purchase source? Still not stated — useful to have on file (e.g. in the private theme repo's own README) in case the license terms ever need re-checking, or a renewal/extended license is needed later.
16. Uses Bootstrap + jQuery (confirmed from `source-files/`) alongside GSAP, Swiper, Lenis, Matter.js, and others — need to confirm none of these conflict with Umbraco's own backoffice scripts/styles when the site is running (backoffice and front-end are normally isolated in Umbraco, but worth a Plan-phase sanity check).
17. **Resolved:** the private repo already exists at `neonpixel-software/neonpixel-theme` (same org as this repo), and already contains the raw purchased template under `HTML/` (private storage is fine — the license only restricts *public* redistribution). CI/CD access is via a fine-grained PAT (`THEME_REPO_PAT`, see Assumption 19), not a deploy key.
18. Does the theme repo need any license/attribution notice of its own (e.g. a private README noting the commercial template it's derived from and that it must never go public), so a future maintainer doesn't accidentally make *that* repo public too?
19. **Partially resolved:** implemented and the static-file half verified end-to-end (a test file in `theme/wwwroot` served correctly at runtime). The Razor-view half uses `MvcRazorRuntimeCompilationOptions.FileProviders`, which works because `Umbraco.Cms.DevelopmentMode.Backoffice` (referenced unconditionally in the scaffold) already requires runtime compilation for `ModelsMode InMemoryAuto` — so this isn't a new dependency, just reuse of one the project already has. **New residual risk:** this breaks if the project ever moves off `InMemoryAuto` models mode or drops that dev-mode package (a plausible production-hardening step) — at that point the theme views need to move to an MSBuild-level Razor compile-include instead of a runtime file provider. Full runtime proof that a `theme/Views` `.cshtml` actually renders against real Umbraco content is still pending — deferred to Task 12, once a document type + content exist to render against.
