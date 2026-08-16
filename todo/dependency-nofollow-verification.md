# Dependency no-follow verification

(c) xpagedeveloper.com 2026

- [x] Open dependency sources through an already-open handle before staging.
- [x] Windows opens with `FILE_FLAG_OPEN_REPARSE_POINT` and rejects reparse-point handles.
- [x] Unix opens with `O_NOFOLLOW` on Linux, macOS and FreeBSD.
- [x] Reject directory handles before copying dependency bytes.
- [ ] Build on Windows, Ubuntu and macOS.
- [ ] Run managed/native dependency staging regressions on Windows, Ubuntu and macOS.
- [ ] Mark the corresponding security-review TODO complete after the verification gate passes.
