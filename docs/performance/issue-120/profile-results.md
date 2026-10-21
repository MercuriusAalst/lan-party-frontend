# Homepage rendering profile: issue #120

The hero’s three tournament images each had their own `blur(12px)` filter while their transforms continued animating. The fix keeps their image order, individual sizes, opacity, transforms, rotation and animation, but moves the same blur to one shared wrapper. The glow, fade, card backdrop blur and all other motion stay unchanged.

## Matched 900px run

Chromium 148.0.7778.96, visible browser, NVIDIA RTX 5070 Ti, 900×800 CSS pixels at DPR 2, dark theme, foreground tab, idle hero, no throttling. The profiler loaded the page and decoded its assets before sampling. It reset document-timeline CSS animations to 3000 ms before each 1.4 second trace and interleaved baseline and treatment in the same page session.

The reported metric is the mean duration of Chrome GPU-process `Graphics.Pipeline` events whose step is `STEP_DRAW_AND_SWAP`, divided by that step’s event count (equal to swap count in these samples). This is CPU-side GPU-process pipeline duration, not hardware GPU-busy time. It measures the same named non-overlapping pipeline stage in every sample.

The compact [per-trial dataset](per-trial-summary.json) contains every measured trial, event count, presented-frame p95, missed-frame count, environment, and source summary path for the repeated 900px comparisons. The raw CDP traces remain local and are not required to audit the values summarized here.

| Page | Original separate image blurs | Shared blur surface | Change |
| --- | ---: | ---: | ---: |
| Deployed homepage | 0.47, 0.47, 0.48 ms/swap | 0.41, 0.42, 0.41 ms/swap | 13% lower median |
| Local patched app, run 1 | 0.58, 0.58, 0.54, 0.53 ms/swap | 0.47, 0.47, 0.48, 0.48 ms/swap | 15% lower median |
| Local patched app, run 2 | 0.56, 0.54, 0.56 ms/swap | 0.51, 0.47, 0.71 ms/swap | 9% lower median; one high treatment sample |

Presented-frame p95 stayed around 5.0–5.2 ms with zero intervals over 16.7 ms in the 900px runs. Those smooth frame intervals mean this host did not reproduce a visible missed-frame problem; the measured change is the consistent reduction in the selected pipeline stage. A separate all-hero-blurs-off diagnostic reduced that stage further but removed several visible effects, so it was not selected.

The local app used `MercuriusAPI:BaseAddress=https://dev-api.mercurius-aalst.be/` and the public live API, rather than mock records. Both pages decoded all 36 images and 25 sponsor logos; the three hero images matched by source and intrinsic size:

| Hero image | Intrinsic dimensions | Response size |
| --- | ---: | ---: |
| Primary | 2254×1268 | 1,219,038 bytes |
| Secondary | 1920×1080 | 2,345,860 bytes |
| Tertiary | 1280×720 | 263,562 bytes |

On the local browser navigation, `responseEnd` was 18 ms, DOM content loaded at 160 ms, and the load event ended at 252 ms. The rendering traces began only after the hero and sponsor images were decoded, so they represent the warm animated state. Cross-origin browser resource timing does not expose the image transfer-byte fields; the response sizes above come from the image response headers.

## Responsive and reduced-motion checks

At each width, baseline and treatment had identical document and client widths, decoded asset counts, and scroll heights. The document exceeded the layout client width by 5–6 CSS pixels in both states, so the change introduces no additional horizontal overflow but does not remove the existing small overflow.

| Viewport / DPR | Original mean per swap | Shared mean per swap | Frame p95, original → shared | Document/client width |
| --- | ---: | ---: | ---: | ---: |
| 390×844 / 2 | 0.58 ms | 0.44 ms | 5.08 → 5.02 ms | 386/380 in both |
| 768×900 / 2 | 0.60 ms | 0.51 ms | 5.07 → 5.08 ms | 763/758 in both |
| 900×800 / 2 | 0.56 ms | 0.51 ms | 5.10 → 5.13 ms | 895/890 in both |
| 1440×900 / 1.25 | 0.79 ms | 0.54 ms | 5.18 → 5.17 ms | 1435/1430 in both |

The 390px reduced-motion capture confirms the existing hero image animations remain stopped under `prefers-reduced-motion: reduce`; the shared blur does not add animation. The wider responsive figures are one short A/B pair each and are directional checks, not precise performance estimates.

The affected device was reported as Edge on an Intel i7-1255U with integrated graphics. This host has an RTX 5070 Ti and Chromium, and Edge is not installed. The change is measured on real GPU-enabled Chromium here, but the reported laptop and browser still need a direct confirmation. No CPU/GPU throttling was applied.

## Reproduction commands

These PowerShell commands use the captured Chromium and Node locations from the measurement host. Replace `CHROME_EXECUTABLE_PATH` with the installed Chromium executable and `NODE_PATH`/`$nodeExe` with the installed Node runtime and its Playwright package path on another machine. The runner opens headed Chromium with GPU enabled, decodes the homepage assets before measurement, sets dark theme, resets document-timeline CSS animations to 3000 ms, then alternates baseline and treatment in the same page. It writes a `summary.json`, screenshots, and compressed raw traces under each profile directory.

Run the deployed comparison from the repository worktree:

```powershell
Set-Location 'D:\Github Repositories\lan-party\.worktrees\homepage-rendering-performance'
$env:NODE_PATH = 'C:\Users\svenp\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'
$nodeExe = 'C:\Users\svenp\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
$env:CHROME_EXECUTABLE_PATH = 'C:\Users\svenp\AppData\Local\ms-playwright\chromium-1223\chrome-win64\chrome.exe'
$env:HOMEPAGE_URL = 'https://dev-lan.mercurius-aalst.be'
$env:HOMEPAGE_PROFILE_DIR = 'docs/performance/issue-120-repro-deployed'
$env:HOMEPAGE_WIDTH = '900'
$env:HOMEPAGE_HEIGHT = '800'
$env:HOMEPAGE_DPR = '2'
$env:HOMEPAGE_REPEATS = '3'
$env:HOMEPAGE_SAMPLE_MS = '1400'
$env:HOMEPAGE_ACTION = 'idle'
$env:HOMEPAGE_CONTROL = 'single-image-blur-surface'
& $nodeExe scripts/profile-homepage-live.cjs
```

For the local run, start the patched app in a separate PowerShell window, configured to use the same live API records and image assets rather than the local mock data:

```powershell
Set-Location 'D:\Github Repositories\lan-party\.worktrees\homepage-rendering-performance'
$env:MercuriusAPI__BaseAddress = 'https://dev-api.mercurius-aalst.be/'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5186'
dotnet build src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj -c Release
dotnet run --project src/Mercurius.LAN.Web/Mercurius.LAN.Web.csproj -c Release --no-build --no-launch-profile
```

In another PowerShell window, use the same browser and sampling settings with the local page and a separate output directory:

```powershell
Set-Location 'D:\Github Repositories\lan-party\.worktrees\homepage-rendering-performance'
$env:NODE_PATH = 'C:\Users\svenp\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'
$nodeExe = 'C:\Users\svenp\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
$env:CHROME_EXECUTABLE_PATH = 'C:\Users\svenp\AppData\Local\ms-playwright\chromium-1223\chrome-win64\chrome.exe'
$env:HOMEPAGE_URL = 'http://127.0.0.1:5186'
$env:HOMEPAGE_PROFILE_DIR = 'docs/performance/issue-120-repro-local'
$env:HOMEPAGE_WIDTH = '900'
$env:HOMEPAGE_HEIGHT = '800'
$env:HOMEPAGE_DPR = '2'
$env:HOMEPAGE_REPEATS = '3'
$env:HOMEPAGE_SAMPLE_MS = '1400'
$env:HOMEPAGE_ACTION = 'idle'
$env:HOMEPAGE_CONTROL = 'single-image-blur-surface'
& $nodeExe scripts/profile-homepage-live.cjs
```

The local run requires the app to be reachable at port 5186 before starting the profiler. `MercuriusAPI__BaseAddress` is .NET environment-variable syntax for `MercuriusAPI:BaseAddress`.

## Screenshots

Baseline and shared-surface screenshots use the same viewport, scroll position and synchronized animation phase. The 900px pair is from the local patched app with the live API assets; the other desktop/mobile pairs use the deployed page with the controlled shared-surface diagnostic.

| Viewport | Original | Shared blur surface |
| --- | --- | --- |
| 390×844 / DPR 2 | ![390 before](home-390-before.png) | ![390 after](home-390-after.png) |
| 768×900 / DPR 2 | ![768 before](home-768-before.png) | ![768 after](home-768-after.png) |
| 900×800 / DPR 2 | ![900 before](home-900-before.png) | ![900 after](home-900-after.png) |
| 1440×900 / DPR 1.25 | ![1440 before](home-1440-before.png) | ![1440 after](home-1440-after.png) |

Reduced motion: [390×844 local patched page](home-390-reduced-motion.png).

Raw trace captures remain in the local `docs/performance/issue-120-*` profile directories and are not committed. The compact summaries and screenshots here are the review evidence.
