# FE issue 120: homepage rendering investigation

## Result

Issue 120 remains unresolved. No rendering bottleneck was isolated and no product code was changed. Local Development mock data had missing hero and sponsor artwork, so those measurements were discarded as nonrepresentative. A headed baseline was then captured against the deployment shown in the reproduction video (`https://dev-lan.mercurius-aalst.be`) at 390, 768, 900, 1440, and 1920 CSS pixels. Its hero artwork and sponsor logos loaded. Raw Chromium traces and full-page screenshots are retained locally under `docs/performance/homepage-issue-site/`; they are generated evidence and are not committed.

The live-site run used Playwright's installed Chromium 148 on Windows 11 build 26200, headed, DPR 1, with a 4x DevTools CPU throttle and an NVIDIA RTX 5070 Ti (Direct3D11). The machine's display refresh rate was not verified, and GPU throttling was unavailable. The harness collected a three-second idle window and a three-second actual wheel-scroll window at each width, producing five raw traces (`390-baseline.json`, `768-baseline.json`, `900-baseline.json`, `1440-baseline.json`, `1920-baseline.json`) and five full-page screenshots in `docs/performance/homepage-issue-site/`. These traces are browser-level evidence, not proof of physical display presentation or lower-powered GPU behavior. The failed control stopped the old run before its in-memory metrics summary was written. Therefore no live-site p95, dropped-frame, or before/after result is claimed. The runner now checkpoints the summary after each completed case and records a failed optional diagnostic, preserving completed baseline results.

## Diagnostic failure

The `frozen-hero` candidate expected `.home-hero-media-image` to have a running animation. In the profile context, computed `animationName` was `none` while `prefers-reduced-motion` was false. A separate fresh headed page at the same URL, 1920px viewport, and 4x CPU throttle showed the hashed `home-hero-shift` animation and loaded the 2254px hero image; setting `animation:none` then changed the computed name to `none`. The discrepancy between the capture and fresh probe is unresolved, so candidate comparisons are invalid. Other candidate effects were not tested and must be treated as unmeasured.

The script defaults to baseline-only captures. Candidate controls are opt-in using `HOMEPAGE_DIAGNOSTIC_WIDTHS`; each checks its target style before and after injection and exits nonzero if the target effect is not active or the override does not take effect. The exception above is intentionally not counted as an improvement.

## Reproduction

Run the local app with mock data only for harness checks; its image assets are not representative. For issue-site captures, set `HOMEPAGE_URL` to the public deployment. Ensure Node can resolve the installed `playwright` package and set `CHROME_EXECUTABLE_PATH` to the installed Chromium executable.

```powershell
$env:NODE_PATH = '<node_modules containing playwright>'
$env:CHROME_EXECUTABLE_PATH = '<installed Chromium executable>'
$env:HOMEPAGE_URL = 'https://dev-lan.mercurius-aalst.be'
$env:HOMEPAGE_PROFILE_DIR = 'docs/performance/homepage-profile'
$env:HOMEPAGE_HEADED = '1'
$env:HOMEPAGE_CPU_THROTTLE = '4'
$env:HOMEPAGE_SAMPLE_MS = '3000'
$env:HOMEPAGE_WIDTHS = '390,768,900,1440,1920'
$env:HOMEPAGE_DIAGNOSTIC_WIDTHS = '0'
node scripts/profile-homepage.cjs
```

To attempt one-at-a-time controls, set `HOMEPAGE_DIAGNOSTIC_WIDTHS='1920'`. Candidate output is exploratory only unless the style assertions pass and the resulting trace and screenshots support a controlled comparison. No OpenSpec delta was needed because there is no product behavior change.
