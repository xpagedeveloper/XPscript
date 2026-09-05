import { defineCollection } from "astro:content";
import { glob } from "astro/loaders";
import { z } from "astro/zod";

const parameterSchema = z.object({
  name: z.string(),
  type: z.string().optional(),
  required: z.boolean(),
  default: z.union([z.string(), z.number(), z.boolean(), z.null()]).optional(),
  description: z.string()
});

const docsSchema = z.object({
  id: z.string().optional(),
  title: z.string().optional(),
  type: z.enum([
    "overview",
    "index",
    "language",
    "function-category",
    "function",
    "object",
    "property"
  ]).optional(),
  shortDescription: z.string().optional(),
  order: z.number().optional(),
  category: z.string().optional(),
  object: z.string().optional(),
  returnType: z.string().optional(),
  dataType: z.string().optional(),
  access: z.enum(["Read", "ReadWrite"]).optional(),
  syntax: z.string().optional(),
  example: z.string().regex(/^\/(?:demo|samples)\/.+\.xps$/).optional(),
  parameters: z.array(parameterSchema).optional(),
  related: z.array(z.string()).optional(),
  migration: z.enum(["legacy", "partial", "complete"]).optional()
}).passthrough().superRefine((data, ctx) => {
  if (data.migration !== "complete") return;

  const requireField = (field: keyof typeof data) => {
    if (data[field] === undefined || data[field] === null || data[field] === "") {
      ctx.addIssue({
        code: "custom",
        path: [field],
        message: `${String(field)} is required for a completely migrated document`
      });
    }
  };

  requireField("id");
  requireField("title");
  requireField("type");
  requireField("shortDescription");

  if (data.type === "function-category") {
    requireField("category");
  }

  if (data.type === "function") {
    requireField("category");
    requireField("syntax");
    requireField("parameters");
    requireField("example");
  }

  if (data.type === "property") {
    requireField("object");
    requireField("dataType");
    requireField("access");
  }
});

const docs = defineCollection({
  loader: glob({
    pattern: "**/*.md",
    base: "../docs"
  }),
  schema: docsSchema
});

export const collections = { docs };
