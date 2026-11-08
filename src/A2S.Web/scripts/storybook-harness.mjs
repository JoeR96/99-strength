// Serves a built Storybook (storybook-static) over plain HTTP and lists the page-level
// stories from its index.json, so Playwright can open each one through iframe.html?id=.
//
// Used by tests/storybook/*.spec.ts via playwright.storybook.config.ts. Run
// `npm run build-storybook -- --quiet` first; the harness never builds.
import { createServer } from 'node:http';
import { createReadStream, existsSync, readFileSync, statSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = fileURLToPath(new URL('.', import.meta.url));
export const STORYBOOK_STATIC = resolve(here, '..', 'storybook-static');
export const DEFAULT_PORT = 6007;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ico': 'image/x-icon',
  '.map': 'application/json',
  '.txt': 'text/plain; charset=utf-8',
};

/**
 * The stories that render a whole page. The repo has no `pages-*` titles yet, so page-level
 * features are matched by title: anything under a `*Page` component, the setup wizard and
 * the workout dashboard. A `pages-*` id always counts, so moving stories under a "Pages"
 * title picks them up with no change here.
 */
export const PAGE_STORY_PATTERN = /^pages-|page--|-setupwizard--|-workoutdashboard--/;

export function listStories(root = STORYBOOK_STATIC) {
  const indexPath = join(root, 'index.json');
  if (!existsSync(indexPath)) {
    throw new Error(`No Storybook build at ${indexPath}. Run "npm run build-storybook -- --quiet" first.`);
  }
  const index = JSON.parse(readFileSync(indexPath, 'utf8'));
  return Object.values(index.entries).filter((entry) => entry.type === 'story');
}

export function listPageStories(root = STORYBOOK_STATIC) {
  return listStories(root).filter((story) => PAGE_STORY_PATTERN.test(story.id));
}

export function storyUrl(baseUrl, id) {
  return `${baseUrl.replace(/\/$/, '')}/iframe.html?id=${encodeURIComponent(id)}&viewMode=story`;
}

export function serve(root = STORYBOOK_STATIC, port = DEFAULT_PORT) {
  if (!existsSync(join(root, 'iframe.html'))) {
    throw new Error(`No Storybook build at ${root}. Run "npm run build-storybook -- --quiet" first.`);
  }

  const server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', 'http://localhost');
    let pathname = decodeURIComponent(url.pathname);
    if (pathname === '/') pathname = '/index.html';
    const file = normalize(join(root, pathname));
    if (!file.startsWith(root) || !existsSync(file) || statSync(file).isDirectory()) {
      res.writeHead(404, { 'content-type': 'text/plain' });
      res.end('not found');
      return;
    }
    res.writeHead(200, {
      'content-type': MIME[extname(file).toLowerCase()] ?? 'application/octet-stream',
      'cache-control': 'no-store',
    });
    createReadStream(file).pipe(res);
  });

  return new Promise((resolveServer, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', () => resolveServer(server));
  });
}

const invokedDirectly = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (invokedDirectly) {
  const port = Number(process.env.STORYBOOK_PORT ?? DEFAULT_PORT);
  if (process.argv.includes('--list')) {
    for (const story of listPageStories()) console.log(story.id);
  } else {
    await serve(STORYBOOK_STATIC, port);
    console.log(`storybook-static served at http://127.0.0.1:${port} (${listPageStories().length} page stories)`);
  }
}
