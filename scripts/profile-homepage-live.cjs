const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const { chromium } = require('playwright');

const origin = process.env.HOMEPAGE_URL || 'https://dev-lan.mercurius-aalst.be';
const output = path.resolve(process.env.HOMEPAGE_PROFILE_DIR || 'docs/performance/homepage-live-comparison');
const width = Number(process.env.HOMEPAGE_WIDTH || 1920);
const height = Number(process.env.HOMEPAGE_HEIGHT || 940);
const dpr = Number(process.env.HOMEPAGE_DPR || 1.25);
const repeats = Number(process.env.HOMEPAGE_REPEATS || 3);
const sampleMs = Number(process.env.HOMEPAGE_SAMPLE_MS || 2000);
const wheelIntervalMs = Number(process.env.HOMEPAGE_WHEEL_INTERVAL_MS || 100);
const wheelStep = Number(process.env.HOMEPAGE_WHEEL_STEP || 100);
const action = process.env.HOMEPAGE_ACTION || 'scroll';
const phaseDelayMs = Number(process.env.HOMEPAGE_PHASE_DELAY_MS || 0);
const controlName = process.env.HOMEPAGE_CONTROL || '';
const browserPath = process.env.CHROME_EXECUTABLE_PATH || chromium.executablePath();
const launchArgs = ['--enable-gpu', `--window-size=${width},${height + 140}`];
if (process.env.HOMEPAGE_DISABLE_GPU === '1') launchArgs.push('--disable-gpu');
const categories = 'devtools.timeline,blink.user_timing,cc,benchmark,disabled-by-default-devtools.timeline.frame';
const controls = {
  'single-image-blur-surface': {
    selector: '.home-hero-media-art', css: 'html.issue120-single-blur .home-hero-media-art{position:absolute;inset:0;pointer-events:none;filter:blur(12px)}html.issue120-single-blur .home-hero-media-image{filter:none!important}', property: 'filter',
    before: values => values.length === 1 && values[0] === 'none',
    after: values => values.length === 1 && values[0] === 'blur(12px)',
    extra: [
      { selector: '.home-hero-media-image', property: 'filter', before: values => values.length === 3 && values.every(value => value === 'blur(12px)'), after: values => values.length === 3 && values.every(value => value === 'none') },
      { selector: '.home-hero-media-image', property: 'animationName', before: values => values.length === 3 && (values.every(value => value !== 'none') || values.every(value => value === 'none')), after: values => values.length === 3 && (values.every(value => value !== 'none') || values.every(value => value === 'none')) }
    ]
  }
};

async function prepare(page, route) {
  await page.goto(new URL(route, origin).href, { waitUntil: 'networkidle', timeout: 45000 });
  await page.evaluate(() => document.fonts.ready);
  await page.locator(route === '/' ? '.home-hero-title' : 'main').waitFor({ state: 'visible' });
  await page.waitForTimeout(750);
  if (route === '/') {
    await page.waitForFunction(() => document.querySelectorAll('.home-hero-media-image').length > 0 && document.querySelectorAll('#home-sponsors img').length > 0, null, { timeout: 45000 });
  }
  const themeState = await page.locator('.layout-shell').getAttribute('data-theme-state');
  if (themeState !== 'dark') {
    await page.locator('.theme-toggle').click();
    await page.waitForFunction(() => document.querySelector('.layout-shell')?.dataset.themeState === 'dark');
  }

  // Warm lazy assets identically before timing either page.
  await page.evaluate(async () => {
    window.scrollTo(0, document.documentElement.scrollHeight);
    await new Promise(resolve => setTimeout(resolve, 350));
    await Promise.all([...document.images].map(image => image.decode().catch(() => {})));
    window.scrollTo(0, 0);
  });
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(500);

  const state = await page.evaluate(() => {
    const images = [...document.images].map(image => {
      const rect = image.getBoundingClientRect();
      return {
        src: image.currentSrc || image.src,
        alt: image.alt,
        complete: image.complete,
        naturalWidth: image.naturalWidth,
        naturalHeight: image.naturalHeight,
        renderedPixelBounds: { width: Math.round(rect.width * devicePixelRatio), height: Math.round(rect.height * devicePixelRatio) },
        visibleAtTop: rect.bottom > 0 && rect.top < innerHeight && rect.right > 0 && rect.left < innerWidth
      };
    });
    return {
      theme: document.querySelector('.layout-shell')?.dataset.themeState,
      viewport: { width: innerWidth, height: innerHeight, dpr: devicePixelRatio },
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth: document.documentElement.clientWidth,
      scrollHeight: document.documentElement.scrollHeight,
      imageCount: images.length,
      decodedImages: images.filter(image => image.complete && image.naturalWidth > 0).length,
      sponsorImages: [...document.querySelectorAll('#home-sponsors img')].map(image => ({
        src: image.currentSrc || image.src,
        complete: image.complete,
        naturalWidth: image.naturalWidth,
        naturalHeight: image.naturalHeight
      })),
      visibleImagesAtTop: images.filter(image => image.visibleAtTop)
    };
  });
  if (state.theme !== 'dark') throw new Error(`Dark theme was not applied: ${state.theme}`);
  if (state.decodedImages !== state.imageCount) throw new Error(`Not all images decoded: ${state.decodedImages}/${state.imageCount}`);
  if (route === '/' && state.sponsorImages.some(image => !image.complete || !image.naturalWidth)) {
    throw new Error('A homepage sponsor image did not decode');
  }
  return state;
}

async function traceScroll(page, pageName, label) {
  const cdp = await page.context().newCDPSession(page);
  await syncAnimationPhase(page);
  await page.bringToFront();
  await page.mouse.move(Math.floor(width / 2), Math.floor(height / 2));
  const focus = await page.evaluate(() => ({ visibility: document.visibilityState, hasFocus: document.hasFocus() }));
  if (focus.visibility !== 'visible' || !focus.hasFocus) throw new Error(`Page is not foreground: ${JSON.stringify(focus)}`);
  await cdp.send('Tracing.start', { categories, transferMode: 'ReturnAsStream' });
  const maxScrollY = await page.evaluate(() => document.documentElement.scrollHeight - innerHeight);
  const targetY = Math.min(600, maxScrollY);
  const start = Date.now();
  let direction = 1;
  let previousY = 0;
  let movedPixels = 0;
  let reversals = 0;
  let minY = 0;
  let maxY = 0;
  const ySamples = [];
  const wheelLatenciesMs = [];
  while (action === 'scroll' && Date.now() - start < sampleMs) {
    const wheelStart = Date.now();
    await page.mouse.wheel(0, direction * wheelStep);
    wheelLatenciesMs.push(Date.now() - wheelStart);
    await page.waitForTimeout(wheelIntervalMs);
    const y = await page.evaluate(() => window.scrollY);
    movedPixels += Math.abs(y - previousY);
    previousY = y;
    minY = Math.min(minY, y);
    maxY = Math.max(maxY, y);
    ySamples.push(y);
    if (direction > 0 && y >= targetY - 2) { direction = -1; reversals++; }
    else if (direction < 0 && y <= 2) { direction = 1; reversals++; }
  }
  if (action === 'idle') await page.waitForTimeout(sampleMs);
  const completed = new Promise(resolve => cdp.once('Tracing.tracingComplete', resolve));
  await cdp.send('Tracing.end');
  const event = await completed;
  const chunks = [];
  let eof = false;
  while (!eof) {
    const chunk = await cdp.send('IO.read', { handle: event.stream });
    chunks.push(chunk.data);
    eof = chunk.eof;
  }
  await cdp.send('IO.close', { handle: event.stream });
  const raw = chunks.join('');
  const trace = JSON.parse(raw);
  const events = trace.traceEvents || [];
  const processNames = new Map(events.filter(item => item.ph === 'M' && item.name === 'process_name').map(item => [item.pid, item.args?.name]));
  const threadNames = new Map(events.filter(item => item.ph === 'M' && item.name === 'thread_name').map(item => [`${item.pid}:${item.tid}`, item.args?.name]));
  const metadata = events.find(item => item.name === 'TracingStartedInBrowser')?.args?.data;
  const frames = metadata?.frames || [];
  const mainFrame = frames.find(frame => frame.url === new URL(pageName === 'home' ? '/' : '/tournaments', origin).href) || frames.find(frame => frame.parent === undefined);
  const rendererPids = [...processNames].filter(([, name]) => name === 'Renderer').map(([pid]) => pid);
  if (rendererPids.length !== 1 && process.env.HOMEPAGE_REDUCED_MOTION !== '1') throw new Error(`Expected one foreground Renderer process, found ${rendererPids.length}`);
  const rendererEvents = events.filter(item => rendererPids.includes(item.pid));
  const rendererThreads = [...new Set(rendererEvents.map(item => threadNames.get(`${item.pid}:${item.tid}`)).filter(Boolean))];
  const counts = {};
  const durationUs = {};
  const eventDurations = {};
  const selected = /^(BeginMainFrame|BeginMainFrameToCommit|Commit|Layerize|PrePaint|Paint|RasterTask|CompositeLayers|UpdateLayer|Animation|DrawFrame|MainFrame.Draw|Swap|SubmitCompositorFrameToPresentationCompositorFrame|DidNotSubmitInLastFrame)$/;
  for (const item of rendererEvents) {
    if (!selected.test(item.name)) continue;
    counts[item.name] = (counts[item.name] || 0) + 1;
    if (Number.isFinite(item.dur)) {
      durationUs[item.name] = (durationUs[item.name] || 0) + item.dur;
      (eventDurations[item.name] ||= []).push(item.dur / 1000);
    }
  }
  const eventDurationStatsMs = Object.fromEntries(Object.entries(eventDurations).map(([name, values]) => {
    values.sort((a, b) => a - b);
    return [name, { count: values.length, p95: +values[Math.floor(values.length * .95)].toFixed(2), max: +values.at(-1).toFixed(2), over16_7ms: values.filter(value => value > 16.7).length }];
  }));
  const gpu = events.filter(item => /gpu/i.test(processNames.get(item.pid) || ''));
  const gpuCounts = {};
  for (const item of gpu) if (['Swap', 'SubmitCompositorFrameToPresentationCompositorFrame', 'Graphics.Pipeline.DrawAndSwap'].includes(item.name)) gpuCounts[item.name] = (gpuCounts[item.name] || 0) + 1;
  const gpuPipelineSteps = {};
  for (const item of gpu) {
    if (item.name !== 'Graphics.Pipeline' || !Number.isFinite(item.dur)) continue;
    const step = item.args?.chrome_graphics_pipeline?.step || 'unknown';
    (gpuPipelineSteps[step] ||= []).push(item.dur / 1000);
  }
  const gpuGraphicsPipelineStepsMs = Object.fromEntries(Object.entries(gpuPipelineSteps).map(([step, values]) => {
    values.sort((a, b) => a - b);
    const total = values.reduce((sum, value) => sum + value, 0);
    return [step, { count: values.length, total: +total.toFixed(1), mean: +(total / values.length).toFixed(3), p95: +values[Math.floor(values.length * .95)].toFixed(2), max: +values.at(-1).toFixed(2) }];
  }));
  const pipelineFrames = new Map();
  for (const item of rendererEvents) {
    if (item.name !== 'PipelineReporter' || item.ph !== 'b') continue;
    const frame = item.args?.frame_reporter;
    if (!frame) continue;
    const key = `${item.pid}:${frame.layer_tree_host_id}:${frame.frame_source}:${frame.frame_sequence}`;
    pipelineFrames.set(key, { ...frame, ts: item.ts });
  }
  const pipelineStateCounts = {};
  const pipelineScrollCounts = {};
  // PipelineReporter ph=b is the pipeline begin timestamp, not display presentation time.
  const pipelineStartTimestampsForPresentedStateFrames = [];
  for (const frame of pipelineFrames.values()) {
    pipelineStateCounts[frame.state] = (pipelineStateCounts[frame.state] || 0) + 1;
    pipelineScrollCounts[frame.scroll_state] = (pipelineScrollCounts[frame.scroll_state] || 0) + 1;
    if (frame.state === 'STATE_PRESENTED_ALL' || frame.state === 'STATE_PRESENTED_PARTIAL') pipelineStartTimestampsForPresentedStateFrames.push(frame.ts);
  }
  pipelineStartTimestampsForPresentedStateFrames.sort((a, b) => a - b);
  const pipelineStartIntervalsForPresentedStateFramesMs = pipelineStartTimestampsForPresentedStateFrames.slice(1).map((value, index) => (value - pipelineStartTimestampsForPresentedStateFrames[index]) / 1000).sort((a, b) => a - b);
  const pipelineStartPercentile = p => pipelineStartIntervalsForPresentedStateFramesMs[Math.min(pipelineStartIntervalsForPresentedStateFramesMs.length - 1, Math.floor(pipelineStartIntervalsForPresentedStateFramesMs.length * p))] || 0;
  const summary = {
    rendererPids,
    rendererName: 'Renderer',
    mainFrame,
    rendererThreadNames: rendererThreads,
    rendererEventCount: rendererEvents.length,
    rendererCounts: counts,
    rendererDurationMs: Object.fromEntries(Object.entries(durationUs).map(([name, us]) => [name, +(us / 1000).toFixed(1)])),
    rendererEventDurationStatsMs: eventDurationStatsMs,
    focus,
    pipelineFrames: pipelineFrames.size,
    pipelineStateCounts,
    pipelineScrollCounts,
    pipelineStartIntervalsForPresentedStateFramesMs: {
      count: pipelineStartIntervalsForPresentedStateFramesMs.length,
      p50: +pipelineStartPercentile(.5).toFixed(2),
      p95: +pipelineStartPercentile(.95).toFixed(2),
      over16_7ms: pipelineStartIntervalsForPresentedStateFramesMs.filter(interval => interval > 16.7).length
    },
    rendererMainThreadEventCount: rendererEvents.filter(item => threadNames.get(`${item.pid}:${item.tid}`) === 'CrRendererMain').length,
    rendererCompositorThreadEventCount: rendererEvents.filter(item => threadNames.get(`${item.pid}:${item.tid}`) === 'Compositor').length,
    gpuProcessNames: [...new Set(gpu.map(item => processNames.get(item.pid)).filter(Boolean))],
    gpuCounts,
    gpuGraphicsPipelineStepsMs,
    action,
    wheelInput: { count: wheelLatenciesMs.length, p95CommandLatencyMs: wheelLatenciesMs.sort((a, b) => a - b)[Math.floor(wheelLatenciesMs.length * .95)] || 0, maxCommandLatencyMs: wheelLatenciesMs.at(-1) || 0, requestedDeltaPx: wheelLatenciesMs.length * wheelStep },
    scrollJourney: { maxScrollY, targetY, minY, maxY, movedPixels, directionReversals: reversals, ySamples, finalY: await page.evaluate(() => window.scrollY) },
    traceEventCount: events.length,
    traceBytes: raw.length
  };
  fs.writeFileSync(path.join(output, `${label}.json.gz`), zlib.gzipSync(raw));
  await cdp.detach();
  return summary;
}

async function syncAnimationPhase(page) {
  await page.evaluate(() => {
    window.scrollTo(0, 0);
    for (const animation of document.getAnimations({ subtree: true })) {
      if (animation.timeline !== document.timeline) continue;
      animation.pause();
      animation.currentTime = 3000;
      animation.play();
    }
  });
  await page.waitForTimeout(250);
}

async function main() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: false, executablePath: browserPath, args: launchArgs });
  try {
      const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: dpr, ...(process.env.HOMEPAGE_REDUCED_MOTION === '1' ? { reducedMotion: 'reduce' } : {}) });
    const page = await context.newPage();
    const results = { url: origin, browser: await browser.version(), browserPath, launchArgs, width, height, dpr, repeats, sampleMs, action, phaseDelayMs, wheelIntervalMs, wheelStep, categories, controlName: controlName || null, order: [], trials: [] };
    const order = [];
    if (controlName) {
      if (!controls[controlName]) throw new Error(`Unknown HOMEPAGE_CONTROL ${controlName}`);
      for (let index = 0; index < repeats; index++) order.push(...(index % 2 ? [controlName, 'baseline'] : ['baseline', controlName]));
    } else {
      for (let index = 0; index < repeats; index++) order.push(...(index % 2 ? ['tournaments', 'home'] : ['home', 'tournaments']));
    }
    const samePageSingleBlur = controlName === 'single-image-blur-surface';
    let localPatchedMarkup = false;
    if (samePageSingleBlur) {
      await prepare(page, '/');
      localPatchedMarkup = await page.locator('.home-hero-media-art').count() === 1;
      if (!localPatchedMarkup) await page.evaluate(() => {
        const images = [...document.querySelectorAll('.home-hero-media-image')];
        if (images.length !== 3) throw new Error(`Expected 3 hero images, found ${images.length}`);
        const group = document.createElement('div');
        group.className = 'home-hero-media-art';
        images[0].before(group);
        for (const image of images) group.append(image);
      });
      await page.addStyleTag({ content: `${controls[controlName].css}html.issue120-image-blur-baseline .home-hero-media-art{filter:none!important}html.issue120-image-blur-baseline .home-hero-media-image{filter:blur(12px)!important}` });
      await page.evaluate(() => document.documentElement.classList.add('issue120-image-blur-baseline'));
      results.localPatchedMarkup = localPatchedMarkup;
    }
    for (let index = 0; index < order.length; index++) {
      const name = order[index];
      const pageName = controlName ? 'home' : name;
      const label = `${String(index + 1).padStart(2, '0')}-${pageName}-${name}`;
      const route = pageName === 'home' ? '/' : '/tournaments';
      if (!samePageSingleBlur) await prepare(page, route);
      let controlCheck = null;
      if (controlName && name === controlName) {
        const control = controls[controlName];
        if (samePageSingleBlur) await page.evaluate(() => { document.documentElement.classList.remove('issue120-single-blur'); document.documentElement.classList.add('issue120-image-blur-baseline'); });
        const before = await page.evaluate(({ selector, property, pseudo }) => [...document.querySelectorAll(selector)].map(element => getComputedStyle(element, pseudo)[property]), { selector: control.selector, property: control.property, pseudo: control.pseudo || null });
        if (!control.before(before)) throw new Error(`${controlName}: precondition failed (${before.join(', ')})`);
        const extras = Array.isArray(control.extra) ? control.extra : control.extra ? [control.extra] : [];
        const extraChecks = [];
        for (const extra of extras) {
          const extraParams = { selector: extra.selector, property: extra.property, pseudo: extra.pseudo || null };
          const extraBefore = await page.evaluate(({ selector, property, pseudo }) => [...document.querySelectorAll(selector)].map(element => getComputedStyle(element, pseudo)[property]), extraParams);
          if (!extra.before(extraBefore)) throw new Error(`${controlName}: extra precondition failed (${extraBefore.join(', ')})`);
          extraChecks.push({ extra, extraParams, extraBefore });
        }
        if (samePageSingleBlur) await page.evaluate(() => { document.documentElement.classList.remove('issue120-image-blur-baseline'); document.documentElement.classList.add('issue120-single-blur'); });
        else await page.addStyleTag({ content: control.css });
        const after = await page.evaluate(({ selector, property, pseudo }) => [...document.querySelectorAll(selector)].map(element => getComputedStyle(element, pseudo)[property]), { selector: control.selector, property: control.property, pseudo: control.pseudo || null });
        if (!control.after(after)) throw new Error(`${controlName}: override failed (${after.join(', ')})`);
        controlCheck = { selector: control.selector, property: control.property, before, after };
        for (const { extra, extraParams, extraBefore } of extraChecks) {
          const extraAfter = await page.evaluate(({ selector, property, pseudo }) => [...document.querySelectorAll(selector)].map(element => getComputedStyle(element, pseudo)[property]), extraParams);
          if (!extra.after(extraAfter)) throw new Error(`${controlName}: extra override failed (${extraAfter.join(', ')})`);
          (controlCheck.extra ||= []).push({ selector: extra.selector, pseudo: extra.pseudo, property: extra.property, before: extraBefore, after: extraAfter });
        }
      } else if (samePageSingleBlur) {
        await page.evaluate(() => { document.documentElement.classList.remove('issue120-single-blur'); document.documentElement.classList.add('issue120-image-blur-baseline'); });
      }
      if (phaseDelayMs) await page.waitForTimeout(phaseDelayMs);
      const state = await page.evaluate(() => ({
        theme: document.querySelector('.layout-shell')?.dataset.themeState,
        viewport: { width: innerWidth, height: innerHeight, dpr: devicePixelRatio },
        documentWidth: document.documentElement.scrollWidth,
        viewportWidth: document.documentElement.clientWidth,
        scrollHeight: document.documentElement.scrollHeight,
        navigationTiming: (() => { const entry = performance.getEntriesByType('navigation')[0]; return entry ? { responseEndMs: +entry.responseEnd.toFixed(1), domContentLoadedMs: +entry.domContentLoadedEventEnd.toFixed(1), loadEventEndMs: +entry.loadEventEnd.toFixed(1), transferSizeBytes: entry.transferSize } : null; })(),
        imageCount: [...document.images].length,
        decodedImages: [...document.images].filter(image => image.complete && image.naturalWidth > 0).length,
        heroAssets: [...document.querySelectorAll('.home-hero-media-image')].map(image => {
          const resource = performance.getEntriesByName(image.currentSrc || image.src).at(-1);
          return { src: image.currentSrc || image.src, intrinsicWidth: image.naturalWidth, intrinsicHeight: image.naturalHeight, renderedWidthPx: Math.round(image.getBoundingClientRect().width * devicePixelRatio), renderedHeightPx: Math.round(image.getBoundingClientRect().height * devicePixelRatio), resourceDurationMs: resource ? +resource.duration.toFixed(1) : null, encodedBodySizeBytes: resource?.encodedBodySize ?? null, transferSizeBytes: resource?.transferSize ?? null };
        }),
        visibleImagesAtTop: [...document.images].filter(image => { const r = image.getBoundingClientRect(); return r.bottom > 0 && r.top < innerHeight && r.right > 0 && r.left < innerWidth; }).map(image => { const r = image.getBoundingClientRect(); return { src: image.currentSrc || image.src, width: image.naturalWidth, height: image.naturalHeight, renderedPixelBounds: { width: Math.round(r.width * devicePixelRatio), height: Math.round(r.height * devicePixelRatio) } }; }),
        sponsorImageCount: document.querySelectorAll('#home-sponsors img').length,
        decodedSponsorImages: [...document.querySelectorAll('#home-sponsors img')].filter(image => image.complete && image.naturalWidth > 0).length
      }));
      const screenshotCase = controlName ? (name === controlName ? 'control' : name) : name;
      const screenshotPath = path.join(output, `${screenshotCase}.png`);
      if (!fs.existsSync(screenshotPath)) {
        await syncAnimationPhase(page);
        await page.screenshot({ path: screenshotPath });
      }
      await page.bringToFront();
      const metrics = await traceScroll(page, pageName, label);
      results.order.push(name);
      results.trials.push({ label, page: pageName, case: name, state, controlCheck, screenshotPath, ...metrics });
      fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(results, null, 2));
      console.log(JSON.stringify(results.trials.at(-1)));
    }
    await context.close();
    console.log(`SUMMARY=${path.join(output, 'summary.json')}`);
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
