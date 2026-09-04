import { defineCollection } from "astro:content";
import { glob } from "astro/loaders";
import { z } from "astro/zod";

const parameterSchema = z.object({
  name: z.string(),
  type: z.string(),
  required: z.boolean(),
  default: z.union([z.string(), z.number(), z.boolean(), z.null()]).optional(),
  description: z.string()
});

const docs = defineCollection({
  loader: glob({
    pattern: "**/*.md",
    base: "../docs"
  }),
  schema: z.object({
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
    parameters: z.array(parameterSchema).optional(),
    related: z.array(z.string()).optional(),
    migration: z.enum(["legacy", "partial", "complete"]).optional()
  }).passthrough()
});

export const collections = { docs };
