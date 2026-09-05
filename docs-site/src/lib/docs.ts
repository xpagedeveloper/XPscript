import { posix } from "node:path";
import type { CollectionEntry } from "astro:content";

export type DocEntry = CollectionEntry<"docs">;

export function normalizedEntryId(entryId: string): string {
  return entryId
    .replace(/\\/g, "/")
    .replace(/\.md$/i, "")
    .replace(/^\/+|\/+$/g, "")
    .toLowerCase();
}

export function routeForEntry(entry: DocEntry): string {
  let id = normalizedEntryId(entry.id);

  if (id === "index") return "/";
  if (id.endsWith("/index")) id = id.slice(0, -"/index".length);

  return `/${id}/`;
}

function normalizeRoute(route: string): string {
  const trimmed = route.replace(/^\/+|\/+$/g, "");
  return trimmed ? `/${trimmed}/` : "/";
}

export function relativeRouteHref(fromRoute: string, toRoute: string): string {
  const from = normalizeRoute(fromRoute);
  const to = normalizeRoute(toRoute);
  const relative = posix.relative(from, to);

  return relative ? `${relative}/` : "./";
}

export function relativeHrefForEntry(fromEntry: DocEntry, toEntry: DocEntry): string {
  return relativeRouteHref(routeForEntry(fromEntry), routeForEntry(toEntry));
}

export function routeParamForEntry(entry: DocEntry): string | undefined {
  const route = routeForEntry(entry);
  return route === "/" ? undefined : route.slice(1, -1);
}

export function titleForEntry(entry: DocEntry): string {
  if (typeof entry.data.title === "string" && entry.data.title.trim()) {
    return entry.data.title.trim();
  }

  const id = normalizedEntryId(entry.id);
  const lastSegment = id.split("/").filter(Boolean).at(-1) ?? "XPScript";

  if (lastSegment === "index") return "XPScript Documentation";

  return lastSegment
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export function isStructuredEntry(entry: DocEntry): boolean {
  return Boolean(entry.data.id && entry.data.title && entry.data.type);
}

export function displayAccess(access: unknown): string | null {
  if (access === "ReadWrite") return "Read/Write";
  if (access === "Read") return "Read";
  return null;
}
