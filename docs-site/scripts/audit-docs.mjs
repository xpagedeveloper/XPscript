import { promises as fs } from "node:fs";
import { dirname, extname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const docsRoot = resolve(here, "../../docs");
const jsonOutput = process.argv.includes("--json");

async function collectMarkdownFiles(directory) {
  const entries = await fs.readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const fullPath = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...await collectMarkdownFiles(fullPath));
    } else if (entry.isFile() && extname(entry.name).toLowerCase() === ".md") {
      files.push(fullPath);
    }
  }

  return files;
}

function splitFrontmatter(content) {
  if (!content.startsWith("---\n") && !content.startsWith("---\r\n")) {
    return { hasFrontmatter: false, frontmatter: "", body: content };
  }

  const match = content.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n/);
  if (!match) return { hasFrontmatter: false, frontmatter: "", body: content };

  return {
    hasFrontmatter: true,
    frontmatter: match[1],
    body: content.slice(match[0].length)
  };
}

function frontmatterScalar(frontmatter, name) {
  const match = frontmatter.match(new RegExp(`^${name}:\\s*(.+)$`, "m"));
  if (!match) return null;

  const value = match[1].trim();
  if (
    (value.startsWith('"') && value.endsWith('"')) ||
    (value.startsWith("'") && value.endsWith("'"))
  ) {
    return value.slice(1, -1);
  }

  return value;
}

function extractMarkdownLinks(body) {
  const links = [];
  const pattern = /\[[^\]]*\]\(([^)]+)\)/g;
  let match;

  while ((match = pattern.exec(body)) !== null) {
    const raw = match[1].trim();
    const target = raw.startsWith("<") && raw.endsWith(">")
      ? raw.slice(1, -1)
      : raw.split(/\s+["']/)[0];
    links.push(target);
  }

  return links;
}

async function localLinkExists(sourceFile, link) {
  if (!link || link.startsWith("#") || /^[a-z][a-z0-9+.-]*:/i.test(link)) return true;

  let target = link.split("#", 1)[0].split("?", 1)[0];
  if (!target) return true;

  try {
    target = decodeURIComponent(target);
  } catch {
    return true;
  }

  const fullPath = resolve(dirname(sourceFile), target);

  try {
    await fs.access(fullPath);
    return true;
  } catch {
    return false;
  }
}

async function analyzeFile(file) {
  const content = await fs.readFile(file, "utf8");
  const { hasFrontmatter, frontmatter, body } = splitFrontmatter(content);
  const h1Match = body.match(/^#\s+(.+)$/m);
  const frontmatterTitle = hasFrontmatter ? frontmatterScalar(frontmatter, "title") : null;
  const pageTitle = frontmatterTitle ?? h1Match?.[1].trim() ?? null;
  const migration = hasFrontmatter ? frontmatterScalar(frontmatter, "migration") : null;
  const documentType = hasFrontmatter ? frontmatterScalar(frontmatter, "type") : null;
  const codeLanguages = [...body.matchAll(/^```([^\s`]*)/gm)]
    .map((match) => match[1] || "plain");
  const frontmatterFields = hasFrontmatter
    ? [...frontmatter.matchAll(/^([A-Za-z][A-Za-z0-9_-]*):/gm)].map((match) => match[1])
    : [];
  const links = extractMarkdownLinks(body);
  const brokenLinks = [];

  for (const link of links) {
    if (!await localLinkExists(file, link)) brokenLinks.push(link);
  }

  return {
    path: relative(docsRoot, file).replace(/\\/g, "/"),
    hasFrontmatter,
    frontmatterFields,
    h1: h1Match?.[1].trim() ?? null,
    pageTitle,
    migration,
    documentType,
    codeBlocks: codeLanguages.length,
    codeLanguages,
    links: links.length,
    brokenLinks
  };
}

const files = (await collectMarkdownFiles(docsRoot)).sort();
const results = [];

for (const file of files) {
  results.push(await analyzeFile(file));
}

const titleCounts = new Map();
for (const result of results) {
  if (!result.pageTitle) continue;
  const key = result.pageTitle.toLowerCase();
  titleCounts.set(key, (titleCounts.get(key) ?? 0) + 1);
}

const duplicateTitles = results
  .filter((result) => result.pageTitle && (titleCounts.get(result.pageTitle.toLowerCase()) ?? 0) > 1)
  .map((result) => ({ path: result.path, title: result.pageTitle }));

const report = {
  generatedAt: new Date().toISOString(),
  totals: {
    markdownFiles: results.length,
    withFrontmatter: results.filter((result) => result.hasFrontmatter).length,
    withoutFrontmatter: results.filter((result) => !result.hasFrontmatter).length,
    structuredComplete: results.filter((result) => result.migration === "complete").length,
    missingTitle: results.filter((result) => !result.pageTitle).length,
    codeBlocks: results.reduce((sum, result) => sum + result.codeBlocks, 0),
    brokenLinks: results.reduce((sum, result) => sum + result.brokenLinks.length, 0),
    duplicateTitles: duplicateTitles.length
  },
  duplicateTitles,
  files: results
};

if (jsonOutput) {
  console.log(JSON.stringify(report, null, 2));
} else {
  console.log("XPScript documentation audit");
  console.log("");
  console.log(`Markdown files:       ${report.totals.markdownFiles}`);
  console.log(`With frontmatter:     ${report.totals.withFrontmatter}`);
  console.log(`Without frontmatter:  ${report.totals.withoutFrontmatter}`);
  console.log(`Structured complete:  ${report.totals.structuredComplete}`);
  console.log(`Missing page title:   ${report.totals.missingTitle}`);
  console.log(`Code blocks:          ${report.totals.codeBlocks}`);
  console.log(`Broken local links:   ${report.totals.brokenLinks}`);
  console.log(`Duplicate titles:     ${report.totals.duplicateTitles}`);

  const filesWithBrokenLinks = results.filter((result) => result.brokenLinks.length > 0);
  if (filesWithBrokenLinks.length) {
    console.log("\nBroken local links:");
    for (const result of filesWithBrokenLinks) {
      for (const link of result.brokenLinks) console.log(`  ${result.path}: ${link}`);
    }
  }

  if (duplicateTitles.length) {
    console.log("\nDuplicate page titles:");
    for (const duplicate of duplicateTitles) console.log(`  ${duplicate.path}: ${duplicate.title}`);
  }

  console.log("\nCompletely migrated documents are schema-validated during the Astro content build.");
}
