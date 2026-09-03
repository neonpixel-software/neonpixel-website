# Deployment Runbook: NeonPixel Website

Manual, one-time VPS provisioning steps for `neonpixel-website`, plus the templates the automated deploy workflow (`.github/workflows/deploy.yml`) expects to already be in place. This is not automated — a human runs through it once per server. See `SPEC.md` for the architecture this supports.

Domain: `neonpixel.eu` (SPEC.md Open Question 3, resolved). DNS must point at the VPS before step 7 (certbot).

Placeholders used below (replace with real values, then set the matching GitHub Actions secrets):
- `<deploy-path>` — e.g. `/opt/neonpixel-web` (becomes the `VPS_DEPLOY_PATH` secret)
- `<service-name>` — e.g. `neonpixel-web` (becomes the `VPS_SERVICE_NAME` secret, systemd unit will be `<service-name>.service`)
- `<deploy-user>` — a dedicated, non-root user the deploy workflow SSHes in as
- `<sqlite-db-path>` — e.g. `/var/lib/neonpixel-website/neonpixel.sqlite.db` (becomes the `VPS_DB_PATH` secret; never appears in a committed file — see step 4)
- `<ssh-port>` — the VPS's SSH port (`22` if unchanged from default; becomes the `VPS_SSH_PORT` secret)

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

Set the remaining deploy secrets: `VPS_HOST`, `VPS_USER=<deploy-user>`, `VPS_DEPLOY_PATH=<deploy-path>`, `VPS_SERVICE_NAME=<service-name>`, `VPS_DB_PATH=<sqlite-db-path>`, `VPS_SSH_PORT=<ssh-port>`.

## 3. Install the .NET runtime

Umbraco 18 targets `net10.0` (confirmed via `Umbraco.Templates` 18.1.1 — see SPEC.md Assumption 3). Install the ASP.NET Core runtime (not the full SDK — the VPS only runs published output, it doesn't build):

```bash
wget https://packages.microsoft.com/config/ubuntu/26.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
```

Verify: `dotnet --list-runtimes` should show `Microsoft.AspNetCore.App 10.0.x`.

## 4. Create the deploy directory and the SQLite data directory

```bash
sudo mkdir -p <deploy-path>
sudo chown <deploy-user>:<deploy-user> <deploy-path>
```

This is what `deploy.yml`'s rsync step writes into — the published app, replaced on every deploy.

The SQLite database lives **outside** it, at a fixed path independent of `<deploy-path>` — and that exact path is never committed to the repo, only set as the `VPS_DB_PATH` secret (step 2), e.g. `/var/lib/neonpixel-website/neonpixel.sqlite.db`:

```bash
sudo mkdir -p /var/lib/neonpixel-website
sudo chown <deploy-user>:<deploy-user> /var/lib/neonpixel-website
```

`/var/lib/neonpixel-website` itself is fixed (it's infrastructure wiring baked into `deploy.yml` and the systemd unit below, not sensitive) and serves two purposes: it's where `<deploy-user>` creates the actual `.db` file on first run (assuming `VPS_DB_PATH` is set to somewhere under it, as above — it can point anywhere `<deploy-user>` can write, this is just the recommended default), and it's where `deploy.yml`'s "Write production connection string" step writes `app.env` on every deploy, resolving `VPS_DB_PATH` fresh each time (no `sudo` needed, since `<deploy-user>` owns this directory). The systemd unit's `EnvironmentFile` directive (step 5) loads that file as `ConnectionStrings__umbracoDbDSN` — ASP.NET Core's standard double-underscore env-var override of the `|DataDirectory|`-relative default in the base `appsettings.json` (that default is fine for local dev; it would otherwise nest the live db inside `<deploy-path>/umbraco/Data` in production). Keeping the db physically outside the deploy directory means a deploy can never touch it regardless of what the rsync excludes — SPEC.md's preferred option over relying on excludes alone — and keeping its exact path in a GitHub Actions secret rather than `appsettings.Production.json` means the production filesystem layout is never visible in the (public) repo.

`umbraco/Logs` and `wwwroot/media` are still excluded from the rsync (see `deploy.yml`) and persist inside `<deploy-path>` across deploys, since those aren't part of the build output either.

## 5. systemd unit

`/etc/systemd/system/<service-name>.service`:

```ini
[Unit]
Description=NeonPixel Website (Umbraco)
After=network.target

[Service]
Type=simple
User=<deploy-user>
WorkingDirectory=<deploy-path>
ExecStart=/usr/bin/dotnet <deploy-path>/NeonPixel.Web.dll
Restart=on-failure
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=<service-name>
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
EnvironmentFile=-/var/lib/neonpixel-website/app.env

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable <service-name>.service
```

The leading `-` on `EnvironmentFile` makes it optional, so the unit doesn't fail to start if `app.env` doesn't exist yet — it's written by `deploy.yml` itself (step 4), before the first deploy has run.

Don't `systemctl start` it yet — there's no published app in `<deploy-path>` until the first deploy runs.

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

With everything above in place, a push to `main` (see `SPEC.md`'s CI/CD section) runs `deploy.yml`, which publishes and rsyncs the app into `<deploy-path>` and restarts `<service-name>.service`. After the first successful run:

```bash
sudo systemctl status <service-name>.service
curl -I http://localhost:5000/
```

Then visit `https://neonpixel.eu/` and `https://neonpixel.eu/umbraco` to confirm the site and backoffice are both reachable, and complete the Umbraco install/admin-account step (see SPEC.md — this is an interactive, one-time step, not automated).

## Prerequisites checklist

- [ ] Deploy user created, scoped `sudo` for the restart command only
- [ ] `VPS_DEPLOY_KEY`, `VPS_HOST`, `VPS_USER`, `VPS_DEPLOY_PATH`, `VPS_SERVICE_NAME`, `VPS_DB_PATH`, `VPS_SSH_PORT` secrets set on `neonpixel-website`
- [ ] `THEME_REPO_PAT` secret set (see SPEC.md Assumption 19 / Open Question 19 — separate from the VPS key)
- [ ] .NET 10 ASP.NET Core runtime installed
- [ ] Deploy directory created and owned by the deploy user
- [ ] `/var/lib/neonpixel-website` created and owned by the deploy user (SQLite db lives here, outside the deploy directory)
- [ ] systemd unit installed and enabled (not started — no app there yet)
- [ ] nginx site config installed, `nginx -t` passes
- [ ] DNS for `neonpixel.eu` points at this VPS
- [ ] certbot HTTPS issued and auto-renewal confirmed
