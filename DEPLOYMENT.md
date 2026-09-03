# Deployment Runbook: NeonPixel Website

Manual, one-time VPS provisioning steps for `neonpixel-website`, plus the templates the automated deploy workflow (`.github/workflows/deploy.yml`) expects to already be in place. This is not automated — a human runs through it once per server. See `SPEC.md` for the architecture this supports.

Domain: `neonpixel.eu` (SPEC.md Open Question 3, resolved). DNS must point at the VPS before step 7 (certbot).

Placeholders used below (replace with real values, then set the matching GitHub Actions secrets):
- `<deploy-path>` — e.g. `/opt/neonpixel-web` (becomes the `VPS_DEPLOY_PATH` secret)
- `<service-name>` — e.g. `neonpixel-web` (becomes the `VPS_SERVICE_NAME` secret, systemd unit will be `<service-name>.service`)
- `<deploy-user>` — a dedicated, non-root user the deploy workflow SSHes in as
- `<sqlite-data-dir>` — e.g. `/var/lib/neonpixel-website`, a directory outside `<deploy-path>` for the two paths below (never appears in a committed file itself — see step 4)
- `<sqlite-db-path>` — `<sqlite-data-dir>/neonpixel.sqlite.db` (becomes the `VPS_DB_PATH` secret; never appears in a committed file — see step 4)
- `<env-file-path>` — `<sqlite-data-dir>/app.env` (becomes the `VPS_ENV_FILE` secret; never appears in a committed file — see step 4)
- `<ssh-port>` — the VPS's SSH port (`22` if unchanged from default; becomes the `VPS_SSH_PORT` secret)
- `<vps-host>` — the VPS's hostname or IP (becomes the `VPS_HOST` secret)

## 1. Create a dedicated deploy user

Don't deploy as root or a personal account.

```bash
sudo adduser --disabled-password --gecos "" <deploy-user>
sudo usermod -aG www-data <deploy-user>
```

Grant it passwordless `sudo` for exactly one command — restarting the app's systemd service, nothing broader:

```bash
echo "<deploy-user> ALL=(ALL) NOPASSWD: /bin/systemctl restart <service-name>.service" | sudo tee /etc/sudoers.d/<deploy-user>-restart
sudo chmod 440 /etc/sudoers.d/<deploy-user>-restart
```

## 2. Generate the VPS deploy key (for GitHub Actions → VPS, not the theme repo)

On your own machine (not the VPS):

```bash
ssh-keygen -t ed25519 -f ./neonpixel_vps_deploy_key -N "" -C "neonpixel-website-deploy"
```

Add the **public** key to the deploy user's `authorized_keys` on the VPS:

```bash
ssh-copy-id -p <ssh-port> -i ./neonpixel_vps_deploy_key.pub <deploy-user>@<vps-host>
```

Add the **private** key content as the `VPS_DEPLOY_KEY` GitHub Actions secret on `neonpixel-website`, then delete the local private key file — it only needs to exist in the GitHub secret store from this point on.

Pin the VPS's host key too, so `deploy.yml` can keep `StrictHostKeyChecking=yes` instead of trusting whatever key a MITM'd first connection presents:

```bash
ssh-keyscan -p <ssh-port> <vps-host>
```

Paste the full output (one or more lines) as the `VPS_KNOWN_HOSTS` secret's value, verbatim.

Set the remaining deploy secrets: `VPS_HOST`, `VPS_USER=<deploy-user>`, `VPS_DEPLOY_PATH=<deploy-path>`, `VPS_SERVICE_NAME=<service-name>`, `VPS_DB_PATH=<sqlite-db-path>`, `VPS_ENV_FILE=<env-file-path>`, `VPS_SSH_PORT=<ssh-port>`.

## 3. Install the .NET runtime

Umbraco 18 targets `net10.0` (confirmed via `Umbraco.Templates` 18.1.1 — see SPEC.md Assumption 3). Install the ASP.NET Core runtime (not the full SDK — the VPS only runs published output, it doesn't build):

```bash
wget https://packages.microsoft.com/config/ubuntu/26.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
```

Verify: `dotnet --list-runtimes` should show `Microsoft.AspNetCore.App 10.0.x`.

## 4. Create the releases directory structure and the SQLite data directory

Every deploy uploads into its own timestamped `releases/<id>/` directory rather than overwriting one fixed location in place — a `current` symlink is atomically repointed at the newest one after each successful deploy, so the web server never sees a broken or half-updated app, and prior releases stay on disk for a fast manual rollback (see the "Rollback" section below).

```bash
sudo mkdir -p <deploy-path>/releases <deploy-path>/shared/wwwroot/media
sudo chown -R <deploy-user>:<deploy-user> <deploy-path>
```

**Ownership matters here**: `deploy.yml` connects over SSH as `<deploy-user>` with no `sudo` for anything except the final service restart, so it needs to `mkdir`/`rsync`/`ln` directly under `<deploy-path>` — including `releases/` and `shared/`. Creating these as `root` (e.g. via a plain `sudo mkdir -p <deploy-path>` with no matching `chown -R`) is the single most common way to break the first deploy: rsync fails with `Permission denied` trying to write into a directory it doesn't own.

`shared/wwwroot/media` is where Umbraco's uploaded media library actually lives, persisted across every release — it's runtime state, never part of the published build output, so each fresh `releases/<id>/` wouldn't have it at all unless something points there. `deploy.yml`'s "Symlink shared media into the release" step creates `releases/<id>/wwwroot/media` as a symlink into it after every upload, so every release serves the same persistent library instead of starting empty.

The SQLite database, and the env file the systemd unit reads its connection string from, both live **outside** `<deploy-path>` entirely, under `<sqlite-data-dir>` — a directory whose real value is never typed into any committed file, only set as the `VPS_DB_PATH` and `VPS_ENV_FILE` secrets (step 2) and, once, directly into the systemd unit on the VPS itself (step 5):

```bash
sudo mkdir -p <sqlite-data-dir>
sudo chown <deploy-user>:<deploy-user> <sqlite-data-dir>
```

This directory serves two purposes: it's where `<deploy-user>` creates the actual `.db` file on first run (at `<sqlite-db-path>`), and it's where `deploy.yml`'s "Write production connection string" step writes the env file on every deploy (at `<env-file-path>`), resolving `VPS_DB_PATH` fresh each time (no `sudo` needed, since `<deploy-user>` owns this directory). The systemd unit's `EnvironmentFile` directive (step 5) loads that file as `ConnectionStrings__umbracoDbDSN` — ASP.NET Core's standard double-underscore env-var override of the `|DataDirectory|`-relative default in the base `appsettings.json` (that default is fine for local dev; it would otherwise nest the live db inside a release directory in production). Keeping both physically outside `<deploy-path>` means a deploy can never touch them, no matter how the releases/current structure inside `<deploy-path>` changes — and keeping their exact paths out of every committed file means the production filesystem layout is never visible in the (public) repo.

**Pre-seed an empty file at `<sqlite-db-path>` before the very first deploy ever runs on a new server:**

```bash
sudo -u <deploy-user> sqlite3 <sqlite-db-path> "VACUUM;"
```

(or the same thing via Python if `sqlite3` isn't installed: `sudo -u <deploy-user> python3 -c "import sqlite3; sqlite3.connect('<sqlite-db-path>').close()"`)

Umbraco's own "is the database available" check deliberately opens the file **read-only**, specifically so a routine health check never has the side effect of creating one. On a path with no file yet, that means the check reports "unavailable" and retries forever — it will never create the file itself, no matter how long you wait, and the site gets stuck on a "Boot failed" page indefinitely. This isn't a permissions problem or anything `deploy.yml` can fix; it only needs to happen once, ever, per server (an existing file from any prior deploy is enough — this step is skippable on every deploy after the very first).

## 5. systemd unit

`/etc/systemd/system/<service-name>.service`:

```ini
[Unit]
Description=NeonPixel Website (Umbraco)
After=network.target

[Service]
Type=simple
User=<deploy-user>
WorkingDirectory=<deploy-path>/current
ExecStart=/usr/bin/dotnet <deploy-path>/current/NeonPixel.Web.dll
Restart=on-failure
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=<service-name>
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
EnvironmentFile=-<env-file-path>

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable <service-name>.service
```

`WorkingDirectory`/`ExecStart` point at `<deploy-path>/current` — the symlink, not a specific release — so `deploy.yml`'s atomic symlink switch (step 4) is what actually changes which release is running on the next restart; this unit file itself never needs editing again after this one-time setup.

The leading `-` on `EnvironmentFile` makes it optional, so the unit doesn't fail to start if `app.env` doesn't exist yet — it's written by `deploy.yml` itself, before the first deploy has run.

Don't `systemctl start` it yet — `<deploy-path>/current` doesn't exist until the first deploy creates it.

## 6. nginx reverse proxy

`/etc/nginx/sites-available/neonpixel.eu`:

```nginx
server {
    listen 80;
    server_name neonpixel.eu;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header    Upgrade $http_upgrade;
        proxy_set_header    Connection keep-alive;
        proxy_set_header    Host $host;
        proxy_cache_bypass  $http_upgrade;
        proxy_set_header    X-Real-IP $remote_addr;
        proxy_set_header    X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header    X-Forwarded-Proto $scheme;
        client_max_body_size 50M;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/neonpixel.eu /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

The `X-Forwarded-Proto` header above is consumed by ASP.NET Core's Forwarded Headers Middleware, wired up in `Program.cs` — this is what lets Umbraco know the public-facing site is HTTPS even though Kestrel itself only ever speaks plain HTTP on `localhost:5000`. `appsettings.Production.json` (loaded automatically since the systemd unit above sets `ASPNETCORE_ENVIRONMENT=Production`) sets `Umbraco:CMS:Global:UseHttps: true`, `Umbraco:CMS:Runtime:Mode: Production`, and `Umbraco:CMS:WebRouting:UmbracoApplicationUrl: https://neonpixel.eu/` to match. Nothing further to configure on the app side for this — just the nginx/certbot steps below.

## 7. HTTPS via certbot

Requires `neonpixel.eu` DNS already pointed at the VPS (SPEC.md Open Question 3).

```bash
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d neonpixel.eu
```

Certbot edits the nginx site config in place to add the TLS `server` block and a redirect from port 80. Confirm auto-renewal is set up: `sudo certbot renew --dry-run`.

## 8. First deploy

With everything above in place, a push to `main` (see `SPEC.md`'s CI/CD section) runs `deploy.yml`, which:

1. Uploads the published app into a new `<deploy-path>/releases/<UTC-timestamp>/` directory
2. Symlinks `shared/wwwroot/media` into it
3. Atomically switches the `current` symlink to point at it
4. Restarts `<service-name>.service`
5. Prunes all but the 5 most recent releases
6. Runs a smoke test against `https://neonpixel.eu/`, failing the job if it doesn't return HTTP 200 or 302

After the first successful run:

```bash
sudo systemctl status <service-name>.service
curl -I http://localhost:5000/
ls <deploy-path>/releases   # should show exactly one release
readlink <deploy-path>/current   # should point at it
```

Then visit `https://neonpixel.eu/` and `https://neonpixel.eu/umbraco` to confirm the site and backoffice are both reachable, and complete the Umbraco install/admin-account step (see SPEC.md — this is an interactive, one-time step, not automated).

## Rollback

There's no automated rollback. To roll back manually, SSH into the VPS, repoint the symlink at an older release, and restart the service:

```bash
ssh -p <ssh-port> <deploy-user>@<vps-host>
cd <deploy-path>
ls releases                          # see available releases
ln -sfn releases/<older-id> current  # repoint at an older one
sudo systemctl restart <service-name>.service
```

## SonarCloud (CI, not VPS)

Already set up and working: the **SonarQube Cloud** GitHub App is installed on the `neonpixel-software` org (Automatic Analysis mode). It hooks into PR events directly and posts its own `SonarCloud Code Analysis` status check — no `SONAR_TOKEN`, no scanner step, no `ci.yml` job needed on this repo's side at all. Confirmed working live on PR #27.

`SonarCloud Code Analysis` is a required status check on `main` (added 2026-09-03, once PR #27 showed it passing).

If the GitHub App integration is ever removed and needs re-adding: install it from [sonarcloud.io](https://sonarcloud.io) → the org's GitHub App settings, or `github.com/organizations/neonpixel-software/settings/installations`.

## Prerequisites checklist

- [ ] Deploy user created, scoped `sudo` for the restart command only
- [ ] `VPS_DEPLOY_KEY`, `VPS_KNOWN_HOSTS`, `VPS_HOST`, `VPS_USER`, `VPS_DEPLOY_PATH`, `VPS_SERVICE_NAME`, `VPS_DB_PATH`, `VPS_ENV_FILE`, `VPS_SSH_PORT` secrets set on `neonpixel-website`
- [ ] `THEME_REPO_PAT` secret set (see SPEC.md Assumption 19 / Open Question 19 — separate from the VPS key)
- [x] SonarCloud GitHub App installed and `SonarCloud Code Analysis` required on `main` (see "SonarCloud" above)
- [ ] .NET 10 ASP.NET Core runtime installed
- [ ] `<deploy-path>/releases` and `<deploy-path>/shared/wwwroot/media` created, and `<deploy-path>` **recursively** owned by the deploy user (not just the top-level directory — see step 4's ownership note)
- [ ] `<sqlite-data-dir>` created and owned by the deploy user (SQLite db and the connection-string env file both live here, outside the deploy directory)
- [ ] Empty file pre-seeded at `<sqlite-db-path>` (first-ever deploy on this server only — see step 4; otherwise the site gets stuck reporting the database unavailable forever)
- [ ] systemd unit installed and enabled (not started — no app there yet), `WorkingDirectory`/`ExecStart` pointing at `<deploy-path>/current`
- [ ] nginx site config installed, `nginx -t` passes
- [ ] DNS for `neonpixel.eu` points at this VPS
- [ ] certbot HTTPS issued and auto-renewal confirmed
