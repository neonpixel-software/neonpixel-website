using Microsoft.Extensions.FileProviders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The purchased front-end template's license forbids redistribution, so its converted
// Razor views and static assets live in a private companion repo (neonpixel-theme),
// pulled in here as a git submodule at theme/ (a sibling of src/NeonPixel.Web) rather
// than copied into this repo's own Views/wwwroot. See SPEC.md Assumption 17 and Open
// Question 19. theme/Views is compiled at build time via an MSBuild Content/LinkBase
// include in NeonPixel.Web.csproj (not a runtime file provider — Razor runtime
// compilation is obsolete in .NET 10 and only worked under ASPNETCORE_ENVIRONMENT=
// Development, which broke production). theme/wwwroot still needs a runtime static-file
// wiring since static assets aren't part of Razor compilation. Guarded with
// Directory.Exists so a clone without submodule access still builds and runs — just
// with no front-end presentation, which is expected.
string themeWwwroot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "theme", "wwwroot"));

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();

if (Directory.Exists(themeWwwroot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(themeWwwroot),
    });
}

// SPEC.md Assumption 20: English is served under /en/ from day one (not bare "/"), so
// adding a second language later doesn't need a breaking URL change. Umbraco's own
// Culture and Hostnames domain binding may or may not redirect bare "/" on its own
// (undocumented for a path-only, no-real-hostname setup) -- this is registered before
// app.UseUmbraco() so it always wins regardless, rather than depending on that behavior.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/en/", permanent: false);
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
