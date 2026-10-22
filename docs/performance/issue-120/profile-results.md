# Homepage image delivery and hero rendering: issue #120

## Findings

The browser cache was working. A fresh persistent Chromium profile with cache enabled loaded the same three stable WebP URLs on the homepage and in a direct image tab. The responses allow one-year freshness (`Cache-Control: public, max-age=31536000`) and include weak ETags. After the cold request, the direct-image navigation and homepage reloads were served from browser cache with zero encoded image bytes. No cache-busting, service worker, application cache, or backend change is needed.

The perceived wait came from when Home learned the image URLs. Before the fix, the deployed cold page reached DOMContentLoaded at 486 ms, but did not insert hero image elements until 1,416 ms; the warm deployed reload inserted them at 782 ms even though the image requests transferred zero bytes. Local cold evidence showed the same late insertion pattern: DCL 1,375 ms and first hero image at 2,650 ms. `Home` previously started its featured-data request from `OnAfterRenderAsync`, after interactive startup.

The fix removes the tournament artwork blur and its unused shared wrapper. It loads public featured data during prerender, persists the result through Blazor's prerender-to-interactive state handoff, and uses streaming rendering to send the loading shell while the request is pending. Sponsor loading starts independently alongside the featured request; completed public sponsor data is also reused during handoff, while a still-pending sponsor read may be retried by the interactive instance. Existing retry, loading/error, disposal and admin-order generation guards remain in the shared load path.

## Measured behavior

Chromium 148.0.7778.96, headed with real GPU enabled, 900×800 CSS pixels at DPR 2, live API data and artwork. The local app points at `https://dev-api.mercurius-aalst.be/`. Cache is enabled in a persistent browser profile. The timing script records navigation timings, FCP/LCP, hero image DOM insertion/load events, and CDP cache/byte observations.

| Run | Response start | DCL | FCP | LCP | First hero node | First hero image load |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Local before, cold | 1,151 ms | 1,375 ms | not captured | not captured | 2,650 ms | 2,764 ms |
| Local after, cold | 507 ms | 940 ms | 1,160 ms | 1,320 ms | 918 ms | 1,214 ms |
| Local before, warm reload | 6 ms | 31 ms | not captured | not captured | 167 ms | 168 ms |
| Local after, warm reload | 6 ms | 115 ms | 40 ms | 192 ms | 114 ms | 114 ms |
| Deployed baseline, cold | 282 ms | 486 ms | 780 ms | 1,760 ms | 1,416 ms | 1,681 ms |
| Deployed baseline, warm reload | 132 ms | 151 ms | 160 ms | 160 ms | 782 ms | 782 ms |

The local cold comparison is directional: the before and after runs used the same live API/assets, but the server/browser cold-start state was not paired. The warm local runs are the closer comparison: the image nodes appeared about 53 ms earlier after the change, and before DCL rather than after it. DCL is later in the candidate run because the featured-data request now participates in streamed prerender; streaming returned the shell earlier (6 ms response start in the warm case), FCP was 40 ms, and the image nodes were inserted at 114 ms. The cold local candidate showed image discovery about 1.7 seconds earlier, with a lower response-start and DCL, but that difference should not be treated as a controlled effect size.

On the candidate cold navigation, the three hero requests each transferred about 1.2 MB, 2.35 MB, and 264 KB; on warm navigation all three were status 200 cache hits with zero encoded bytes and the same URLs. The deployed direct-image navigation and the local and deployed homepage reloads after the cold load hit the same browser cache partition. App logs show one featured API request per navigation, with no duplicate request from prerender/hydration. Sponsor requests start independently. A slow sponsor response may be retried by the interactive instance if it did not finish before prerender state was serialized.

The previous profiling-only experiment that grouped the image blurs (13% lower mean Chrome `Graphics.Pipeline` draw-and-swap stage in one selected run) is historical and is not part of the fix. That stage is CPU-side GPU-process pipeline duration, not GPU hardware-busy time. The affected laptop was reported as Edge on an Intel i7-1255U with integrated graphics. This host has Chromium and an RTX 5070 Ti, so it does not validate frame pacing on that reported device.

## Responsive and reduced-motion checks

The final page was rendered with the live three-image set at 390×844 / DPR 2, 768×900 / DPR 2, 900×800 / DPR 2, and 1440×900 / DPR 1.25. Computed image filters are `none` at each width; image drift animations remain active normally and report `none` under reduced motion. The measured document/client widths match the earlier baseline at all four viewports: 386/380, 763/758, 895/890, and 1435/1430. This leaves the existing 5–6px horizontal overflow unchanged.

The [responsive summary](responsive/responsive-summary.json) and headed Chromium screenshots show the final no-blur page. Reduced-motion screenshot: [390×844](responsive/home-390-no-blur-reduced-motion.png).

| Viewport | Final no-blur screenshot |
| --- | --- |
| 390×844 / DPR 2 | ![Home at 390 pixels, no blur](responsive/home-390-no-blur.png) |
| 768×900 / DPR 2 | ![Home at 768 pixels, no blur](responsive/home-768-no-blur.png) |
| 900×800 / DPR 2 | ![Home at 900 pixels, no blur](responsive/home-900-no-blur.png) |
| 1440×900 / DPR 1.25 | ![Home at 1440 pixels, no blur](responsive/home-1440-no-blur.png) |

## Reproduction

These PowerShell commands use the captured Chromium and Node paths from the measurement host. Set `NODE_PATH` and `CHROME_EXECUTABLE_PATH` to the installed Playwright runtime/browser on another machine. The cache profiler uses a headed persistent context with cache enabled by default; use a new profile directory for a cold run and rerun with the same directory for warm/direct checks.

```powershell
Set-Location 'D:\Github Repositories\lan-party\.worktrees\homepage-rendering-performance'
$env:NODE_PATH = 'C:\Users\svenp\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'
$nodeExe = 'C:\Users\svenp\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
$env:CHROME_EXECUTABLE_PATH = 'C:\Users\svenp\AppData\Local\ms-playwright\chromium-1223\chrome-win64\chrome.exe'
```

For deployed behavior:

```powershell
$env:HOMEPAGE_URL = 'https://dev-lan.mercurius-aalst.be'
$env:HOMEPAGE_CACHE_PROFILE = Join-Path $env:TEMP 'issue120-cache-profile-deployed'
$env:HOMEPAGE_CACHE_OUTPUT = 'docs/performance/issue-120/cache-profile-deployed-baseline-timing.json'
$env:HOMEPAGE_WIDTH = '900'
$env:HOMEPAGE_HEIGHT = '800'
$env:HOMEPAGE_DPR = '2'
& $nodeExe scripts/profile-homepage-cache.cjs
```

For the local final build using the live API, start the app in one terminal. Local dummy Auth0 values only satisfy startup; the public Home route uses the configured live API.

```powershell
$env:MercuriusAPI__BaseAddress = 'https://dev-api.mercurius-aalst.be/'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5186'
$env:Auth0__ClientId = 'local-profile-client'
$env:Auth0__ClientSecret = 'local-profile-not-used'
$env:Auth0__Audience = 'https://dev-api.mercurius-aalst.be/'
dotnet build src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj -c Release
dotnet run --project src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj -c Release --no-build --no-launch-profile
```

Run the cold/warm/direct profile in a second terminal with the same Node/browser setup and a new persistent profile directory:

```powershell
$env:HOMEPAGE_URL = 'http://127.0.0.1:5186'
$env:HOMEPAGE_CACHE_PROFILE = Join-Path $env:TEMP 'issue120-cache-profile-local'
$env:HOMEPAGE_CACHE_OUTPUT = 'docs/performance/issue-120/cache-profile-local-final.json'
$env:HOMEPAGE_WIDTH = '900'
$env:HOMEPAGE_HEIGHT = '800'
$env:HOMEPAGE_DPR = '2'
& $nodeExe scripts/profile-homepage-cache.cjs
```

For final responsive screenshots and computed filter/overflow checks, keep the local app running and run:

```powershell
$env:HOMEPAGE_URL = 'http://127.0.0.1:5186'
$env:HOMEPAGE_RESPONSIVE_OUTPUT = 'docs/performance/issue-120/responsive'
& $nodeExe scripts/profile-homepage-responsive.cjs
```

Committed JSON files are the compact, auditable browser reports. The browser profile caches are created under `%TEMP%`; no raw traces or profile contents are committed.
