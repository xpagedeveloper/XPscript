# OpenAPI multi-definition REST client/server verification

Test every OpenAPI definition in `test/openapi/` one at a time. Do not batch the definitions.

For each definition:
1. Generate the XPscript REST server from the complete source definition.
2. Verify generation completes and produces real models and operations from that definition.
3. Compile/transpile an appropriate generated server regression. If the complete generated source is too large for a practical compile, keep full-definition generation/structure checks and compile the smallest representative generated regression that covers any discovered failure.
4. Generate the XPscript REST client from the same complete definition.
5. Verify generation completes and produces real models and operations from that definition.
6. Compile/transpile an appropriate generated client regression under the same rule.
7. If REST server or REST client import/generation fails because the OpenAPI definition contains unsupported or invalid input, the importer/generator must return a clear error message that identifies the source line and the OpenAPI property/field that could not be handled. Do not accept a generic parse/generation failure without actionable location/property information.
8. Add a focused regression that verifies the diagnostic includes the relevant line and property/field for every newly discovered import/generation failure.
9. If anything fails, follow `knowledge/test-failure-feedback-rule.md`: add or move a small deterministic regression so the failing behavior runs first, fix it, rerun the focused regression, then rerun the definition test.
10. Mark the definition complete only when both REST server and REST client verification pass.

## Definitions

- [ ] `test/openapi/digitalocean.yaml` — REST server + REST client
- [ ] `test/openapi/github.json` — REST server + REST client
- [ ] `test/openapi/openai.yaml` — REST server + REST client
- [ ] `test/openapi/riksantikvarieambetet.yaml` — REST server + REST client
- [ ] `test/openapi/scb.json` — REST server + REST client
- [ ] `test/openapi/skogsstyrelsen.json` — REST server + REST client
- [ ] `test/openapi/stripe.json` — REST server + REST client
- [ ] `test/openapi/swedac.json` — REST server + REST client
- [ ] `test/openapi/vinnova.yaml` — REST server + REST client

## Completion

- [ ] All definitions above pass server generation/verification.
- [ ] All definitions above pass client generation/verification.
- [ ] Relevant focused regressions pass.
- [ ] Relevant FullTest/Compile Linux and Windows CI is green.
