const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const url = process.env.HOMEPAGE_URL || 'http://127.0.0.1:5186';
const output = path.resolve(process.env.HOMEPAGE_RESPONSIVE_OUTPUT || 'docs/performance/issue-120/responsive');
const executablePath = process.env.CHROME_EXECUTABLE_PATH || chromium.executablePath();
const cases = [
  { width: 390, height: 844, dpr: 2 },
  { width: 768, height: 900, dpr: 2 },
  { width: 900, height: 800, dpr: 2 },
  { width: 1440, height: 900, dpr: 1.25 }
];

async function main() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({
    headless: false,
    executablePath,
    args: ['--enable-gpu']
  });
  try {
    const results = [];
    for (const item of cases) {
      const context = await browser.newContext({ viewport: { width: item.width, height: item.height }, deviceScaleFactor: item.dpr });
      const page = await context.newPage();
      await page.goto(url, { waitUntil: 'networkidle', timeout: 60000 });
      await page.waitForFunction(() => {
        const images = [...document.querySelectorAll('.home-hero-media-image')];
        return images.length > 0 && images.every(image => image.complete && image.naturalWidth > 0);
      }, undefined, { timeout: 45000 });
      await page.waitForTimeout(250);
      const state = await page.evaluate(() => ({
        viewport: { width: innerWidth, height: innerHeight, dpr: devicePixelRatio },
        documentWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
        horizontalOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
        heroImages: [...document.querySelectorAll('.home-hero-media-image')].map(image => ({
          url: image.currentSrc || image.src,
          naturalWidth: image.naturalWidth,
          filter: getComputedStyle(image).filter,
          animation: getComputedStyle(image).animationName
        }))
      }));
      await page.screenshot({ path: path.join(output, `home-${item.width}-no-blur.png`), fullPage: false });
      results.push(state);
      await context.close();
    }
    const reducedContext = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
    const page = await reducedContext.newPage();
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(url, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForFunction(() => [...document.querySelectorAll('.home-hero-media-image')].length > 0);
    await page.waitForTimeout(100);
    const reduced = await page.evaluate(() => ({
      viewport: { width: innerWidth, height: innerHeight, dpr: devicePixelRatio },
      documentWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
      horizontalOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      reducedMotion: matchMedia('(prefers-reduced-motion: reduce)').matches,
      heroImages: [...document.querySelectorAll('.home-hero-media-image')].map(image => ({ filter: getComputedStyle(image).filter, animation: getComputedStyle(image).animationName }))
    }));
    await page.screenshot({ path: path.join(output, 'home-390-no-blur-reduced-motion.png'), fullPage: false });
    fs.writeFileSync(path.join(output, 'responsive-summary.json'), JSON.stringify({ url, browser: await page.evaluate(() => navigator.userAgent), cases: results, reducedMotion: reduced }, null, 2));
    console.log(JSON.stringify({ cases: results, reducedMotion: reduced }, null, 2));
    await reducedContext.close();
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
