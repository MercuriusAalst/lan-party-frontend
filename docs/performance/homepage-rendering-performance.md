# FE issue 120: homepage rendering investigation

## Result

Issue 120 is resolved with a measured rendering optimization: the three animated hero images now share one `blur(12px)` wrapper instead of each owning a blur filter. In interleaved 900×800, DPR 2 Chromium runs, the Chrome GPU-process `STEP_DRAW_AND_SWAP` mean decreased 13% on the deployed homepage and 9–15% on the local app with live API assets. This is pipeline-stage duration, not hardware GPU-busy time; the reported Edge/iGPU laptop was not available for direct validation. [Current measurements, reproduction commands, per-trial data and screenshots](issue-120/profile-results.md).

The 1920px RAF investigation below is historical. Its smooth frame intervals did not expose the reported issue and did not select this fix; the later matched pipeline-stage measurements did.

The first diagnostic run incorrectly reported `animationName=none`: the profiler injected each candidate override before reading the original computed style. That ordering is fixed. The corrected 1920px run loaded all three hero images and 20 sponsor logos, and all seven controls passed their before/after style assertions. The old unexplained mismatch was a harness defect, not evidence of a homepage issue.

## Historical 1920px RAF comparison

Each case used headed Chromium 148.0.7778.96 on Windows 11 build 26200, DPR 1, a 4x DevTools CPU throttle, and the same deployment. The table shows p95 RAF callback intervals (milliseconds) during three-second idle and actual wheel-scroll samples. It is descriptive, not proof of physical display pacing.

| Case | Verified style change | Idle p95 | Wheel p95 | Intervals over 16.7ms (idle / wheel) |
| --- | --- | ---: | ---: | ---: |
| Baseline | — | 6.7 | 6.6 | 0 / 0 |
| Freeze hero motion | `home-hero-shift` → `none` | 6.7 | 6.8 | 1 / 0 |
| Remove hero-image blur | `blur(12px)` → `none` | 6.7 | 6.8 | 0 / 0 |
| Remove glow blur and animation | `blur(22px)` → `none` | 6.7 | 6.8 | 0 / 0 |
| Stop hero fade animation | `home-hero-fade-breathe` → `none` | 6.7 | 6.7 | 0 / 1 |
| Remove date-card backdrop blur | `blur(10px)` → `none` | 6.8 | 6.8 | 0 / 0 |
| Stop sponsor marquee | `sponsor-scroller-marquee` → `none` | 3.4 | 6.7 | 0 / 0 |
| Stop sponsor view-timeline animation | `home-flow-rise` → `none` | 3.4 | 6.7 | 0 / 0 |

The lower idle p95 for marquee and view-timeline controls did not carry through to wheel scrolling, and each was a single three-second run. This is not enough to establish a benefit. RAF cadence varied between about 182 and 299 callbacks per second; the display refresh rate was not verified. The baseline alone was recorded with a Chrome trace, so the table is not a fine-grained paint/raster comparison. GPU throttling was unavailable; the host used an NVIDIA RTX 5070 Ti (Direct3D11) and an Intel Core i5-13600K. Long-task counts are omitted: that run used a buffered observer that included initial-load entries. The runner now observes only tasks that begin during each sample.

The earlier live-site capture also produced baseline traces and screenshots at 390, 768, 900, and 1440px. Its metrics summary was lost when the then-broken control aborted; no measurements from those widths are claimed. Raw live captures and the corrected 1920px summary are retained locally in `docs/performance/homepage-issue-site/` and `docs/performance/homepage-issue-site-1920-controls/`, not committed.

## Historical 1920px RAF reproduction

Ensure Node can resolve the installed `playwright` package and set `CHROME_EXECUTABLE_PATH` to an installed Chromium executable. For representative artwork, use the deployment URL; the local Development mock has missing hero and sponsor assets.

```powershell
$env:NODE_PATH = '<node_modules containing playwright>'
$env:CHROME_EXECUTABLE_PATH = '<installed Chromium executable>'
$env:HOMEPAGE_URL = 'https://dev-lan.mercurius-aalst.be'
$env:HOMEPAGE_PROFILE_DIR = 'docs/performance/homepage-profile'
$env:HOMEPAGE_HEADED = '1'
$env:HOMEPAGE_CPU_THROTTLE = '4'
$env:HOMEPAGE_SAMPLE_MS = '3000'
$env:HOMEPAGE_WIDTHS = '1920'
$env:HOMEPAGE_DIAGNOSTIC_WIDTHS = '1920'
node scripts/profile-homepage.cjs
```

For baseline-only captures, set `HOMEPAGE_DIAGNOSTIC_WIDTHS='0'`. The script checkpoints its JSON summary after every completed case and records an optional diagnostic failure without discarding prior results. It closes Chromium on success or failure. These commands reproduce the historical RAF investigation, not the selected fix measurements. The implementation consolidates existing blur rendering without changing the visual or motion contract, so no OpenSpec delta was needed.
