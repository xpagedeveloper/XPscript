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

const headingCounts = new Map();
for (const result of results) {
  if (!result.h1) continue;
  const key = result.h1.toLowerCase();
  headingCounts.set(key, (headingCounts.get(key) ?? 0) + 1);
}

const duplicateHeadings = results
  .filter((result) => result.h1 && (headingCounts.get(result.h1.toLowerCase()) ?? 0) > 1)
  .map((result) => ({ path: result.path, h1: result.h1 }));

const report = {
  generatedAt: new Date().toISOString(),
  totals: {
    markdownFiles: results.length,
    withFrontmatter: results.filter((result) => result.hasFrontmatter).length,
    withoutFrontmatter: results.filter((result) => !result.hasFrontmatter).length,
    missingH1: results.filter((result) => !result.h1).length,
    codeBlocks: results.reduce((sum, result) => sum + result.codeBlocks, 0),
    brokenLinks: results.reduce((sum, result) => sum + result.brokenLinks.length, 0),
    duplicateH1: duplicateHeadings.length
  },
  duplicateHeadings,
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
  console.log(`Missing H1:           ${report.totals.missingH1}`);
  console.log(`Code blocks:          ${report.totals.codeBlocks}`);
  console.log(`Broken local links:   ${report.totals.brokenLinks}`);
  console.log(`Duplicate H1 entries: ${report.totals.duplicateH1}`);

  const filesWithBrokenLinks = results.filter((result) => result.brokenLinks.length > 0);
  if (filesWithBrokenLinks.length) {
    console.log("\nBroken local links:");
    for (const result of filesWithBrokenLinks) {
      for (const link of result.brokenLinks) console.log(`  ${result.path}: ${link}`);
    }
  }

  if (duplicateHeadings.length) {
    console.log("\nDuplicate H1 headings:");
    for (const duplicate of duplicateHeadings) console.log(`  ${duplicate.path}: ${duplicate.h1}`);
  }

  console.log("\nThis command is informational during the compatibility migration. Strict schema validation will be enabled section by section.");
}
