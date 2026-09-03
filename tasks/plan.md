# Implementation Plan: NeonPixel Website

Source spec: `../SPEC.md` (read that first — this plan assumes its Assumptions/Boundaries/Open Questions).

## Overview
Scaffold an Umbraco 18 CMS site (SQLite persistence, uSync for content/schema sync), wire up GitFlow branching with a two-workflow GitHub Actions pipeline (CI on `develop`, deploy to the VPS on `main`), then convert the purchased HTML template at `docs/HTML/` into Umbraco Razor templates for the Home page and a custom 404 page. The template's license forbids redistribution, so the converted front-end (Razor views, CSS/JS/images) lives permanently in a separate private repo (`neonpixel-theme`), pulled into this public repo as a git submodule and loaded directly from that path — never copied into a directory this repo tracks. Foundation work (Phase 1) and the theme-repo setup (Phase 2) have no ordering dependency on each other and can proceed in parallel; actual template conversion (Phase 3) needs both done first.

## Architecture Decisions
- **Umbraco 18 CMS, not hand-rolled EF Core** — content (including the Home page) lives in Umbraco's content tree, edited via the backoffice, not custom DbContext entities. Rationale: explicit user requirement.
- **uSync for environment sync, not the SQLite file itself** — schema/content move between local dev and production as serialized files in `src/NeonPixel.Web/uSync/` (committed), while `umbraco/Data/*.sqlite` stays environment-local and gitignored. Rationale: avoids shipping a binary database file through git, and avoids merge conflicts on it.
- **GitFlow with a single deploy target** — only `main` triggers the deploy job; `develop` and `feature/*` only run CI. Rationale: there's one VPS/production environment; a staging deploy from `develop` is explicitly an open question, not assumed.
- **Template-derived front-end lives in a private submodule, never copied into this repo** — the purchased template's license forbids redistribution, and this repo is public. A separate private `neonpixel-theme` repo holds the converted Razor/CSS/JS; this repo references it as a git submodule at `theme/` (a commit-hash pointer only) and is configured (`Program.cs`) to load Views and static files directly from `theme/Views` / `theme/wwwroot` at runtime, so the actual template content never enters this repo's git history. Rationale: a submodule pointer is the standard, low-friction way to compose a public repo with private-licensed content, and avoids the maintenance burden of a copy-at-build-time step going stale.

## Task List

### Phase 1: Public-Repo Foundation (no theme dependency — start immediately)
- [ ] Task 1: Verify Umbraco 18 / .NET version compatibility, then scaffold the project
- [ ] Task 2: Add and configure uSync
- [ ] Task 3: Set up GitFlow branch structure
- [ ] Task 4: CI workflow (build + test, with theme submodule checkout)
- [ ] Task 5: Deploy workflow (publish + SSH deploy + restart, with theme submodule checkout)
- [ ] Task 6: VPS deployment runbook

### Phase 2: Private Theme Repo Setup (no Phase-1 dependency — can run in parallel)
- [ ] Task 7: Create the private `neonpixel-theme` repo
- [ ] Task 8: Add it to this repo as a git submodule at `theme/`
- [ ] Task 9: Wire Umbraco to load Views/static files from `theme/`
- [ ] Task 10: Generate and store the read-only `THEME_REPO_PAT`

### Checkpoint: Foundation + Theme Plumbing
- [ ] `dotnet build` and `dotnet test` succeed from a clean checkout (with `theme/` submodule initialized, even if empty of real content so far)
- [ ] `dotnet run` serves the site locally; backoffice at `/umbraco` reachable, admin account created
- [ ] Manual `uSync` export/import round-trips successfully against a throwaway content change
- [ ] `develop` branch exists; a test PR from a scratch `feature/*` branch triggers the CI workflow (including submodule checkout) and goes green
- [ ] Deploy workflow file is present, syntactically valid, and checks out the submodule (actual deploy run stays blocked until VPS secrets/prerequisites exist — see Task 5/6 and SPEC.md Open Questions 6, 7)
- [ ] `theme/` submodule resolves to the private `neonpixel-theme` repo; `git log`/`git show` on this repo contains no template file content, only the submodule reference
- [ ] Review with human before proceeding

### Phase 3: Template Conversion (into the private theme repo)
- [ ] Task 11: Extract shared layout + static assets from the template into `theme/`
- [ ] Task 12: Home document type + template
- [ ] Task 13: Custom 404 page
- [ ] Task 14: uSync export of Home + 404, verify clean-clone reconstruction

### Checkpoint: Template Integration
- [ ] Home page renders through Umbraco and visually matches `docs/HTML/index.html`
- [ ] Requesting an unmatched URL returns the custom 404 template with a genuine HTTP 404 status
- [ ] A fresh clone (with `theme/` submodule access) + `dotnet run` (no prior database) reconstructs Home + 404 content/schema purely from committed `src/NeonPixel.Web/uSync/` files
- [ ] A fresh clone *without* `theme/` submodule access still builds and runs (no front-end presentation, which is correct, not a bug)
- [ ] `develop` CI is green with template changes merged in
- [ ] Review with human before proceeding

### Phase 4: Release & Deploy
- [ ] Task 15: Cut `release/1.0.0`, provision VPS prerequisites, ship to production
- [ ] Task 16: Post-deploy verification

### Checkpoint: Complete
- [ ] All Success Criteria in `SPEC.md` are met
- [ ] Production site reachable over HTTPS at the final domain, backoffice usable, 404 page working
- [ ] Ready for review

## Task Details

### Task 1: Verify Umbraco 18 compatibility, then scaffold the project
**Description:** Before scaffolding, check Umbraco 18's official system requirements/release notes to confirm the target .NET version (SPEC.md assumes .NET 10 but flags this as unverified since it postdates training data). Then run `dotnet new install Umbraco.Templates` and `dotnet new umbraco -n NeonPixel.Web -o src/NeonPixel.Web`, confirm it builds and runs, and create the initial backoffice admin account.

**Acceptance criteria:**
- [ ] Confirmed .NET target version matches what Umbraco 18 actually requires (update SPEC.md Assumption 3 if it differs from .NET 10)
- [ ] `dotnet run --project src/NeonPixel.Web` starts the site and `/umbraco` is reachable
- [ ] Initial admin account created (credentials handled per SPEC.md — never committed)

**Verification:**
- [ ] Build succeeds: `dotnet build`
- [ ] Manual check: backoffice login works, default Umbraco welcome content renders

**Dependencies:** None

**Files likely touched:**
- `src/NeonPixel.Web/**` (new, from template scaffold)
- `NeonPixel.sln` (or equivalent solution file)
- `SPEC.md` (only if the .NET version assumption needs correcting)

**Estimated scope:** M (scaffold is large in file count but mechanical/generated)

---

### Task 2: Add and configure uSync
**Description:** Add the `uSync` NuGet package to the Umbraco project and configure it (export/import handlers, `ImportAtStartup` or equivalent) so backoffice changes serialize to a `src/NeonPixel.Web/uSync/` folder and reimport cleanly on a fresh database.

**Acceptance criteria:**
- [ ] `uSync` package installed and its backoffice section appears
- [ ] A manual content/schema change locally produces files under `src/NeonPixel.Web/uSync/`
- [ ] Deleting the local SQLite db and restarting reconstructs the same content from `src/NeonPixel.Web/uSync/`

**Verification:**
- [ ] Build succeeds: `dotnet build`
- [ ] Manual check: export → wipe db → restart → content reappears unchanged

**Dependencies:** Task 1

**Files likely touched:**
- `src/NeonPixel.Web/NeonPixel.Web.csproj`
- `src/NeonPixel.Web/appsettings.json`
- `src/NeonPixel.Web/uSync/**` (generated)

**Estimated scope:** S

---

### Task 3: Set up GitFlow branch structure
**Description:** Create the `develop` branch from `main`. Document (in a short section of `tasks/plan.md` or a repo README note) that branch protection rules for `main`/`develop` (required reviews, required status checks — including path-scoped protection on workflow files, per SPEC.md's `THEME_REPO_PAT` risk note) are a GitHub repo setting to be configured by the human — not something this task can enforce from the CLI alone.

**Acceptance criteria:**
- [ ] `develop` branch exists and is pushed
- [ ] Human has been told branch protection is a manual GitHub Settings step (SPEC.md Open Question 10), including protecting workflow-file changes given the theme deploy key risk

**Verification:**
- [ ] Manual check: `git branch -a` shows `develop`; a scratch `feature/*` branch can be created from it

**Dependencies:** Task 1 (needs an initial commit to branch from)

**Files likely touched:** None (branch operation only)

**Estimated scope:** XS

---

### Task 4: CI workflow (build + test, with theme submodule checkout)
**Description:** Write `.github/workflows/ci.yml`: on push to `develop` and on pull requests targeting `develop` or `main`, check out this repo plus the `theme/` submodule (`actions/checkout` with `submodules: true` and `token: ${{ secrets.THEME_REPO_PAT }}` — a fine-grained PAT, since `neonpixel-software` has deploy keys disabled org-wide), then run `dotnet build` then `dotnet test`. No deploy step.

**Acceptance criteria:**
- [ ] Workflow triggers on the specified events only
- [ ] Submodule checkout succeeds using the read-only PAT (depends on Task 10)
- [ ] A red build/test fails the check and blocks a PR from looking mergeable
- [ ] A green build/test passes

**Verification:**
- [ ] Manual check: open a scratch PR from a `feature/*` branch into `develop`, confirm the workflow runs (including submodule checkout) and reports status

**Dependencies:** Task 1, Task 3, Task 10 (needs the PAT to exist as a secret)

**Files likely touched:**
- `.github/workflows/ci.yml`

**Estimated scope:** S

---

### Task 5: Deploy workflow (publish + SSH deploy + restart, with theme submodule checkout)
**Description:** Write `.github/workflows/deploy.yml`: on push to `main`, check out this repo plus `theme/` (same submodule auth as Task 4), run build+test (stop if either fails), `dotnet publish`, copy the output — including the resolved theme content it depends on — to the VPS over SSH (rsync/scp) using secrets (`VPS_HOST`, `VPS_USER`, `VPS_DEPLOY_KEY`, deploy path), then restart the systemd service. Exclude the live SQLite db and `umbraco/Data` from whatever gets overwritten; include the committed `src/NeonPixel.Web/uSync/` folder so uSync's startup import picks up any changes.

**Acceptance criteria:**
- [ ] Deploy job only runs after build+test pass, only on `main`
- [ ] Theme submodule content is present in the published output the VPS receives
- [ ] Live database/content is never overwritten by the rsync step
- [ ] Workflow references secrets by name, nothing environment-specific hardcoded

**Verification:**
- [ ] Manual check: workflow YAML is valid (`act` or GitHub's own linting, or a dry run once secrets exist); a real run is blocked until VPS prerequisites (Task 6) and secrets are in place — that's expected and not a task failure

**Dependencies:** Task 1, Task 3, Task 10

**Files likely touched:**
- `.github/workflows/deploy.yml`

**Estimated scope:** S

---

### Task 6: VPS deployment runbook
**Description:** Write a `DEPLOYMENT.md` at the repo root documenting VPS prerequisites: deploy user creation, SSH key generation for GitHub Actions (VPS deploy key — distinct from the theme repo's deploy key), .NET runtime install, an nginx reverse-proxy config template (Kestrel upstream), a systemd unit file template, and certbot/Let's Encrypt HTTPS setup steps. This resolves SPEC.md Open Questions 6 and 7 by making the prerequisites and deploy path/service name explicit and repeatable — actual execution on the real VPS is a manual step for the human (see Task 15), not automated here.

**Acceptance criteria:**
- [ ] Runbook lists every prerequisite from SPEC.md Assumption 8 as an explicit, ordered step
- [ ] Includes copy-pasteable nginx config and systemd unit templates with placeholders for domain/paths
- [ ] Deploy directory path and systemd unit name are decided and match what Task 5's workflow expects

**Verification:**
- [ ] Manual check: a human unfamiliar with the project could follow it start-to-finish (self-review, since no second reviewer is specified)

**Dependencies:** None (can run in parallel with all other tasks)

**Files likely touched:**
- `DEPLOYMENT.md`

**Estimated scope:** S

---

### Task 7: Create the private `neonpixel-theme` repo
**Description:** Create a new, private GitHub repository (`neonpixel-theme`) to hold the template-derived Razor views and static assets, permanently. Add a short private README noting it's derived from a purchased template with a no-redistribution license, and must never be made public or forked publicly (resolves SPEC.md Open Question 18).

**Acceptance criteria:**
- [ ] Repo exists, visibility is Private
- [ ] README states the license constraint explicitly, for future maintainers
- [ ] Repo structure anticipates `Views/` and `wwwroot/` subfolders (matching what Task 9's wiring will expect)

**Verification:**
- [ ] Manual check: visit the repo, confirm it's private and the README is present

**Dependencies:** None — but needs a human decision on which GitHub account/org owns it (SPEC.md Open Question 17)

**Files likely touched:** None in this repo (new repo is separate)

**Estimated scope:** XS

---

### Task 8: Add the theme repo as a git submodule
**Description:** In this repo, run `git submodule add <neonpixel-theme URL> theme` and commit the resulting `.gitmodules` + gitlink. Confirm `git submodule update --init --recursive` on a fresh clone (with access) checks out the private repo's content at `theme/`.

**Acceptance criteria:**
- [ ] `.gitmodules` references the private repo
- [ ] `theme/` resolves correctly after `git submodule update --init --recursive`
- [ ] No file content from the private repo appears in this repo's own tracked tree or history — only the gitlink

**Verification:**
- [ ] Manual check: `git show HEAD -- theme` shows only a commit-hash reference, not file contents; `git log -p -- theme` across history confirms the same

**Dependencies:** Task 1 (repo needs to exist), Task 7

**Files likely touched:**
- `.gitmodules`
- `theme` (gitlink entry)

**Estimated scope:** XS

---

### Task 9: Wire Umbraco to load Views/static files from `theme/` — DONE
**Description:** Added to `Program.cs`: `theme/wwwroot` is registered as an additional static file source via `UseStaticFiles` with a `PhysicalFileProvider`; `theme/Views` is registered via `MvcRazorRuntimeCompilationOptions.FileProviders`. Both are guarded by `Directory.Exists` so a clone without submodule access still builds and runs.

**What was actually found (differs from the original guess):** `RazorViewEngineOptions` has no `FileProviders` member — that lives on `MvcRazorRuntimeCompilationOptions` (`Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation`), which is marked obsolete (`ASPDEPR003`) as a *general* .NET 10 recommendation against runtime compilation in production. However, this project already depends on it regardless of this change: `Umbraco.Cms.DevelopmentMode.Backoffice` (referenced unconditionally in the scaffolded `.csproj`) pulls in that same package, and `RazorCompileOnBuild`/`RazorCompileOnPublish` are set to `false` specifically because Umbraco's `ModelsMode InMemoryAuto` requires runtime compilation to work at all. So this reuses a mechanism the project already needs, rather than introducing a new deprecated dependency — documented with a code comment and a `#pragma warning disable ASPDEPR003` around just that block. **Residual risk, carried into SPEC.md Open Question 19:** if the project ever moves off `ModelsMode InMemoryAuto` or drops the dev-mode package (a plausible production hardening step), this wiring breaks and needs to become an MSBuild-level Razor compile-include of `theme/Views` instead of a runtime file provider.

**Acceptance criteria:**
- [x] A static asset placed in `theme/wwwroot` is servable via its expected URL path — verified: a throwaway `theme/wwwroot/wiring-test.txt` returned HTTP 200 with correct content at `/wiring-test.txt`, then removed (never committed to either repo)
- [ ] A `.cshtml` file placed in `theme/Views` renders correctly for a matching Umbraco content node — not yet verified at runtime (needs a configured database + content, which doesn't exist yet); structurally correct (builds clean, same file-provider mechanism as the static-file half, which did verify). Full proof deferred to Task 12.
- [x] No template content was copied into `src/NeonPixel.Web/Views` or `src/NeonPixel.Web/wwwroot` to make this work

**Verification:**
- [x] Build succeeds: `dotnet build` (0 warnings, 0 errors, with the pragma in place)
- [x] Manual check: throwaway test file in `theme/wwwroot` loaded correctly via HTTP; confirmed absent from this repo's and the theme repo's tracked files afterward

**Dependencies:** Task 1, Task 8

**Files touched:**
- `src/NeonPixel.Web/Program.cs`

**Estimated scope:** S

---

### Task 10: Generate and store the read-only `THEME_REPO_PAT`
**Description:** Originally planned as an SSH deploy key, but `neonpixel-software` has deploy keys disabled org-wide (`deploy_keys_enabled_for_repositories: false`, confirmed via `gh api orgs/neonpixel-software` — a deliberate admin policy, not overridden). Instead: create a fine-grained Personal Access Token at github.com/settings/tokens?type=beta — Resource owner `neonpixel-software`, Repository access restricted to only `neonpixel-theme`, Permissions: Contents → Read-only. This requires the GitHub web UI (no API to mint fine-grained PATs); a human has to do this step. Store the token as a GitHub Actions secret (`THEME_REPO_PAT`) on the public `neonpixel-website` repo, for Tasks 4 and 5 to consume via `actions/checkout`'s `token` input.

**Acceptance criteria:**
- [ ] Fine-grained PAT created, scoped to only `neonpixel-theme`, Contents: Read-only
- [ ] Corresponding secret `THEME_REPO_PAT` exists on `neonpixel-website`'s Actions secrets
- [ ] Token confirmed read-only (no accidental write access that could let a compromised workflow push to the private theme repo)
- [ ] Token has a set expiration, with a reminder to rotate it before then (fine-grained PATs don't auto-renew)

**Verification:**
- [ ] Manual check: attempt a checkout using the token in a scratch workflow run, confirm it succeeds for read/clone

**Dependencies:** Task 7

**Files likely touched:** None (GitHub repo/secret configuration only)

**Estimated scope:** XS

---

### Task 11: Extract shared layout + static assets from the template into `theme/`
**Description:** Convert the shared chrome of `docs/HTML/index.html` (head, nav, footer, global scripts) into an Umbraco master Razor layout, committed to the **private `neonpixel-theme` repo** (never this one). Copy the referenced CSS/JS/img/fonts/video into `theme/wwwroot/`, keeping third-party libraries (Bootstrap, jQuery, GSAP, Swiper, Lenis, Matter.js, etc.) as-is and preserving their license files alongside them.

**Acceptance criteria:**
- [ ] Master layout renders the template's nav/footer/global scripts with no visual difference from the static template
- [ ] All referenced assets resolve (no 404s in browser devtools) via the `theme/wwwroot` wiring from Task 9
- [ ] Third-party library license files are preserved alongside the copied assets
- [ ] All of the above is committed only to `neonpixel-theme`, never to `neonpixel-website`

**Verification:**
- [ ] Build succeeds: `dotnet build` (in `neonpixel-website`, with `theme/` submodule updated to the new commit)
- [ ] Manual check: load the site locally, compare against `docs/HTML/index.html` open directly in a browser

**Dependencies:** Task 9, **local reference copy at `docs/HTML/`** (never committed anywhere)

**Files likely touched (in `neonpixel-theme`, not this repo):**
- `Views/Shared/master.cshtml` (or equivalent layout path)
- `wwwroot/css/**`, `wwwroot/js/**`, `wwwroot/img/**`, `wwwroot/fonts/**`, `wwwroot/video/**`

**Files touched in this repo:**
- `theme` (gitlink bump to the new `neonpixel-theme` commit)

**Estimated scope:** M

---

### Task 12: Home document type + template
**Description:** In the backoffice, create a "Home" document type with properties for each editable content section on the template's homepage (hero title/body, section content, etc.), then build the corresponding Razor template (in `neonpixel-theme`) rendering `docs/HTML/index.html`'s page-specific markup against those properties.

**Acceptance criteria:**
- [ ] Home document type exists with properties covering the page's editable content
- [ ] Home template renders visually matching the source template
- [ ] Editing content in the backoffice changes the rendered page

**Verification:**
- [ ] Build succeeds: `dotnet build`
- [ ] Manual check: edit a field in the backoffice, confirm it reflects on the front end

**Dependencies:** Task 11

**Files likely touched (in `neonpixel-theme`):**
- `Views/Home.cshtml`

**Files touched in this repo:**
- `theme` (gitlink bump)
- Backoffice-created document type (captured to disk in Task 14 via uSync, not hand-written)

**Estimated scope:** M

---

### Task 13: Custom 404 page
**Description:** Create an error/404 document type + Razor template (in `neonpixel-theme`) from `docs/HTML/404.html`, and configure Umbraco's `Error404Collection` (or equivalent, in this repo's `appsettings.json`) so unmatched routes serve it with a genuine HTTP 404 status.

**Acceptance criteria:**
- [ ] Requesting a nonexistent URL returns the custom 404 template
- [ ] Response status code is 404, not 200
- [ ] Visual match with `docs/HTML/404.html`

**Verification:**
- [ ] Manual check: `curl -I` a nonexistent local URL, confirm `404` status; view the page in a browser

**Dependencies:** Task 11

**Files likely touched (in `neonpixel-theme`):**
- `Views/Error404.cshtml` (or equivalent)

**Files touched in this repo:**
- `theme` (gitlink bump)
- `src/NeonPixel.Web/appsettings.json` (`Error404Collection` config)

**Estimated scope:** S

---

### Task 14: uSync export of Home + 404, verify clean-clone reconstruction
**Description:** Export the Home and 404 document types/content via uSync, commit the resulting `src/NeonPixel.Web/uSync/` files (to this repo — content structure is Umbraco/uSync's, not the template's, so no license concern here), and verify a completely fresh clone (no local db, no prior uSync state) reconstructs the same site on `dotnet run`, both with and without `theme/` submodule access.

**Acceptance criteria:**
- [ ] `src/NeonPixel.Web/uSync/` contains the Home and 404 document type + content definitions
- [ ] A fresh clone with submodule access, after `dotnet run`, shows the same Home/404 content without manual backoffice work
- [ ] A fresh clone without submodule access still builds/runs (backend only, no presentation — expected)
- [ ] No secrets or environment-specific values appear in the exported files (per SPEC.md Boundaries)

**Verification:**
- [ ] Manual check: clone to a scratch directory (with and without submodule init), `dotnet run`, compare behavior

**Dependencies:** Task 12, Task 13, Task 2

**Files likely touched:**
- `src/NeonPixel.Web/uSync/**`

**Estimated scope:** S

---

### Task 15: Cut release, provision VPS, ship to production
**Description:** Once the Template Integration checkpoint passes and the VPS is provisioned per `DEPLOYMENT.md` (Task 6), cut `release/1.0.0` from `develop`, merge into `main` and `develop`, tag it. The `main` merge triggers the deploy workflow (Task 5) with real secrets configured in GitHub (both `VPS_DEPLOY_KEY` and `THEME_REPO_PAT`). This is where the human executes the VPS-side prerequisite steps (deploy user, SSH key, nginx, systemd, certbot) — those are infrastructure actions outside this repo's code and are called out here, not automated.

**Acceptance criteria:**
- [ ] VPS prerequisites from `DEPLOYMENT.md` are complete
- [ ] Both GitHub Actions secrets (VPS + theme repo) configured
- [ ] Push to `main` triggers a successful deploy; site reachable on the VPS
- [x] Domain decided (`neonpixel.eu`) and `Umbraco:CMS:Runtime:Mode: "Production"` now set explicitly, in a new `appsettings.Production.json` (2026-09-01) — see SPEC.md Open Question 25. Its prerequisites (HTTPS, `UmbracoApplicationUrl`, Release-mode build) are all satisfied; verified end-to-end with a real Production-mode Release build.

**Verification:**
- [ ] Manual check: visit the deployed site over HTTPS; confirm `/umbraco` backoffice loads on production

**Dependencies:** Checkpoint: Template Integration; Task 5; Task 6 (executed, not just written). ~~New hard blocker confirmed during Task 12 testing~~ — **resolved 2026-09-01**, see SPEC.md Open Question 19: the `theme/` rendering approach no longer depends on `ASPNETCORE_ENVIRONMENT`, verified against a real Production-mode Release build.

**Files likely touched:** None in-repo beyond the release merge/tag itself (unless the runtime-compilation fix above requires it — TBD)

**Estimated scope:** S (in-repo) + manual ops work (not file-scoped)

---

### Task 16: Post-deploy verification
**Description:** Confirm the production site meets SPEC.md's Success Criteria end-to-end: HTTPS works, Home and 404 pages render correctly, backoffice is reachable and login works, and a subsequent push to `main` (e.g. a trivial content tweak) redeploys cleanly.

**Acceptance criteria:**
- [ ] All SPEC.md Success Criteria checked off against the live site
- [ ] A second deploy (redeploy) works without manual intervention

**Verification:**
- [ ] Manual check: run through the Success Criteria list in `SPEC.md` against the production URL

**Dependencies:** Task 15

**Files likely touched:** None

**Estimated scope:** XS

## Risks and Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| Theme content accidentally lands in the public repo (copy-paste mistake, wrong working directory during conversion, etc.) | High — the entire reason for the submodule architecture is defeated; a public commit can't be fully un-published (forks, caches, history) | Boundaries explicitly forbid it; Checkpoint after Phase 1/2 includes a literal `git log`/`git show` audit of this repo for template content before any template conversion work begins |
| `THEME_REPO_PAT` leaks or is over-scoped, exposing the private theme repo via the public repo's workflows | Medium — a public-repo workflow file is itself a public artifact; anyone can read/propose changes to it | Key is read-only at the deploy-key level (not just convention); branch protection should require review on workflow-file changes (SPEC.md CI/CD notes, Task 3) |
| Umbraco 18 / uSync / .NET version facts postdate this assistant's training | Medium — wrong assumption could mean a broken scaffold or incompatible uSync version | Task 1 starts with a live compatibility check against official docs before any code is generated |
| **Resolved 2026-09-01.** The `theme/` file-provider approach only rendered when `ASPNETCORE_ENVIRONMENT=Development`, which would have broken production. Fixed by moving `theme/Views` to an MSBuild-level Razor compile-include (`Content`/`LinkBase`) and dropping `Umbraco.Cms.DevelopmentMode.Backoffice`/`InMemoryAuto` entirely (`ModelsMode: Nothing` — views use `IPublishedContent` only, no generated models needed). Verified end-to-end with a Release build under `ASPNETCORE_ENVIRONMENT=Production`: real Home/404 content on both languages, genuine 404 status, static assets resolving. See SPEC.md Open Question 19 for full detail. | ~~High~~ Resolved | Done — see SPEC.md Open Question 19 |
| VPS not yet provisioned (deploy user, SSH key, nginx, systemd, .NET runtime) | Medium — deploy workflow can be written but not proven until this exists | Task 6 produces a runbook early; Task 15 is explicitly gated on it being executed |
| Public repo + secrets/PII exposure (general, beyond the theme key) | Medium — a slip here is hard to fully undo | Existing SPEC.md boundaries (secrets only via GH Actions secrets / gitignored local files); uSync exports reviewed before commit |
| Umbraco media library files (uploads) not covered by uSync by default | Low at launch (Home likely uses static template imagery, not backoffice-uploaded media) | Revisit if/when a media-picker field is added; not a launch blocker |
| No staging environment — `main` deploy goes straight to production | Medium | Deploy only fires after CI passes on `main`; consider adding a staging environment later if this proves risky in practice (SPEC.md Open Question 9) |

## Open Questions
Carried from `SPEC.md` (see that file for the full list) — the ones that actively block a task above:
- **Which GitHub account/org owns `neonpixel-theme`, and who needs access** (SPEC.md Q17) — blocks Task 7.
- **VPS deploy path / systemd unit name** (SPEC.md Q7) — needed to finalize Task 5's workflow and Task 6's runbook.
- ~~Domain name / DNS (SPEC.md Q3)~~ — **Resolved 2026-09-01**: `neonpixel.eu`. DNS pointing at the VPS is still a manual step for the human (`DEPLOYMENT.md`'s prerequisites checklist).
- **uSync version compatible with Umbraco 18** (SPEC.md Q11) — needed at Task 2.
- ~~Confirm Umbraco 18's actual view/static-file extension mechanism (SPEC.md Q19)~~ — **Resolved 2026-09-01**, see SPEC.md Q19 and the Risks table above.
