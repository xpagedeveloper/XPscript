# If / ElseIf / Else layout verification TODO

(c) xpagedeveloper.com 2026

- [x] single-line `If condition Then statement` regression added; no `End If` is required when the complete If is on one physical line
- [x] single-line `If condition Then statement Else statement` regression added for both true and false branch selection; no `End If` is required
- [x] standard multi-line `If / ElseIf / Else / End If` regression added
- [x] split `Then` layout regression retained for block If/ElseIf
- [x] inline `ElseIf ... Then statement` inside a block regression retained
- [ ] verify the permanent `If ElseIf Layout Verification` and `Control Flow and Error Handling Compatibility` workflows are green on the exact final PR head
- [ ] archive this TODO to `todo/done/` after exact-head verification and merge
