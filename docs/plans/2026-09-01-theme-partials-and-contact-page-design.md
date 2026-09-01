# Design: Theme Partial Extraction + Contact Page

Date: 2026-09-01

## Context

Only 3 Umbraco properties exist so far (`heroTitle`, `seoTitle`, `seoDescription` on Home), and all template markup is monolithic: `theme/Views/Shared/_Layout.cshtml` (712 lines) inlines the loader/nav/header/footer, and `theme/Views/HomeContent.cshtml` (1320 lines) inlines all 11 `mxd-section` blocks. The user wants these split into reusable partials, plus a new Contact page.

`contact.html` wasn't part of what was originally extracted into `theme/HTML/`/`docs/HTML/` (only `index.html` and `404.html` were). It was found at `~/Desktop/NeonPixel site/HTML/contact.html` — the same pristine, unbranded purchased-template source as the already-converted `404.html` (confirmed via diff/mtime) — and copied into `docs/HTML/contact.html` (gitignored local reference copy, per existing convention).

## Scope decisions (confirmed with user)

1. **Loader/nav/header**: split into separate partial files (`_Loader.cshtml`, `_Nav.cshtml`, `_Header.cshtml`), included from `_Layout.cshtml` in the same order. Markup unchanged — pure file-organization refactor.
2. **`mxd-section` divs**: one partial per section, not a generic wrapper. `HomeContent.cshtml`'s 11 sections (named per the template's own HTML comments) become individual partials; `HomeContent.cshtml` shrinks to a sequence of `@await Html.PartialAsync(...)` calls.
3. **Contact page markup**: build from `docs/HTML/contact.html` (now available), not improvised — same conversion process already used for Home/404.
4. **Contact form**: stays static/cosmetic. The original template posts to a dead `mail.php` PHP stub (jQuery AJAX in `source-files/azurio-js-files/app.js`) that can't work here; the JS submit-handler wiring is dropped, form fields/markup stay. A real submit-to-email flow is an explicit future task, not part of this one.
5. **Contact document type**: same minimal pattern as Home — `pageTitle`, `seoTitle`, `seoDescription` only. Everything else (office addresses, socials, form) is hardcoded placeholder content, matching how Home already ships.
6. **Footer**: `contact.html`'s original template ships a different (simpler) footer than the one already unified in `_Layout.cshtml`. Decision: keep the single shared footer as-is for Contact too, rather than add a second footer variant — consistent with how 404 already just toggles the shared footer on/off via `ViewData["ShowFooter"]` instead of carrying its own footer content.

## File layout

```
theme/Views/Shared/
  _Layout.cshtml            (shrinks: head/doctype, @await's below, @RenderBody(), footer, scripts)
  _Loader.cshtml             (new)
  _Nav.cshtml                 (new)
  _Header.cshtml               (new)
  Sections/                     (new folder)
    _HeroSection.cshtml
    _StatisticsLinesSection.cshtml     (the "about" block, id="about")
    _NicheCardsSection.cshtml
    _OurCapabilitiesSection.cshtml
    _ParallaxDividerSection.cshtml      (shared — used twice in Home, once in Contact; parameterized by divider image number via @model or a simple @{ } param)
    _ProjectsGridSection.cshtml
    _ParallaxDividerImageTitleSection.cshtml
    _TechStackSection.cshtml
    _BlogPreviewSection.cshtml
    _CtaMatterSection.cshtml            (Home's Matter.js-physics CTA)
    _ContactHeadlineSection.cshtml      (Contact-only: h1 + intro + static form)
    _ConnectSection.cshtml              (Contact-only: "Connect" title + socials list)
    _OfficeLocationsSection.cshtml      (Contact-only)
    _CtaMarqueeSection.cshtml           (Contact-only: marquee-scroll CTA, visually distinct from Home's)
theme/Views/
  HomeContent.cshtml          (shrinks to a sequence of Sections/ partial calls)
  ContactContent.cshtml       (new: sequence of Sections/ partial calls)
  Error404Content.cshtml      (unchanged)
```

`theme/CONTENT-SETUP.md` gets a new "Contact" section, modeled on the existing Home/404 steps, corrected for two things that section of the doc still has wrong from before later fixes landed: `Layout = "~/Views/Shared/_Layout.cshtml"` (not the bare filename — see SPEC.md Open Question 19), and no more mention of a runtime `PhysicalFileProvider` for views (build-time compile-include now, per the same Open Question 19 fix).

## Public repo (`neonpixel-website`) changes

- `src/NeonPixel.Web/Views/Contact.cshtml` — new stub, identical shape to `Home.cshtml`/`Error404.cshtml`: `Layout`, `ViewData["Title"]`/`["MetaDescription"]` sourced from `pageTitle`/`seoTitle`/`seoDescription` with fallbacks, one `@await Html.PartialAsync("~/Views/ContactContent.cshtml")` line.
- No `Program.cs`/`.csproj`/`appsettings.json` changes needed — the existing `Content Include="..\..\theme\Views\**\*.cshtml"` glob already covers the new `Sections/` subfolder, and Contact is a normal content-templated page (no `Error404Collection`-style special config).

## Division of labor (unchanged from established pattern)

- **Claude**: all Razor/theme conversion work (private `neonpixel-theme` repo), the `Contact.cshtml` stub in the public repo, `CONTENT-SETUP.md` updates.
- **User** (backoffice): create the "Contact" document type + properties, hit "Create Template" (which overwrites the stub — fixed back per `CONTENT-SETUP.md`), create content nodes under `/en/` and `/nl/`, assign the template, publish.

## Out of scope (explicitly, not forgotten)

- Real contact-form submission handling (SurfaceController, SMTP/email service).
- Any properties beyond `pageTitle`/`seoTitle`/`seoDescription` on Contact.
- Reconciling `contact.html`'s different footer variant into `_Layout.cshtml`.
