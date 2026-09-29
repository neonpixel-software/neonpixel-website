# NeonPixel Website

Source for [neonpixel.eu](https://neonpixel.eu): an Umbraco 18 CMS site on .NET 10, self-hosted on an Ubuntu VPS behind nginx + systemd.

Editors manage all content through the Umbraco backoffice. Content and schema move between environments via [uSync](https://jumoo.co.uk/usync/) files committed to this repo — the SQLite database itself is never committed.

## Tech stack

- **Umbraco CMS 18.1** on ASP.NET Core / .NET 10, server-rendered Razor views
- **SQLite** via Umbraco's built-in persistence
- **uSync 18.1** for version-controlled content, document types, data types and templates
- **Front-end theme** in a private companion repo, pulled in as a git submodule (see below)
- **GitHub Actions** for CI and deployment; SonarCloud for static analysis

## Repository layout

```
src/NeonPixel.Web/       Umbraco site (Program.cs, appsettings*, Views/, uSync/, appsettings.Local.json.example)
theme/                   Git submodule → private neonpixel-theme repo (Views/ + wwwroot/)
.github/workflows/       ci.yml (PR build + test), deploy.yml (deploy on push to main)
docs/plans/              Design notes
tasks/                   Plan and task list
SPEC.md                  Full project spec: architecture, constraints, open questions
DEPLOYMENT.md            One-time VPS provisioning runbook and rollback steps
```

## The `theme/` submodule

The site's front-end is based on a purchased HTML template whose license **forbids redistribution**. The converted Razor views and static assets (CSS, JS, images, fonts, video) therefore live in the private [`neonpixel-theme`](https://github.com/neonpixel-software/neonpixel-theme) repo. This public repo only tracks a commit reference to it.

- `NeonPixel.Web.csproj` links `theme/Views` and `theme/wwwroot` into the build and publish output.
- `Program.cs` also serves `theme/wwwroot` directly during local `dotnet run`.
- Both are guarded by existence checks. A clone without access to the theme still builds and runs, just without front-end views or assets.

> **Never** copy theme files, or anything derived from the purchased template, into a path this repo tracks. `docs/HTML/` (the original template, local reference only) is permanently gitignored.

Theme SCSS/JS is compiled inside the theme repo (`npm run build`). The compiled output is committed there, so neither this repo nor CI needs Node.

### Updating the theme

```sh
git -C theme fetch origin
git -C theme checkout origin/main
dotnet build                      # verify
git add theme
git commit -m "Update theme submodule to <short-sha>"
```

## Getting started

Prerequisites: .NET 10 SDK, and read access to `neonpixel-theme` for the front-end.

```sh
git clone --recurse-submodules https://github.com/neonpixel-software/neonpixel-website.git
cd neonpixel-website
# already cloned without submodules?
git submodule update --init --recursive

dotnet build
dotnet run --project src/NeonPixel.Web
```

The site runs at `https://localhost:44313`. On first run, Umbraco walks you through creating an admin account. uSync then imports the committed content and schema. The backoffice is at `/umbraco`.

### Local settings

Put local secrets and machine-specific overrides in `src/NeonPixel.Web/appsettings.Local.json`. It's gitignored and only loaded in Development. Start from the template:

```sh
cp src/NeonPixel.Web/appsettings.Local.json.example src/NeonPixel.Web/appsettings.Local.json
```

The template has placeholders for a custom SQLite file and for Umbraco's unattended install. Set `InstallUnattended` to `true` and fill in the admin details to skip the install wizard on a fresh database. Delete any section you don't need.

## Languages and routing

The site is multilingual, with language-prefixed URLs. Dutch is the default: a bare `/` redirects to `/nl/`. English is served under `/en/`. A custom 404 page is configured per culture through Umbraco's `Error404Collection` and returns a genuine HTTP 404.

## Commands

| Task | Command |
| --- | --- |
| Build | `dotnet build` |
| Run | `dotnet run --project src/NeonPixel.Web` |
| Test | `dotnet test` |
| Publish | `dotnet publish src/NeonPixel.Web -c Release -o out` |

There's no custom application logic yet, so there's no test project. xUnit tests go under `tests/NeonPixel.Web.Tests/` once custom code exists.

## Workflow

This repo uses **GitHub Flow**:

1. Branch from `main` as `feature/<name>`. Urgent fixes use the same flow; there's no separate hotfix prefix.
2. Open a PR into `main`. CI builds and tests it, and SonarCloud posts its own status check.
3. Merge after review. Never commit directly to `main`.
4. Tag releases that ship as `vX.Y.Z`.

Dependabot keeps GitHub Actions and NuGet dependencies up to date. Dependabot's CI runs skip the private submodule because they don't get repo secrets.

## Deployment

Every push to `main` runs `.github/workflows/deploy.yml`:

1. Build, test and `dotnet publish`, with the theme checked out using the read-only `THEME_REPO_PAT` secret.
2. rsync the output to a new timestamped `releases/<id>/` directory on the VPS.
3. Symlink the persistent `shared/wwwroot/media` folder into the release.
4. Write the production connection string to the systemd environment file.
5. Atomically repoint the `current` symlink and prune old releases (keeping 5).
6. Restart the systemd service. On startup, uSync applies any shipped content or schema changes.
7. Smoke-test `https://neonpixel.eu/`.

The SQLite database lives outside the deploy directory, so a deploy can never overwrite live content. Connection details come from GitHub Actions secrets and are never committed. See [DEPLOYMENT.md](DEPLOYMENT.md) for server setup, the required secrets and rollback.

## Security notes

This repository is **public**:

- Never commit secrets, connection strings, deploy keys or the SQLite `.db` file.
- Review uSync exports before committing, to make sure nothing environment-specific was serialized.
- Changes to workflow files need careful review. `THEME_REPO_PAT` grants read access to the private theme repo.

## License

[MIT](LICENSE) covers this repository's own code. It does **not** cover the private `neonpixel-theme` content or the purchased template it's derived from.
