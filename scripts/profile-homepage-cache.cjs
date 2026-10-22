const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const homepageUrl = process.env.HOMEPAGE_URL || 'https://dev-lan.mercurius-aalst.be';
const outputPath = path.resolve(process.env.HOMEPAGE_CACHE_OUTPUT || 'docs/performance/issue-120/cache-profile.json');
const profilePath = path.resolve(process.env.HOMEPAGE_CACHE_PROFILE || path.join(process.env.TEMP || '.', 'issue-120-cache-profile'));
const browserPath = process.env.CHROME_EXECUTABLE_PATH || chromium.executablePath();
const width = Number(process.env.HOMEPAGE_WIDTH || 900);
const height = Number(process.env.HOMEPAGE_HEIGHT || 800);
const dpr = Number(process.env.HOMEPAGE_DPR || 2);
const cacheDisabled = process.env.HOMEPAGE_CACHE_DISABLED === '1';

function cacheHeaders(headers = {}) {
  const wanted = ['cache-control', 'etag', 'last-modified', 'expires', 'age', 'vary', 'date'];
  return Object.fromEntries(Object.entries(headers)
    .filter(([name]) => wanted.includes(name.toLowerCase()))
    .map(([name, value]) => [name.toLowerCase(), value]));
}

async function main() {
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  const context = await chromium.launchPersistentContext(profilePath, {
    headless: false,
    executablePath: browserPath,
    args: ['--enable-gpu', `--window-size=${width},${height + 140}`],
    viewport: { width, height },
    deviceScaleFactor: dpr
  });

  try {
    const page = await context.newPage();
    await page.addInitScript(() => {
      window.__heroImageTiming = { firstInsertedAtMs: null, insertions: [], loads: [] };
      const remember = image => {
        if (!(image instanceof HTMLImageElement) || !image.matches('.home-hero-media-image')) return;
        const url = image.currentSrc || image.src;
        const insertedAtMs = performance.now();
        window.__heroImageTiming.insertions.push({ url, insertedAtMs });
        if (window.__heroImageTiming.firstInsertedAtMs === null) window.__heroImageTiming.firstInsertedAtMs = insertedAtMs;
        image.addEventListener('load', () => window.__heroImageTiming.loads.push({ url, loadedAtMs: performance.now(), naturalWidth: image.naturalWidth }), { once: true });
      };
      new MutationObserver(records => records.flatMap(record => [...record.addedNodes]).forEach(node => {
        if (!(node instanceof Element)) return;
        remember(node);
        node.querySelectorAll('img.home-hero-media-image').forEach(remember);
      })).observe(document, { childList: true, subtree: true });
    });
    const cdp = await context.newCDPSession(page);
    await cdp.send('Network.enable');
    await cdp.send('Network.setCacheDisabled', { cacheDisabled });

    const requests = [];
    const byId = new Map();
    let phase = 'setup';
    let phaseStartedAtMs = Date.now();
    cdp.on('Network.requestWillBeSent', event => {
      if (event.type !== 'Image') return;
      const headers = event.request.headers || {};
      const request = {
        phase,
        requestId: event.requestId,
        url: event.request.url,
        method: event.request.method,
        requestStartAfterPhaseMs: Math.round(event.wallTime * 1000 - phaseStartedAtMs),
        requestCacheHeaders: cacheHeaders(headers)
      };
      requests.push(request);
      byId.set(event.requestId, request);
    });
    cdp.on('Network.requestServedFromCache', event => {
      const request = byId.get(event.requestId);
      if (request) request.requestServedFromCache = true;
    });
    cdp.on('Network.responseReceived', event => {
      const request = byId.get(event.requestId);
      if (!request) return;
      request.status = event.response.status;
      request.fromDiskCache = event.response.fromDiskCache || false;
      request.fromServiceWorker = event.response.fromServiceWorker || false;
      request.fromPrefetchCache = event.response.fromPrefetchCache || false;
      request.responseCacheHeaders = cacheHeaders(event.response.headers);
      request.mimeType = event.response.mimeType;
    });
    cdp.on('Network.responseReceivedExtraInfo', event => {
      const request = byId.get(event.requestId);
      if (!request) return;
      request.status = event.statusCode || request.status;
      request.responseCacheHeaders = cacheHeaders(event.headers);
    });
    cdp.on('Network.loadingFinished', event => {
      const request = byId.get(event.requestId);
      if (request) request.encodedDataLength = event.encodedDataLength;
    });
    cdp.on('Network.loadingFailed', event => {
      const request = byId.get(event.requestId);
      if (request) request.error = event.errorText;
    });

    const phases = [];
    const saveReport = () => {
      const report = {
        homepageUrl,
        browserPath,
        viewport: { width, height, dpr },
        cache: { persistentBrowserContext: true, cacheDisabled, profilePath },
        heroImageComparedDirectly: phases[0]?.homeState?.images?.[0]?.url || null,
        phases,
        imageRequests: requests
      };
      fs.writeFileSync(outputPath, JSON.stringify(report, null, 2));
    };
    const waitForHomeImages = () => page.waitForFunction(() => {
      const images = [...document.querySelectorAll('.home-hero-media-image')];
      return images.length > 0 && images.every(image => image.complete && image.naturalWidth > 0);
    }, undefined, { timeout: 45000 });
    const runPhase = async (name, action, isHome = false) => {
      phase = name;
      const start = Date.now();
      phaseStartedAtMs = start;
      await action();
      if (isHome) await waitForHomeImages();
      await page.waitForTimeout(900);
      const homeState = isHome ? await page.evaluate(() => ({
        url: location.href,
        performanceTiming: (() => { const navigation = performance.getEntriesByType('navigation')[0]; return navigation ? { responseStartMs: +navigation.responseStart.toFixed(1), domInteractiveMs: +navigation.domInteractive.toFixed(1), domContentLoadedMs: +navigation.domContentLoadedEventEnd.toFixed(1), loadEventEndMs: +navigation.loadEventEnd.toFixed(1) } : null; })(),
        heroImageTiming: window.__heroImageTiming,
        images: [...document.querySelectorAll('.home-hero-media-image')].map(image => ({ url: image.currentSrc || image.src, naturalWidth: image.naturalWidth, naturalHeight: image.naturalHeight, complete: image.complete }))
      })) : null;
      phases.push({ name, durationMs: Date.now() - start, url: page.url(), homeState });
      saveReport();
      phase = 'idle';
    };

    await runPhase('homepage-cold-navigation', () => page.goto(homepageUrl, { waitUntil: 'networkidle', timeout: 60000 }), true);
    const heroUrl = phases[0].homeState.images[0].url;
    const imageDirectPage = await context.newPage();
    const directCdp = await context.newCDPSession(imageDirectPage);
    await directCdp.send('Network.enable');
    await directCdp.send('Network.setCacheDisabled', { cacheDisabled });
    directCdp.on('Network.requestWillBeSent', event => {
      if (event.type !== 'Image' && !event.request.url.startsWith(heroUrl)) return;
      const request = { phase: 'direct-image-navigation-same-context', requestId: event.requestId, url: event.request.url, method: event.request.method, requestStartAfterPhaseMs: Math.round(event.wallTime * 1000 - phaseStartedAtMs), requestCacheHeaders: cacheHeaders(event.request.headers) };
      requests.push(request);
      byId.set(event.requestId, request);
    });
    directCdp.on('Network.requestServedFromCache', event => { const request = byId.get(event.requestId); if (request) request.requestServedFromCache = true; });
    directCdp.on('Network.responseReceived', event => {
      const request = byId.get(event.requestId);
      if (!request) return;
      request.status = event.response.status;
      request.fromDiskCache = event.response.fromDiskCache || false;
      request.fromServiceWorker = event.response.fromServiceWorker || false;
      request.fromPrefetchCache = event.response.fromPrefetchCache || false;
      request.responseCacheHeaders = cacheHeaders(event.response.headers);
      request.mimeType = event.response.mimeType;
    });
    directCdp.on('Network.responseReceivedExtraInfo', event => { const request = byId.get(event.requestId); if (request) { request.status = event.statusCode || request.status; request.responseCacheHeaders = cacheHeaders(event.headers); } });
    directCdp.on('Network.loadingFinished', event => { const request = byId.get(event.requestId); if (request) request.encodedDataLength = event.encodedDataLength; });
    directCdp.on('Network.loadingFailed', event => { const request = byId.get(event.requestId); if (request) request.error = event.errorText; });

    phase = 'direct-image-navigation-same-context';
    phaseStartedAtMs = Date.now();
    await imageDirectPage.goto(heroUrl, { waitUntil: 'load', timeout: 60000 });
    await imageDirectPage.waitForTimeout(900);
    phases.push({ name: phase, url: imageDirectPage.url(), directImage: true });
    saveReport();

    await runPhase('homepage-reload-after-direct-image', () => page.reload({ waitUntil: 'networkidle', timeout: 60000 }), true);
    const infoLink = page.locator('a[href="/info"]').first();
    if (await infoLink.count()) {
      await runPhase('in-app-home-to-info', () => infoLink.click(), false);
      await runPhase('in-app-back-to-home', () => page.goBack({ waitUntil: 'load', timeout: 20000 }), true);
    }
    await runPhase('homepage-warm-reload', () => page.reload({ waitUntil: 'networkidle', timeout: 60000 }), true);

    saveReport();
    console.log(`CACHE_PROFILE=${outputPath}`);
    console.log(JSON.stringify({ phases, heroRequests: requests.filter(request => request.url.includes('/images/')) }, null, 2));
    await directCdp.detach();
    await cdp.detach();
  } finally {
    await context.close();
  }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
