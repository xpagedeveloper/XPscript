# Authorization and Identity

## Goal

Build one coherent identity, authentication, session, and authorization architecture for XPscript web applications. The same model must work across the web client, WebUIForms, REST server, WASM, and server-side routes. Authorization must remain authoritative on the server even when client runtimes receive authorization information for UI decisions.

The design should support small standalone applications as well as enterprise deployments with external identity providers, multiple server instances, SQL, Domino, and custom authorization backends.

## Core principles

- Keep authentication, identity, authorization, and session transport as separate concepts.
- Use one shared principal and policy model across WebClient, WebUIForms, REST, WASM, and server-side routes.
- Server-side authorization is always authoritative.
- Client/WASM authorization information is only a safe capability projection used for UI decisions.
- Application code should normally authorize against permissions/policies, not hard-coded roles.
- XPscript owns the authorization abstraction and policy engine, while applications choose where identities, roles, and permissions are persisted.
- Keep existing `[Authenticated]` and `[Rule:...]` behavior compatible while moving them onto the shared authorization engine.
- Support both read-only external authorization sources and fully manageable authorization stores.
- Do not require XPscript to own the application's user database.

## Existing foundation

XPscript already has useful web security foundations that should be reused rather than replaced:

- server-side sessions
- session ID rotation
- authentication state
- `[Authenticated]`
- named authorization rules and `[Rule:...]`
- positive and negative rule checks
- CSRF protection for cookie/session based browser requests
- shared web runtime used by server-side routes and WebUIForms
- generated/allowlisted WASM server bridges

Current sessions are in-memory in the server process. This works for a single server instance but does not provide distributed session state for multiple XPscript server instances.

## Terminology

### Authentication

How a caller proves its identity, for example:

- XPscript application login
- session cookie
- JWT bearer token
- OpenID Connect
- Windows/Negotiate
- API key/service identity
- future LDAP/SAML integration

### Identity

Who the caller is. Internally this should preferably map onto .NET `ClaimsPrincipal` / `ClaimsIdentity` so standard identity protocols and providers can be integrated cleanly.

### Authorization

What an authenticated identity is allowed to do. Authorization should use permissions and named policies, with roles as one source of permissions.

### Session/token

How authenticated state is carried between requests. Session transport must not be confused with the durable source of user access rights.

## Shared current-user principal

Add a common current-user principal, preferably exposed through `Request.User` and/or `Authorization.CurrentUser`.

Candidate surface:

```xpscript
If Request.User.IsAuthenticated Then
    Print Request.User.Name
    Print Request.User.Subject
End If

If Request.User.IsInRole("Administrator") Then
    ...
End If

If Request.User.HasClaim("department", "finance") Then
    ...
End If

If Request.User.HasScope("orders.write") Then
    ...
End If
```

Candidate properties/functions:

- `IsAuthenticated`
- `Subject`
- `Name`
- `AuthenticationType`
- `Roles`
- `Claims`
- `Scopes`
- `TenantId`
- `SessionId`
- `ExpiresAt`
- `AuthenticationMethods`
- `AuthenticationContext`
- `IsInRole(role)`
- `HasClaim(name, value)`
- `HasScope(scope)`

`AuthenticationMethods` should allow later MFA/step-up policies, for example `password`, `mfa`, `webauthn`, `certificate`, or `external`.

## Permission and policy model

Roles alone are too coarse for application authorization. Applications should normally check named permissions/policies such as:

- `Orders.Read`
- `Orders.Create`
- `Orders.Edit`
- `Orders.Delete`
- `Orders.Approve`
- `Users.Manage`
- `Reports.Read`
- `Reports.Export`

Candidate API:

```xpscript
If Authorization.Can("Orders.Edit") Then
    ...
End If

Call Authorization.Require("Orders.Edit")
```

`Require()` should stop execution with the appropriate authorization response. For REST, an authenticated user who fails authorization should receive HTTP 403.

Policies should eventually be able to combine:

- authenticated user
- role
- permission
- claim/value
- OAuth/OIDC scope
- authentication method/MFA state
- custom XPscript authorization logic
- resource ownership
- tenant membership

The application should depend on the policy name rather than the implementation of the policy.

## Existing attributes and compatibility

Keep existing syntax working.

`[Authenticated]` can internally become an authenticated-user authorization policy.

Existing `[Rule:...]` should map onto the new named policy engine without breaking current applications.

Potential future explicit syntax:

```text
[Authorize]
[Policy:Orders.Edit]
[Roles:Administrator,OrderManager]
[Scopes:orders.write]
```

Prefer `[Policy:...]` for normal application code so role definitions can change without changing source code.

## Users, roles, permissions, grants, and denies

Use a durable authorization model conceptually based on:

```text
User
  -> Roles
      -> Permissions

User
  -> optional direct permission grants/denies
```

Example:

```text
User: fredrik
Roles:
  - SalesManager
  - ReportUser

SalesManager:
  - Orders.Read
  - Orders.Create
  - Orders.Edit
  - Customers.Read

ReportUser:
  - Reports.Read
  - Reports.Export
```

Effective permissions are the union of allowed permissions after applying explicit denies.

Support direct user grants/denies for exceptions, but encourage role-based assignment for normal administration.

An explicit `Deny` should win over `Allow` when multiple assignments conflict.

Example:

```text
SalesManager:
  Orders.Delete = Allow

User Fredrik:
  Orders.Delete = Deny

Effective:
  Orders.Delete = Deny
```

## Authorization management object model

Provide a stable XPscript API for creating and updating authorization data regardless of backend.

Candidate object model:

```text
Authorization.CurrentUser
Authorization.Users
Authorization.Roles
Authorization.Permissions
Authorization.Policies
```

Candidate management API:

```xpscript
Call Authorization.Users.AddRole(userId, "SalesManager")
Call Authorization.Users.RemoveRole(userId, "SalesManager")

Call Authorization.Users.Grant(userId, "Orders.Edit")
Call Authorization.Users.Deny(userId, "Orders.Delete")
Call Authorization.Users.Revoke(userId, "Orders.Edit")

Call Authorization.Roles.Create("SalesManager")
Call Authorization.Roles.Delete("SalesManager")
Call Authorization.Roles.Grant("SalesManager", "Orders.Edit")
Call Authorization.Roles.Revoke("SalesManager", "Orders.Edit")
```

Lookup examples:

```xpscript
roles = Authorization.Users.GetRoles(userId)
rights = Authorization.Users.GetPermissions(userId)

If Authorization.Users.HasRole(userId, "SalesManager") Then
    ...
End If
```

The public API should describe authorization concepts, not SQL tables or Domino documents.

## Provider architecture

Do not implement one large authorization object containing backend-specific branches. Use interfaces/providers and composition.

### Identity provider

Define an identity-provider abstraction, conceptually:

```text
IIdentityProvider
```

Responsibilities:

- identify/authenticate users where applicable
- resolve external identity information
- provide claims/groups/scopes from external identity systems
- validate identity state where required

### Authorization resolver

Define a read-oriented authorization abstraction, conceptually:

```text
IAuthorizationResolver
```

Responsibilities:

- resolve a user
- get roles
- get permissions
- get grants/denies
- get security/version information
- resolve effective permissions

This allows read-only providers such as external directory/group mappings.

### Authorization store

Define a management-capable abstraction, conceptually:

```text
IAuthorizationStore : IAuthorizationResolver
```

Responsibilities can include:

- `GetUser`
- `CreateUser`
- `UpdateUser`
- `DisableUser`
- `GetRoles`
- `CreateRole`
- `UpdateRole`
- `DeleteRole`
- `GetUserRoles`
- `AddUserRole`
- `RemoveUserRole`
- `GetRolePermissions`
- `GrantRolePermission`
- `RevokeRolePermission`
- `GetUserPermissions`
- `GrantUserPermission`
- `DenyUserPermission`
- `RemoveUserPermission`
- `ResolveEffectivePermissions`
- `GetSecurityVersion`
- `IncrementSecurityVersion`

Providers should expose capabilities such as:

```text
CanManageUsers
CanManageRoles
CanManagePermissions
```

so XPscript can use read-only identity/group sources without assuming it may modify them.

### Authorization engine

Add a backend-independent authorization engine that combines identity information with the configured authorization resolver/store and evaluates policies.

```text
Identity provider
       |
       v
Identity / claims
       |
       v
AuthorizationEngine <---- AuthorizationResolver/Store
       |
       v
Effective policies/capabilities
```

## Initial authorization providers

Design for these implementations:

```text
IAuthorizationResolver / IAuthorizationStore
    |
    +-- MemoryAuthorizationStore
    +-- SqlAuthorizationStore
    +-- DominoAuthorizationStore
    +-- CustomAuthorizationStore
```

### SQL authorization store

A default SQL schema could use tables conceptually like:

```text
Users
-----
UserId
UserName
Enabled
SecurityVersion

Roles
-----
RoleId
Name

Permissions
-----------
PermissionId
Name

UserRoles
---------
UserId
RoleId

RolePermissions
---------------
RoleId
PermissionId

UserPermissions
---------------
UserId
PermissionId
Allow/Deny
```

Do not expose this physical schema through the generic authorization API.

### Domino authorization store

Build a first-class Domino implementation after the generic interfaces are stable. Do not force Domino to mimic SQL storage.

The Domino provider should be free to use appropriate Domino concepts such as:

- canonical Domino user names
- Domino groups
- NSF ACL roles
- authorization/profile documents in an NSF
- Domino Directory information where appropriate

Possible mapping:

```text
Domino ACL role [OrderManager]
        |
        v
XPscript role OrderManager
        |
        v
Orders.Read
Orders.Edit
Orders.Approve
```

Identity and authorization storage may be separate. For example, identity can come from Domino Directory while application-specific permissions come from an application NSF.

### Custom provider

Allow applications to supply a custom provider eventually, so existing application databases/directories can participate without migration into an XPscript-owned database.

Conceptual XPscript implementation:

```xpscript
Class MyAuthorizationProvider
    Implements AuthorizationProvider

    Function GetRoles(userId As String)
        ...
    End Function

    Function GetPermissions(userId As String)
        ...
    End Function

    Sub AddRole(userId As String, roleName As String)
        ...
    End Sub
End Class
```

Exact syntax depends on the final XPscript interface/class implementation model.

## Authentication providers

Once the common principal and authorization engine exist, authentication mechanisms should become adapters into that model rather than separate authorization implementations.

Suggested implementation order:

1. Existing XPscript session/custom login
2. JWT Bearer for REST/service clients
3. OpenID Connect for external human identity providers
4. API key/service identity
5. Windows/Negotiate for enterprise intranet
6. LDAP/AD direct integration if needed
7. SAML if needed for legacy enterprise SSO

OIDC should be the primary external human-identity protocol and should cover providers such as Microsoft Entra ID, Keycloak, Okta, and Auth0 without provider-specific authorization logic.

## Local/custom sign-in

XPscript must support applications that do not use an external identity provider.

Conceptual API:

```xpscript
Dim identity As Identity
Set identity = New Identity("user-123")

identity.Name = "Fredrik"
Call identity.AddRole("Administrator")
Call identity.AddClaim("department", "IT")

Call Authentication.SignIn(identity)
```

`SignIn()` should:

1. validate the identity
2. rotate the session identifier
3. create the authentication ticket/session state
4. establish the current principal
5. emit an audit/security event

Provide `Authentication.SignOut()` and ensure server-side session/authentication state is invalidated.

Existing simple authenticated-session behavior should remain compatible by creating a minimal principal internally.

## External group and claim mapping

External identity providers can provide groups/claims which are mapped into local XPscript roles or policies.

Example:

```text
Entra group SalesManagers
    -> XPscript role SalesManager
    -> Orders.Read
    -> Orders.Edit
```

This allows identity to remain externally managed while application permissions remain application-specific.

Hybrid models should be supported, for example:

```text
Identity: Microsoft Entra ID
External groups: Entra ID
Application authorization: SQL
```

or:

```text
Identity: Domino Directory
Application authorization: Domino NSF
```

## Authorization state and sessions

Do not use the web session as the durable source of access rights. The authorization provider/store is authoritative.

After login, the session may cache a resolved authorization snapshot to avoid database/directory access on every request.

Example cached state:

```text
UserId = 123
Name = Fredrik
Roles:
  SalesManager
  ReportUser
Permissions:
  Orders.Read
  Orders.Create
  Orders.Edit
  Reports.Read
  Reports.Export
SecurityVersion = 17
```

### Security version

Use a `SecurityVersion` or equivalent authorization version to invalidate stale session authorization state.

Example:

```text
Database user SecurityVersion = 18
Session SecurityVersion = 17
```

The session authorization snapshot is stale and must be refreshed or invalidated.

Changing a user's roles, permissions, enabled state, or other security-sensitive authorization information should increment the version.

This prevents removed administrator/privileged rights from remaining active until session expiration.

For distributed deployments, version validation may be backed by a shared store/cache/event mechanism rather than relying on one process's memory.

## Session storage providers

Current XPscript sessions are stored in memory in the server process. Keep that implementation as the simple/default provider but introduce a pluggable session-store abstraction.

Conceptually:

```text
ISessionStore
    |
    +-- MemorySessionStore
    +-- RedisSessionStore
    +-- DatabaseSessionStore
```

### Memory session store

- current/simple behavior
- appropriate for development and single-node applications
- lost on process restart
- not shared between server instances

### Redis session store

Recommended first distributed provider because it naturally supports:

- multiple XPscript server instances
- expiration/TTL
- fast session lookup
- centralized invalidation

### Database session store

Provide an option for deployments that already have SQL/database infrastructure and do not want to deploy Redis.

### Session cookie

The browser should carry only an opaque, cryptographically random session identifier where possible. Keep authoritative authentication/session state server-side.

Server-side session data can include:

- session ID
- subject/user ID
- authentication state
- resolved authorization snapshot or identity reference
- created time
- last access time
- expiration time
- CSRF secret/state
- application session values
- authentication method/MFA state
- security/authorization version

Keeping authoritative state server-side enables immediate logout, revocation, role changes, account disabling, MFA changes, and distributed session management.

## Browser/WebClient/WebUIForms/WASM authorization projection

The browser needs enough authorization information to make UI decisions, but must never become authoritative.

Expose a sanitized current-user/capability snapshot, for example:

```json
{
  "authenticated": true,
  "subject": "12345",
  "name": "Fredrik",
  "capabilities": [
    "Orders.Read",
    "Orders.Edit",
    "Reports.Export"
  ]
}
```

Only explicitly allowed profile claims should be sent to the browser.

Client/WASM code may use:

```xpscript
If Authorization.Can("Orders.Edit") Then
    btnEdit.Visible = True
End If
```

This only controls the UI. Every server-side operation must evaluate the policy again using authoritative server state.

Changing client-side/WASM state must never grant access to a protected server operation.

## WebUIForms integration

Add authorization metadata to forms and controls.

Conceptual properties:

```xpscript
EditButton.AuthorizationPolicy = "Orders.Edit"
DeleteButton.AuthorizationPolicy = "Orders.Delete"
AdminTab.AuthorizationPolicy = "Administration.Access"
Form.AuthorizationPolicy = "Orders.Read"
```

Support behavior such as:

```text
UnauthorizedBehavior = Hidden
UnauthorizedBehavior = Disabled
```

The framework should automatically re-evaluate the control/form policy on the server before invoking protected server-side events. This avoids relying on developers to duplicate every UI permission check manually.

Explicit server-side checks should still be available:

```xpscript
Sub DeleteButton_Click()
    Call Authorization.Require("Orders.Delete")
    ...
End Sub
```

## REST integration

REST endpoints must use the same principal and policy engine.

Conceptual endpoint authorization:

```text
[Policy:Orders.Read]
GET /api/orders

[Policy:Orders.Write]
POST /api/orders
```

Different authentication transports should resolve into the same current principal:

- session cookie
- JWT bearer token
- API key/service identity
- OIDC access token

HTTP behavior:

- `401 Unauthorized` when acceptable authentication is missing/invalid
- `403 Forbidden` when the caller is authenticated but the policy denies access
- emit appropriate `WWW-Authenticate` challenge headers where applicable

Configured authentication schemes should feed generated OpenAPI `securitySchemes` and operation security requirements.

## Browser and WASM authentication modes

Support two distinct scenarios.

### XPscript application with its own backend

Default to a server-side session/BFF-style model:

- Secure cookie
- HttpOnly cookie
- appropriate SameSite policy
- OIDC access/refresh tokens remain on the server
- browser/WASM receives only identity/capability projection

Avoid making JavaScript-accessible local storage the default location for long-lived bearer/refresh tokens.

### Standalone WASM calling external APIs directly

Support OIDC Authorization Code + PKCE with short-lived access tokens where a server-side BFF/session model is not applicable.

## Resource authorization

Design the policy engine so future authorization can evaluate a specific resource, not only a global permission.

Conceptual API:

```xpscript
Call Authorization.Require("Orders.Edit", order)
```

A resource policy may inspect:

- resource owner
- current tenant
- department
- region
- resource ACL
- document/application-specific state

This should support ownership and multi-tenancy without requiring a second authorization system later.

## Multi-tenancy

Reserve tenant context in the principal/policy model even if the first implementation is single-tenant.

Potential requirements:

- `Request.User.TenantId`
- tenant membership
- tenant-specific roles
- tenant-specific permissions
- resource tenant validation
- prevention of cross-tenant authorization leakage

## Audit and observability

Emit structured security events from the identity/authorization subsystem and reuse XPscript's existing request/correlation identifiers.

Events should include at least:

- authentication succeeded
- authentication failed
- session established
- session rotated/refreshed
- sign-out
- token rejected
- authorization denied
- policy evaluated where useful
- MFA/step-up required
- authorization data changed
- role assigned/removed
- permission granted/denied/revoked
- credential/session revoked

Never log passwords, bearer tokens, refresh tokens, raw secrets, or other sensitive credential material.

## Secrets and configuration

Reuse existing XPscript facilities instead of creating another secret subsystem.

Use `Application.Registry` or normal application configuration for non-secret identity configuration.

Use `Application.Secrets` for sensitive material such as:

- OIDC client secrets
- API signing keys
- API-key material
- refresh-token protection keys
- provider credentials

`Application.Id` scoping should continue to isolate XPscript-managed secrets by application namespace.

## Proposed implementation phases

### Phase 1 - Core principal and policy engine

- [ ] Define identity/principal types and internal ClaimsPrincipal mapping.
- [ ] Add common `Request.User` / current-user surface.
- [ ] Add roles, claims, scopes, authentication methods, and subject/name.
- [ ] Define permission naming and comparison rules.
- [ ] Implement `Authorization.Can(policy)`.
- [ ] Implement `Authorization.Require(policy)`.
- [ ] Define named policy representation/evaluator.
- [ ] Map existing `[Authenticated]` to the new engine.
- [ ] Map existing `[Rule:...]` behavior to the new engine without breaking compatibility.
- [ ] Standardize 401 versus 403 behavior.
- [ ] Add tests for authentication and policy evaluation.

### Phase 2 - Authorization provider interfaces and management API

- [ ] Define `IAuthorizationResolver`.
- [ ] Define management-capable `IAuthorizationStore`.
- [ ] Define provider capability flags for read-only/manage users/manage roles/manage permissions.
- [ ] Define `Authorization.Users`.
- [ ] Define `Authorization.Roles`.
- [ ] Define `Authorization.Permissions`.
- [ ] Define grants and explicit denies with Deny precedence.
- [ ] Add `SecurityVersion`/authorization-version semantics.
- [ ] Add memory/reference provider for tests and small applications.
- [ ] Add custom-provider integration point.
- [ ] Ensure all management changes generate audit events and invalidate/refresh affected authorization snapshots.

### Phase 3 - WebClient, WebUIForms, and WASM

- [ ] Define safe current-user/capability bootstrap payload.
- [ ] Ensure only allowlisted claims/profile data are exposed to clients.
- [ ] Add client-side `Authorization.Can()` capability checks.
- [ ] Add authentication-state change/update mechanism.
- [ ] Add WebUIForms `AuthorizationPolicy` for forms and controls.
- [ ] Add Hidden/Disabled unauthorized UI behavior.
- [ ] Revalidate WebUIForms policies server-side before protected events execute.
- [ ] Protect generated WASM server bridge calls with the same policy engine.
- [ ] Test client tampering cannot bypass server authorization.

### Phase 4 - Session-store abstraction

- [ ] Extract current in-memory session implementation behind `ISessionStore`.
- [ ] Keep `MemorySessionStore` as the default/simple provider.
- [ ] Define distributed session serialization and versioning.
- [ ] Add Redis session provider.
- [ ] Add database session provider.
- [ ] Add expiration, idle timeout, rotation, revocation, and cleanup semantics consistently across providers.
- [ ] Integrate authorization `SecurityVersion` validation with sessions.
- [ ] Test multiple XPscript server instances sharing sessions.

### Phase 5 - REST and authentication providers

- [ ] Route all REST authentication into the common principal.
- [ ] Add JWT bearer authentication.
- [ ] Add OpenID Connect authentication.
- [ ] Add API key/service identity authentication.
- [ ] Add sign-in/sign-out/challenge abstraction.
- [ ] Keep browser OIDC access/refresh tokens server-side in the default BFF/session mode.
- [ ] Add OIDC Authorization Code + PKCE mode for standalone WASM where needed.
- [ ] Generate OpenAPI security schemes from configured authentication providers.
- [ ] Add Windows/Negotiate provider where supported.
- [ ] Evaluate LDAP/AD and SAML providers based on demand.

### Phase 6 - SQL authorization provider

- [ ] Define provider-neutral SQL schema/migrations.
- [ ] Implement users, roles, permissions, grants, denies, and security versions.
- [ ] Support transactions for authorization updates.
- [ ] Add indexes for user-role/role-permission resolution.
- [ ] Add effective-permission caching strategy.
- [ ] Add concurrency/version handling.
- [ ] Add provider integration tests on supported SQL backends.

### Phase 7 - Domino authorization provider

- [ ] Define Domino identity mapping and canonical-name rules.
- [ ] Decide which authorization concepts map to Domino groups, ACL roles, and application NSF documents.
- [ ] Support read-only Domino Directory/group resolution where appropriate.
- [ ] Support application-specific authorization data in NSF.
- [ ] Implement role/permission management where the selected Domino backend supports writes.
- [ ] Integrate ACL roles without making generic XPscript authorization dependent on Domino concepts.
- [ ] Add security-version/invalidation strategy suitable for Domino.
- [ ] Add Domino-specific tests.

### Phase 8 - Advanced authorization

- [ ] Add resource-aware `Authorization.Can/Require(policy, resource)`.
- [ ] Add tenant-aware authorization context.
- [ ] Add MFA/step-up policy requirements.
- [ ] Add immediate revocation/invalidation across distributed server instances.
- [ ] Add administration APIs/UI building blocks if needed.
- [ ] Add richer security audit reporting.

## Security requirements

- Server authorization must never trust client/WASM capability state.
- Authentication and authorization failures must fail closed.
- Session identifiers must be cryptographically random and opaque.
- Rotate session IDs during sign-in and other privilege-boundary changes.
- Do not expose provider credentials or tokens to browser code unnecessarily.
- Use Secure/HttpOnly/SameSite cookie protections appropriate to deployment.
- Preserve CSRF protection for cookie-authenticated unsafe requests.
- Validate issuer, audience, signature, expiry, and relevant claims for bearer/OIDC tokens.
- Avoid long-lived bearer/refresh tokens in browser local storage by default.
- Permission/role changes must invalidate stale effective authorization state.
- Explicit deny must not be accidentally overridden by an allow from another role/provider.
- Do not log authentication secrets or tokens.
- Keep tenant/resource boundaries server-authoritative.

## Design decisions to resolve before implementation

- Exact public names: `Request.User`, `Authorization.CurrentUser`, or both.
- Exact XPscript provider/interface implementation syntax.
- Whether permissions and policies are separate first-class concepts or permissions are implemented as simple policies.
- Case sensitivity and normalization rules for role, permission, scope, and claim names.
- Exact explicit-deny precedence when multiple providers contribute authorization data.
- How multiple authorization resolvers/providers can be composed in hybrid deployments.
- Whether `SecurityVersion` is per user only or also includes global role/policy versions.
- Session snapshot refresh frequency and distributed invalidation mechanism.
- Default SQL provider/schema and supported database engines.
- Domino mapping rules for groups versus ACL roles versus application authorization documents.
- How custom XPscript providers participate in async/network-backed resolution if the language/runtime needs asynchronous operations.
- Which claims are safe to project into WebClient/WASM.
- How policy metadata is represented in generated OpenAPI and WebUIForms metadata.

## Target architecture

```text
                  Authentication provider
             session / JWT / OIDC / API key
                           |
                           v
                    ClaimsPrincipal
                     Request.User
                           |
             +-------------+-------------+
             |                           |
             v                           v
      Identity provider          Authorization resolver/store
      external claims            roles / grants / denies
             |                           |
             +-------------+-------------+
                           |
                           v
                 AuthorizationEngine
                           |
               named policies/permissions
                           |
          +----------------+----------------+
          |                |                |
          v                v                v
       REST/server      WebUIForms       WebClient/WASM
       authoritative    authoritative    capability projection
          checks           checks         for UI only
                           |
                           v
                     Session cache
                 Memory / Redis / DB
```

The final system should let application source use the same authorization language regardless of whether identity and rights come from SQL, Domino, Entra ID/OIDC, Windows authentication, or a custom application provider.
