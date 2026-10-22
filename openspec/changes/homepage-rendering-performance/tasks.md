## 1. Specify and measure

- [ ] 1.1 Record actual cold/warm browser cache behavior, direct-image cache behavior, in-app navigation, response headers, transferred bytes, and first hero image insertion/load times.
- [ ] 1.2 Validate the proposed Home prerender lifecycle against request counts, first-image discovery, document response timing, independent sponsor loading, and existing lifecycle race behavior.

## 2. Implement

- [ ] 2.1 Remove the Home tournament artwork blur and its unused wrapper while retaining existing motion and atmospheric layers.
- [ ] 2.2 If the measured lifecycle candidate meets the design criteria, persist the initial featured result across prerender/hydration without duplicate reads; otherwise revise the proposal/spec to omit this requirement and retain the current nonblocking data load.
- [ ] 2.3 Add or update focused browser/contract coverage for the selected behavior.

## 3. Verify and report

- [ ] 3.1 Run Release build, 564 contract tests, focused browser checks, and strict OpenSpec validation.
- [ ] 3.2 Capture final screenshots at 390, 768, 900, and 1440px plus reduced motion; document cache/timing results and limitations.
- [ ] 3.3 Archive this owned OpenSpec change after implementation and final validation.
