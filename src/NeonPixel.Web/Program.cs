using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The purchased front-end template's license forbids redistribution, so its converted
// Razor views and static assets live in a private companion repo (neonpixel-theme),
// pulled in here as a git submodule at theme/ (a sibling of src/NeonPixel.Web) rather
// than copied into this repo's own Views/wwwroot. See SPEC.md Assumption 17 and Open
// Question 19. theme/Views and theme/wwwroot are both pulled into this project's own
// build/publish output at build time via MSBuild Content/LinkBase includes (see
// NeonPixel.Web.csproj) -- that alone is correct and sufficient for `dotnet publish`
// (the only thing production ever runs), but NOT for `dotnet run`/`dotnet build`: those
// use the source project folder as ContentRootPath, not bin/ or a publish folder, so
// MSBuild's copy-to-output never lands anywhere WebRootPath actually looks during local
// dev (a real bug, only caught after the first live production deploy -- theme assets
// 404'd through to Umbraco's own routing and got blocked as a MIME-type mismatch).
// themeWwwroot below restores direct sibling-directory serving as a local-dev-only
// fallback, guarded by Directory.Exists so a clone without submodule access, and a real
// publish output (which has no such sibling directory), both still build and run fine.
string themeWwwroot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "theme", "wwwroot"));

// Production (DEPLOYMENT.md) runs Kestrel on loopback-only HTTP behind an nginx reverse
// proxy that terminates TLS (see DEPLOYMENT.md's nginx config, which already sends
// X-Forwarded-Proto). Without this, Kestrel/Umbraco only ever sees plain-HTTP requests,
// so Request.IsHttps is always false even though the public-facing site is HTTPS -- that
// breaks Umbraco:CMS:Global:UseHttps (appsettings.Production.json) and would generate
// http:// links. Left at ASP.NET Core's default KnownNetworks/KnownProxies (loopback
// only) rather than cleared, since nginx and Kestrel run on the same VPS and Kestrel is
// never exposed directly -- clearing them would accept forwarded headers from any client.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();

// Must run before anything that reads Request.Scheme/IsHttps or the client IP -- the
// static-file/redirect/Umbraco middleware below, in that order.
app.UseForwardedHeaders();

app.UseStaticFiles();

if (Directory.Exists(themeWwwroot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(themeWwwroot),
    });
}

// SPEC.md Assumption 20: bare "/" redirects to a fixed default culture rather than
// depending on Umbraco's own Culture and Hostnames domain-binding behavior for a
// path-only, no-real-hostname setup (undocumented). Dutch is the default per user
// request (2026-09-03) -- was English at launch. Registered before app.UseUmbraco()
// so it always wins regardless.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/nl/", permanent: false);
        return;
    }

    await next();
});

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
