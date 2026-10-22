## 1. Specify and measure

- [x] 1.1 Record actual cold/warm browser cache behavior, direct-image cache behavior, response headers, transferred bytes, and first hero image insertion/load times.
- [x] 1.2 Validate the Home prerender lifecycle against request counts, first-image discovery, document response timing, independent sponsor loading, and existing lifecycle race behavior.

## 2. Implement

- [x] 2.1 Remove the Home tournament artwork blur and its unused wrapper while retaining existing motion and atmospheric layers.
- [x] 2.2 Persist the initial featured result across prerender/hydration without duplicate featured reads; stream the shell while the request is pending. Completed sponsor state is reused; a still-pending sponsor read may be repeated by the interactive component.
- [x] 2.3 Add focused contract coverage for state handoff and independent sponsor completion while featured loading remains pending.

## 3. Verify and report

- [x] 3.1 Run Release build, focused contract tests, browser checks, and strict OpenSpec validation. Full repository verification is run by the architect.
- [x] 3.2 Capture final screenshots at 390, 768, 900, and 1440px plus reduced motion; document cache/timing results and limitations.
- [x] 3.3 Archive this owned OpenSpec change after the final applicable validation.

