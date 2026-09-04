import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "astro/config";

const here = dirname(fileURLToPath(import.meta.url));
const docsRoot = resolve(here, "../docs");

function rewriteDocsMarkdownLinks() {
  return (tree, file) => {
    const sourcePath = file.path ? resolve(String(file.path)) : null;

    const walk = (node) => {
      if (node && node.type === "link" && typeof node.url === "string" && sourcePath) {
        const [target, fragment] = node.url.split("#", 2);

        if (target && target.toLowerCase().endsWith(".md") && !/^[a-z]+:/i.test(target)) {
          const absoluteTarget = resolve(dirname(sourcePath), target);
          const relativeToDocs = absoluteTarget.startsWith(docsRoot) ? absoluteTarget.slice(docsRoot.length) : null;

          if (relativeToDocs !== null && !relativeToDocs.startsWith("..")) {
            let rewritten = target.replace(/index\.md$/i, "").replace(/\.md$/i, "/");
            if (fragment) rewritten += `#${fragment}`;
            node.url = rewritten;
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
  base: "/XPscript",
  trailingSlash: "always",
  markdown: {
    remarkPlugins: [rewriteDocsMarkdownLinks],
    shikiConfig: {
      theme: "github-dark"
    }
  }
});
