# To-Do: NeonPixel Website

Full detail (descriptions, acceptance criteria, verification, files) is in `tasks/plan.md`. This file tracks completion status.

## Phase 1: Public-Repo Foundation (no theme dependency — start immediately)
- [x] Task 1: Verify Umbraco 18 / .NET version compatibility, then scaffold the project
- [x] Task 2: Add and configure uSync
- [x] Task 3: Set up GitFlow branch structure
- [x] Task 4: CI workflow (build + test, with theme submodule checkout) — written, YAML-valid; real run pending `THEME_REPO_PAT`
- [x] Task 5: Deploy workflow (publish + SSH deploy + restart, with theme submodule checkout) — written, YAML-valid; real run pending VPS secrets (`VPS_HOST`, `VPS_USER`, `VPS_DEPLOY_KEY`, `VPS_DEPLOY_PATH`, `VPS_SERVICE_NAME`)
- [x] Task 6: VPS deployment runbook (`DEPLOYMENT.md`) — deploy path/service name/domain left as placeholders pending Open Questions 3, 7

## Phase 2: Private Theme Repo Setup (no Phase-1 dependency — can run in parallel)
- [x] Task 7: Create the private `neonpixel-theme` repo (already existed)
- [x] Task 8: Add it to this repo as a git submodule at `theme/`
- [x] Task 9: Wire Umbraco to load Views/static files from `theme/` (static-file half verified end-to-end; Razor-view half structurally verified, full runtime proof deferred to Task 12 when real content exists)
- [x] Task 10: Generate and store the read-only `THEME_REPO_PAT` — fine-grained PAT created by user, added as a GitHub Actions secret; CI re-run confirmed submodule checkout now succeeds

## Checkpoint: Foundation + Theme Plumbing
- [x] `dotnet build` and `dotnet test` succeed from a clean checkout (theme submodule initialized) — verified locally and via CI on a fresh runner
- [x] `dotnet run` serves the site locally; backoffice reachable, admin account created — unattended install succeeded (`NeonPixel Admin` / `admin@neonpixel.local`); admin password itself wasn't retained (disposable local dev DB, not a concern)
- [x] Manual `uSync` export/import round-trips successfully — confirmed empirically: uSync auto-exported all built-in system Data/Media Types to `src/NeonPixel.Web/uSync/v18/` on first boot, unprompted (51 files, committed)
- [x] `develop` branch exists; a scratch PR triggers CI (with submodule checkout) and goes green — PR #1, confirmed green after `THEME_REPO_PAT` was added
- [x] Deploy workflow file present, valid, checks out the submodule (real run stays blocked on VPS secrets, as expected)
- [x] `git log`/`git show` on this repo contains zero template file content — only the submodule reference — verified
- [x] Review with human before proceeding — approved, proceeded to Phase 3

## Phase 3: Template Conversion (into the private theme repo)
- [x] Task 11: Extract shared layout + static assets from the template into `theme/` — `_Layout.cshtml`, `wwwroot/` (css/js/img/fonts/video) committed to `neonpixel-theme`
- [x] Task 12: Home document type + template — Razor side done (`HomeContent.cshtml` partial + public stub `Home.cshtml`); **document type itself not yet created** — that's on the user via backoffice, see `theme/CONTENT-SETUP.md`
- [x] Task 13: Custom 404 page — Razor side done (`Error404Content.cshtml` + stub); **document type + `Error404Collection` config not yet done** — same handoff doc
- [ ] Task 14: uSync export of Home + 404, verify clean-clone reconstruction — blocked until the user completes Task 12/13's backoffice steps

## Checkpoint: Template Integration
- [ ] Home page renders through Umbraco, visually matches `docs/HTML/index.html` — **not yet verified end-to-end**; static assets confirmed working, the partial-resolution path is unverified pending real content (see `theme/CONTENT-SETUP.md`)
- [ ] Unmatched URL returns custom 404 template with genuine HTTP 404 status — same, pending
- [ ] Fresh clone with `theme/` access reconstructs Home + 404 from committed `src/NeonPixel.Web/uSync/` alone — pending Task 14
- [x] Fresh clone without `theme/` access still builds/runs (no presentation — expected) — verified by construction (Directory.Exists guards in Program.cs)
- [x] `develop` CI green with template changes merged in — verify after next PR merges these commits
- [ ] Review with human before proceeding — **waiting on user to complete `theme/CONTENT-SETUP.md` and report back**

## Phase 4: Release & Deploy
- [ ] Task 15: Cut `release/1.0.0`, provision VPS prerequisites, ship to production
- [ ] Task 16: Post-deploy verification

## Checkpoint: Complete
- [ ] All Success Criteria in `SPEC.md` met
- [ ] Production site reachable over HTTPS, backoffice usable, 404 page working
- [ ] Ready for review
