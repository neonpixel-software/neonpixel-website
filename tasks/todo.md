# To-Do: NeonPixel Website

Full detail (descriptions, acceptance criteria, verification, files) is in `tasks/plan.md`. This file tracks completion status.

## Phase 1: Public-Repo Foundation (no theme dependency — start immediately)
- [ ] Task 1: Verify Umbraco 18 / .NET version compatibility, then scaffold the project
- [ ] Task 2: Add and configure uSync
- [ ] Task 3: Set up GitFlow branch structure
- [ ] Task 4: CI workflow (build + test, with theme submodule checkout)
- [ ] Task 5: Deploy workflow (publish + SSH deploy + restart, with theme submodule checkout)
- [ ] Task 6: VPS deployment runbook

## Phase 2: Private Theme Repo Setup (no Phase-1 dependency — can run in parallel)
- [ ] Task 7: Create the private `neonpixel-theme` repo
- [ ] Task 8: Add it to this repo as a git submodule at `theme/`
- [ ] Task 9: Wire Umbraco to load Views/static files from `theme/`
- [ ] Task 10: Generate and store the read-only `THEME_REPO_DEPLOY_KEY`

## Checkpoint: Foundation + Theme Plumbing
- [ ] `dotnet build` and `dotnet test` succeed from a clean checkout (theme submodule initialized)
- [ ] `dotnet run` serves the site locally; backoffice reachable, admin account created
- [ ] Manual `uSync` export/import round-trips successfully
- [ ] `develop` branch exists; a scratch PR triggers CI (with submodule checkout) and goes green
- [ ] Deploy workflow file present, valid, checks out the submodule (real run stays blocked on VPS secrets)
- [ ] `git log`/`git show` on this repo contains zero template file content — only the submodule reference
- [ ] Review with human before proceeding

## Phase 3: Template Conversion (into the private theme repo)
- [ ] Task 11: Extract shared layout + static assets from the template into `theme/`
- [ ] Task 12: Home document type + template
- [ ] Task 13: Custom 404 page
- [ ] Task 14: uSync export of Home + 404, verify clean-clone reconstruction

## Checkpoint: Template Integration
- [ ] Home page renders through Umbraco, visually matches `docs/HTML/index.html`
- [ ] Unmatched URL returns custom 404 template with genuine HTTP 404 status
- [ ] Fresh clone with `theme/` access reconstructs Home + 404 from committed `uSync/` alone
- [ ] Fresh clone without `theme/` access still builds/runs (no presentation — expected)
- [ ] `develop` CI green with template changes merged in
- [ ] Review with human before proceeding

## Phase 4: Release & Deploy
- [ ] Task 15: Cut `release/1.0.0`, provision VPS prerequisites, ship to production
- [ ] Task 16: Post-deploy verification

## Checkpoint: Complete
- [ ] All Success Criteria in `SPEC.md` met
- [ ] Production site reachable over HTTPS, backoffice usable, 404 page working
- [ ] Ready for review
