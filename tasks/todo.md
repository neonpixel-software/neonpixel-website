# To-Do: NeonPixel Website

Full detail (descriptions, acceptance criteria, verification, files) is in `tasks/plan.md`. This file tracks completion status.

## Phase 1: Public-Repo Foundation — DONE
- [x] Task 1: Umbraco 18 / .NET 10 scaffold
- [x] Task 2: uSync installed and configured (including the `ImportAtStartup` fix — see Phase 3 notes)
- [x] Task 3: GitFlow branches
- [x] Task 4: CI workflow — green
- [x] Task 5: Deploy workflow — written, real run still blocked (see Phase 4 blockers below)
- [x] Task 6: VPS deployment runbook (`DEPLOYMENT.md`) — placeholders for domain/deploy path still open

## Phase 2: Private Theme Repo Setup — DONE
- [x] Task 7-10: theme submodule, `THEME_REPO_PAT`, Program.cs wiring — all working, CI green

## Phase 3: Template Conversion — DONE
- [x] Task 11: Layout + static assets
- [x] Task 12: Home document type + template — **fully working, verified live in English and Dutch**
- [x] Task 13: Custom 404 page — **fully working, verified live**: real HTTP 404 status for any genuinely broken URL, in both languages, via `Umbraco:CMS:Content:Error404Collection` (one entry per culture, config verified against Umbraco's own schema rather than guessed). Also fixed a leftover dead "Go Home" link in the 404 page's own body content (missed in the original template conversion pass). Note: the content node is *also* directly domain-bound at `/en/404` and `/nl/404` (from the user's own backoffice setup) — harmless, it just means the 404 content is reachable as a normal 200 page there too, in addition to being the fallback for real 404s.
- [x] Task 14: uSync round-trip — **verified working**, including a real fix: `uSync:Settings:ImportAtStartup` was never actually enabled (defaulted to `"None"`), so uSync only ever imported on the very first boot. Now set to `"All"` in the base `appsettings.json` — applies to every environment.

## New: Multilingual (added mid-project, not in the original plan)
- [x] English + Dutch (`nl-NL`) both shipping, not just architecture-for-later
- [x] `/en/` and `/nl/` routing verified live, dynamic language switcher verified working both directions
- [x] `/` → `/en/` redirect verified
- PR #5 (`feature/dutch-language`) is **open, CI green, not yet merged**

## Phase 4: Release & Deploy — code-side prep done, ops/secrets remain
- [x] **Former hard blocker, resolved 2026-09-01:** the `theme/` rendering approach used to only work with `ASPNETCORE_ENVIRONMENT=Development`. Fixed by moving `theme/Views` to an MSBuild-level Razor compile-include (build-time compilation, `ModelsMode: Nothing`, dropped `Umbraco.Cms.DevelopmentMode.Backoffice`/`InMemoryAuto`). Verified end-to-end with a Release build under `ASPNETCORE_ENVIRONMENT=Production`: real Home/404 content in both languages, genuine HTTP 404, static assets resolving; a submodule-less clone still builds clean. See `SPEC.md` Open Question 19. Merged to `develop` via PR #7.
- [x] **Domain decided (2026-09-01): `neonpixel.eu`.** Wired into `DEPLOYMENT.md`'s nginx/certbot steps and a new `src/NeonPixel.Web/appsettings.Production.json`, which also sets `Umbraco:CMS:Runtime:Mode: "Production"` for real (previously deferred), `Global:UseHttps: true`, and `WebRouting:UmbracoApplicationUrl`. Also added ASP.NET Core Forwarded Headers Middleware in `Program.cs` — needed behind `DEPLOYMENT.md`'s nginx reverse proxy so Umbraco correctly sees HTTPS even though Kestrel itself only speaks plain HTTP internally (a real bug, confirmed against a matching upstream Umbraco-CMS GitHub issue). Verified end-to-end: a Release build under full `ASPNETCORE_ENVIRONMENT=Production` (all production validators active, not just the Open Question 19 rendering path) boots clean and serves correctly, with and without a simulated `X-Forwarded-Proto: https` header. See `SPEC.md` Open Question 25. VPS user confirms provisioning (deploy user, SSH key, .NET runtime, nginx, systemd unit) is already done on their end — not verified from this session, no VPS access here.
- [ ] Task 15: Cut `release/1.0.0`, provision VPS, ship to production — no longer blocked on rendering or domain; still needs: deploy secrets set on GitHub (`VPS_HOST`, `VPS_USER`, `VPS_DEPLOY_KEY`, `VPS_DEPLOY_PATH`, `VPS_SERVICE_NAME` — user is setting these themselves via the GitHub UI, not this session), DNS for `neonpixel.eu` pointed at the VPS, branch protection not configured
- [ ] Task 16: Post-deploy verification

## Checkpoint: Complete
- [ ] All Success Criteria in `SPEC.md` met
- [ ] Production site reachable over HTTPS, backoffice usable, 404 page working
- [ ] Ready for review
