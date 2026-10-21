const fs = require('node:fs');
const assert = require('node:assert/strict');
const os = require('node:os');
const path = require('node:path');
const { chromium } = require('playwright');

const baseUrl = process.env.HOMEPAGE_URL || 'http://127.0.0.1:5188';
const output = path.resolve(process.env.HOMEPAGE_PROFILE_DIR || 'docs/performance/homepage-profile');
const widths = process.env.HOMEPAGE_WIDTHS ? process.env.HOMEPAGE_WIDTHS.split(',').map(Number) : [390, 768, 900, 1440, 1920];
const diagnosticWidths = (process.env.HOMEPAGE_DIAGNOSTIC_WIDTHS || process.env.HOMEPAGE_DIAGNOSTIC_WIDTH || '0').split(',').map(Number);
const duration = Number(process.env.HOMEPAGE_SAMPLE_MS || 8000);
const cpuThrottle = Number(process.env.HOMEPAGE_CPU_THROTTLE || 1);
const headless = process.env.HOMEPAGE_HEADED !== '1';
const reducedMotion = process.env.HOMEPAGE_REDUCED_MOTION === '1';
const browserPath = process.env.CHROME_EXECUTABLE_PATH || chromium.executablePath();
let results;
let activeCase;
const cases = [
  { name: 'baseline' },
  { name: 'frozen-hero', selector: '.home-hero-media-image', css: '.home-hero-media-image{animation:none!important}', property: 'animationName', before: value => value !== 'none', after: value => value === 'none' },
  { name: 'no-image-blur', selector: '.home-hero-media-image', css: '.home-hero-media-image{filter:none!important}', property: 'filter', before: value => value !== 'none', after: value => value === 'none' },
  { name: 'no-glow', selector: '.home-hero-media-glow', css: '.home-hero-media-glow{filter:none!important;animation:none!important}', property: 'filter', before: value => value !== 'none', after: value => value === 'none' },
  { name: 'no-fade', selector: '.home-hero-shell', pseudo: '::after', css: '.home-hero-shell::after{animation:none!important}', property: 'animationName', before: value => value !== 'none', after: value => value === 'none' },
  { name: 'no-backdrop', selector: '.home-hero-date-card', css: '.home-hero-date-card{backdrop-filter:none!important}', property: 'backdropFilter', before: value => value !== 'none', after: value => value === 'none' },
  { name: 'no-marquee', selector: '.sponsor-scroller__track--marquee', css: '.sponsor-scroller__track--marquee{animation:none!important}', property: 'animationName', before: value => value !== 'none', after: value => value === 'none' },
  { name: 'no-view-timeline', selector: '.home-sponsors-panel', css: '.home-sponsors-panel{animation:none!important;animation-timeline:none!important}', property: 'animationName', before: value => value !== 'none', after: value => value === 'none' }
];

async function sample(page, scroll = false) {
  const frameMetrics = page.evaluate(ms => new Promise(resolve => {
    const deltas = [];
    const longTasks = [];
    const observer = new PerformanceObserver(list => longTasks.push(...list.getEntries().map(e => e.duration)));
    try { observer.observe({ type: 'longtask', buffered: true }); } catch {}
    let previous;
    const start = performance.now();
    function frame(now) {
      if (previous) deltas.push(now - previous);
      previous = now;
      if (now - start < ms) requestAnimationFrame(frame);
      else {
        observer.disconnect();
        const sorted = [...deltas].sort((a, b) => a - b);
        const at = p => sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))] || 0;
        const frameMsP50 = +at(.5).toFixed(2);
        resolve({ frames: deltas.length, fps: +(1000 * deltas.length / deltas.reduce((a, b) => a + b, 0)).toFixed(1), frameMsP50, frameMsP95: +at(.95).toFixed(2), framesOverRefreshBudget: deltas.filter(x => x > frameMsP50 * 1.5).length, framesOver16_7ms: deltas.filter(x => x > 16.7).length, longTasks: longTasks.length, longTaskMs: +longTasks.reduce((a, b) => a + b, 0).toFixed(1) });
      }
    }
    requestAnimationFrame(frame);
  }), duration);
  let scrollJourney = null;
  if (scroll) {
    const started = Date.now();
    let direction = 1;
    let maxScrollY = 0;
    let reachedBottom = false;
    while (Date.now() - started < duration) {
      await page.mouse.wheel(0, direction * 500);
      const position = await page.evaluate(() => ({ y: window.scrollY, max: document.documentElement.scrollHeight - innerHeight }));
      maxScrollY = Math.max(maxScrollY, position.y);
      if (position.y >= position.max - 2) { reachedBottom = true; direction = -1; }
      if (position.y <= 2 && direction < 0) direction = 1;
      await page.waitForTimeout(100);
    }
    scrollJourney = { maxScrollY, reachedBottom, finalScrollY: await page.evaluate(() => window.scrollY) };
  }
  const metrics = await frameMetrics;
  assert.ok(metrics.frames > 0 && Number.isFinite(metrics.frameMsP95), 'RAF sampling must produce finite frame pacing metrics');
  if (scroll) assert.ok(scrollJourney.maxScrollY > 0, 'wheel scrolling must move the page');
  return { ...metrics, scrollJourney };
}

async function main() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless, executablePath: browserPath, args: ['--enable-gpu'] });
  results = { url: baseUrl, browser: await browser.version(), browserPath, headless, cpuThrottle, reducedMotion, platform: `${os.platform()} ${os.release()}`, cpu: os.cpus()[0]?.model, logicalCpus: os.cpus().length, widths: {} };
  for (const width of widths) {
    results.widths[width] = {};
    for (const testCase of (diagnosticWidths.includes(width) ? cases : cases.slice(0, 1))) {
      activeCase = { width, name: testCase.name };
      const { name, selector, pseudo, css, property, before, after } = testCase;
      const context = await browser.newContext({ viewport: { width, height: 900 }, deviceScaleFactor: 1, reducedMotion: reducedMotion ? 'reduce' : 'no-preference' });
      const page = await context.newPage();
      const cdp = await context.newCDPSession(page);
      let layers = [];
      cdp.on('LayerTree.layerTreeDidChange', event => { layers = event.layers || []; });
      await cdp.send('LayerTree.enable');
      const failures = [];
      page.on('pageerror', e => failures.push(e.message));
      await page.goto(baseUrl, { waitUntil: 'networkidle', timeout: 30000 });
      await page.locator('.home-hero-title').waitFor({ state: 'visible', timeout: 15000 });
      await page.waitForTimeout(1000);
      if (cpuThrottle > 1) await cdp.send('Emulation.setCPUThrottlingRate', { rate: cpuThrottle });
      if (!results.gpu) {
        results.gpu = await page.evaluate(() => {
        const gl = document.createElement('canvas').getContext('webgl');
          const ext = gl?.getExtension('WEBGL_debug_renderer_info');
          return { dpr: devicePixelRatio, renderer: ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : 'unavailable', vendor: ext ? gl.getParameter(ext.UNMASKED_VENDOR_WEBGL) : 'unavailable', hardwareConcurrency: navigator.hardwareConcurrency };
        });
      }
      if (css) await page.addStyleTag({ content: css });
      const tracePath = path.join(output, `${width}-baseline.json`);
      if (name === 'baseline') await cdp.send('Tracing.start', { categories: 'devtools.timeline,blink.user_timing', transferMode: 'ReturnAsStream' });
      const pageState = await page.evaluate(() => ({ heroImagesLoaded: [...document.querySelectorAll('.home-hero-media-image')].filter(image => image.complete && image.naturalWidth > 0).length, sponsorCount: document.querySelectorAll('.sponsor-scroller__track--marquee .sponsor-scroller__logo').length, marqueeDisplay: getComputedStyle(document.querySelector('.sponsor-scroller__track--marquee')).display }));
      let overrideCheck = null;
      if (selector) {
        const target = page.locator(selector).first();
        if (!await target.count()) throw new Error(`${name}: selector did not match ${selector}`);
        const original = await target.evaluate((element, options) => ({ value: getComputedStyle(element, options.pseudo)[options.property], reduce: matchMedia('(prefers-reduced-motion: reduce)').matches }), { property, pseudo });
        if (!before(original.value)) throw new Error(`${name}: target effect was not active before override (${property}=${original.value}, reducedMotion=${original.reduce})`);
        await page.addStyleTag({ content: css });
        const changed = await target.evaluate((element, options) => getComputedStyle(element, options.pseudo)[options.property], { property, pseudo });
        if (!after(changed)) throw new Error(`${name}: override did not disable target effect (${property}=${changed})`);
        overrideCheck = { selector, property, before: original.value, after: changed };
      }
      if (name === 'baseline') await page.screenshot({ path: path.join(output, `baseline-${width}.png`), fullPage: true });
      const idle = await sample(page);
      await page.evaluate(() => window.scrollTo(0, 0));
      const scroll = await sample(page, true);
      if (name === 'baseline') {
        const tracingComplete = new Promise(resolve => cdp.once('Tracing.tracingComplete', resolve));
        await cdp.send('Tracing.end');
        const event = await tracingComplete;
        const chunks = [];
        let eof = false;
        while (!eof) {
          const part = await cdp.send('IO.read', { handle: event.stream });
          chunks.push(part.data);
          eof = part.eof;
        }
        fs.writeFileSync(tracePath, chunks.join(''));
        await cdp.send('IO.close', { handle: event.stream });
      }
      results.widths[width][name] = { pageState, overrideCheck, idle, scroll, layerCount: layers.length, trace: name === 'baseline' ? path.basename(tracePath) : null, pageErrors: failures };
      await context.close();
      fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(results, null, 2));
      activeCase = null;
    }
  }
  await browser.close();
  fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results, null, 2));
}

main().catch(error => {
  if (results && activeCase) {
    results.diagnosticFailure = { ...activeCase, error: error.message };
    fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(results, null, 2));
  }
  console.error(error);
  process.exitCode = 1;
});
