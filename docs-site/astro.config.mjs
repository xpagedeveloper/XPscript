import { resolve, dirname, relative, sep, posix } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "astro/config";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");
const docsRoot = resolve(repoRoot, "docs");
const siteBase = "/XPscript";
const repositoryBlobBase = "https://github.com/xpagedeveloper/XPscript/blob/main";

function isInside(parent, child) {
  const path = relative(parent, child);
  return path === "" || (!path.startsWith("..") && !path.startsWith(`..${sep}`));
}

function docsRouteForPath(absoluteTarget) {
  if (!isInside(docsRoot, absoluteTarget)) return null;

  let path = relative(docsRoot, absoluteTarget).split(sep).join("/");
  if (!path.toLowerCase().endsWith(".md")) return null;

  path = path
    .replace(/(^|\/)index\.md$/i, "$1")
    .replace(/\.md$/i, "/")
    .replace(/^\/+/, "")
    .toLowerCase();

  return path ? `/${path}` : "/";
}

function relativeDocsUrl(sourcePath, absoluteTarget, fragment) {
  const sourceRoute = docsRouteForPath(sourcePath);
  const targetRoute = docsRouteForPath(absoluteTarget);
  if (!sourceRoute || !targetRoute) return null;

  const path = posix.relative(sourceRoute, targetRoute);
  const url = path ? `${path}/` : "./";
  return fragment ? `${url}#${fragment}` : url;
}

function repositoryUrlForPath(absoluteTarget, fragment) {
  if (!isInside(repoRoot, absoluteTarget)) return null;

  const path = relative(repoRoot, absoluteTarget).split(sep).join("/");
  if (!path) return null;

  const url = `${repositoryBlobBase}/${path}`;
  return fragment ? `${url}#${fragment}` : url;
}

function rewriteDocsLinks() {
  return (tree, file) => {
    const sourcePath = file.path ? resolve(String(file.path)) : null;

    const walk = (node) => {
      if (
        node &&
        (node.type === "link" || node.type === "image") &&
        typeof node.url === "string" &&
        sourcePath &&
        node.url &&
        !node.url.startsWith("#") &&
        !/^[a-z][a-z0-9+.-]*:/i.test(node.url) &&
        !node.url.startsWith("//")
      ) {
        const hashIndex = node.url.indexOf("#");
        const target = hashIndex >= 0 ? node.url.slice(0, hashIndex) : node.url;
        const fragment = hashIndex >= 0 ? node.url.slice(hashIndex + 1) : "";

        if (target) {
          const absoluteTarget = resolve(dirname(sourcePath), target);
          const docsUrl = relativeDocsUrl(sourcePath, absoluteTarget, fragment);

          if (docsUrl) {
            node.url = docsUrl;
          } else {
            const repositoryUrl = repositoryUrlForPath(absoluteTarget, fragment);
            if (repositoryUrl) node.url = repositoryUrl;
          }
        }
      }

      if (node && Array.isArray(node.children)) {
        for (const child of node.children) walk(child);
      }
    };

    walk(tree);
  };
}

export default defineConfig({
  output: "static",
  site: "https://xpagedeveloper.github.io",
  base: siteBase,
  trailingSlash: "always",
  markdown: {
    remarkPlugins: [rewriteDocsLinks],
    syntaxHighlight: {
      type: "shiki",
      excludeLangs: ["math", "xpscript"]
    },
    shikiConfig: {
      theme: "github-dark"
    }
  }
});
