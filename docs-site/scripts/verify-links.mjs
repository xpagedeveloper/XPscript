import { promises as fs } from "node:fs";
import { dirname, extname, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const siteRoot = resolve(here, "../dist");
const repoRoot = resolve(here, "../..");

async function collectFiles(directory) {
  const entries = await fs.readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...await collectFiles(fullPath));
    } else if (entry.isFile()) {
      files.push(fullPath);
    }
  }

  return files;
}

async function exists(path) {
  try {
    await fs.access(path);
    return true;
  } catch {
    return false;
  }
}

function decodeHtmlAttribute(value) {
  return value
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, '"')
    .replace(/&#39;|&apos;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">");
}

function extractReferences(html) {
  const references = [];
  const pattern = /<(a|area|link|img|script|source|video|audio|iframe)\b[^>]*?\s(href|src)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi;
  let match;

  while ((match = pattern.exec(html)) !== null) {
    const value = decodeHtmlAttribute(match[3] ?? match[4] ?? "").trim();
    if (value) {
      references.push({ tag: match[1].toLowerCase(), attribute: match[2].toLowerCase(), value });
    }
  }

  return references;
}

function extractAnchors(html) {
  const anchors = new Set();
  const pattern = /\s(?:id|name)\s*=\s*(?:"([^"]+)"|'([^']+)')/gi;
  let match;

  while ((match = pattern.exec(html)) !== null) {
    anchors.add(decodeHtmlAttribute(match[1] ?? match[2] ?? ""));
  }

  return anchors;
}

function routeForHtmlFile(file) {
  const path = relative(siteRoot, file).split(sep).join("/");
  if (path === "index.html") return "/";
  if (path.endsWith("/index.html")) return `/${path.slice(0, -"index.html".length)}`;
  return `/${path}`;
}

function normalizeBase(base) {
  if (!base || base === "/") return "/";
  return `/${base.replace(/^\/+|\/+$/g, "")}/`;
}

function stripBase(pathname, base) {
  if (base === "/") return pathname;
  const baseWithoutTrailing = base.slice(0, -1);
  if (pathname === baseWithoutTrailing) return "/";
  if (pathname.startsWith(base)) return `/${pathname.slice(base.length)}`;
  return null;
}

async function resolveSiteTarget(sitePath) {
  let decoded;
  try {
    decoded = decodeURIComponent(sitePath);
  } catch {
    return null;
  }

  const clean = decoded.replace(/^\/+/, "");
  const candidates = [];

  if (!clean || decoded.endsWith("/")) {
    candidates.push(resolve(siteRoot, clean, "index.html"));
  } else if (extname(clean)) {
    candidates.push(resolve(siteRoot, clean));
  } else {
    candidates.push(resolve(siteRoot, clean));
    candidates.push(resolve(siteRoot, clean, "index.html"));
    candidates.push(resolve(siteRoot, `${clean}.html`));
  }

  for (const candidate of candidates) {
    if (candidate === siteRoot || candidate.startsWith(`${siteRoot}${sep}`)) {
      if (await exists(candidate)) return candidate;
    }
  }

  return null;
}

function relativeDisplay(path) {
  return relative(repoRoot, path).split(sep).join("/");
}

const base = normalizeBase(process.env.DOCS_BASE ?? "/XPscript");

if (!await exists(siteRoot)) {
  console.error(`Generated site directory does not exist: ${siteRoot}`);
  console.error("Run `npm run build` before `npm run docs:links`.");
  process.exit(1);
}

const allFiles = await collectFiles(siteRoot);
const htmlFiles = allFiles.filter((file) => extname(file).toLowerCase() === ".html");
const htmlCache = new Map();
const anchorCache = new Map();
const errors = [];
let referencesChecked = 0;
let internalLinksChecked = 0;
let fragmentsChecked = 0;
let repoLinksChecked = 0;
let externalLinksSkipped = 0;
let absoluteInternalLinks = 0;

async function htmlFor(file) {
  if (!htmlCache.has(file)) htmlCache.set(file, await fs.readFile(file, "utf8"));
  return htmlCache.get(file);
}

async function anchorsFor(file) {
  if (!anchorCache.has(file)) anchorCache.set(file, extractAnchors(await htmlFor(file)));
  return anchorCache.get(file);
}

for (const sourceFile of htmlFiles) {
  const html = await htmlFor(sourceFile);
  const sourceRoute = routeForHtmlFile(sourceFile);
  const sourceUrl = new URL(`${base.replace(/\/$/, "")}${sourceRoute}`, "https://docs.local");

  for (const reference of extractReferences(html)) {
    referencesChecked += 1;
    const raw = reference.value;

    if (/^(?:mailto|tel|javascript|data|blob):/i.test(raw)) continue;

    if (raw.startsWith("/") && !raw.startsWith("//")) {
      absoluteInternalLinks += 1;
      errors.push(`${relativeDisplay(sourceFile)}: internal ${reference.attribute} must be relative from the current page: ${raw}`);
      continue;
    }

    let url;
    try {
      url = new URL(raw, sourceUrl);
    } catch {
      errors.push(`${relativeDisplay(sourceFile)}: malformed ${reference.attribute}="${raw}"`);
      continue;
    }

    if (url.hostname === "xpagedeveloper.github.io" && stripBase(url.pathname, base) !== null) {
      absoluteInternalLinks += 1;
      errors.push(`${relativeDisplay(sourceFile)}: documentation URL must be relative from the current page: ${raw}`);
      continue;
    }

    if (url.hostname === "github.com" && url.pathname.startsWith("/xpagedeveloper/XPscript/blob/main/")) {
      repoLinksChecked += 1;
      const repoPath = url.pathname.slice("/xpagedeveloper/XPscript/blob/main/".length);
      let decodedRepoPath;
      try {
        decodedRepoPath = decodeURIComponent(repoPath);
      } catch {
        errors.push(`${relativeDisplay(sourceFile)}: invalid repository link encoding: ${raw}`);
        continue;
      }

      const target = resolve(repoRoot, decodedRepoPath);
      if (!(target === repoRoot || target.startsWith(`${repoRoot}${sep}`)) || !await exists(target)) {
        errors.push(`${relativeDisplay(sourceFile)}: repository target does not exist: ${raw}`);
      }
      continue;
    }

    if (url.hostname !== "docs.local") {
      externalLinksSkipped += 1;
      continue;
    }

    const sitePath = stripBase(url.pathname, base);
    if (sitePath === null) {
      errors.push(`${relativeDisplay(sourceFile)}: internal URL escapes configured base ${base}: ${raw}`);
      continue;
    }

    internalLinksChecked += 1;
    const targetFile = await resolveSiteTarget(sitePath);
    if (!targetFile) {
      errors.push(`${relativeDisplay(sourceFile)}: target does not exist: ${raw}`);
      continue;
    }

    if (url.hash) {
      fragmentsChecked += 1;
      let fragment;
      try {
        fragment = decodeURIComponent(url.hash.slice(1));
      } catch {
        errors.push(`${relativeDisplay(sourceFile)}: invalid fragment encoding: ${raw}`);
        continue;
      }

      if (fragment) {
        const targetAnchors = await anchorsFor(targetFile);
        if (!targetAnchors.has(fragment)) {
          errors.push(`${relativeDisplay(sourceFile)}: fragment #${fragment} not found in ${relativeDisplay(targetFile)} (${raw})`);
        }
      }
    }
  }
}

console.log("XPScript generated-site link verification");
console.log("");
console.log(`HTML pages:                ${htmlFiles.length}`);
console.log(`References inspected:      ${referencesChecked}`);
console.log(`Internal targets checked:  ${internalLinksChecked}`);
console.log(`Fragments checked:         ${fragmentsChecked}`);
console.log(`Repository links checked:  ${repoLinksChecked}`);
console.log(`External URLs skipped:     ${externalLinksSkipped}`);
console.log(`Absolute internal links:   ${absoluteInternalLinks}`);
console.log(`Broken links:              ${errors.length}`);

if (errors.length > 0) {
  console.error("\nBroken links:");
  for (const error of errors) console.error(`  ${error}`);
  process.exit(1);
}

console.log("\nAll generated internal links are relative and all internal targets, fragments, and repository-local example links are valid.");
