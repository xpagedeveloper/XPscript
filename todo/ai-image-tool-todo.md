# AI image tool TODO

(c) xpagedeveloper.com 2026

Future optional AI image generation and editing integration. This work is separate from the local XPImage runtime and must not introduce AI dependencies into normal image processing.

## AIImageTool

- [ ] Add an optional future `AIImageTool` that can attach to `AIClient` rather than hard-coding AI generation into the image runtime.
- [ ] Allow AIImageTool to create images from prompts through a configurable OpenAI-compatible or provider-specific endpoint adapter.
- [ ] Allow AIImageTool to edit supplied source images where the configured provider supports image editing.
- [ ] Support configurable endpoint, headers, model and provider-specific request properties through the same provider-neutral principles as `AIClient`.
- [ ] Return generated/edited images as normal XPScript image/Byte-array values so subsequent local image operations can be applied.
- [ ] Do not require AI dependencies for normal local image processing.
