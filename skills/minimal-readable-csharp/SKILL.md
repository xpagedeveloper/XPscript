# Minimal Readable C\#

## Purpose

Generate, review, and refactor C# code so that it is:

- Minimal
- Idiomatic
- Readable
- Maintainable
- Secure
- Easy to test
- Free from unnecessary abstractions
- Built with established .NET patterns
- Explicit where clarity matters
- Compact where extra structure adds no value

The goal is not to produce the fewest characters.

The goal is to produce the smallest reasonable C# implementation that remains clear, correct, secure, and maintainable.

## Core principle

Prefer the simplest idiomatic C# implementation that correctly solves the current requirement.

Do not create architecture for hypothetical future requirements.

Do not introduce abstractions unless they remove real complexity.

Prefer standard .NET functionality over custom implementations.

Prefer established C# patterns over clever code.

## Decision order

When implementing functionality, evaluate solutions in this order:

1. Can C# or the .NET Base Class Library solve it directly?
2. Can an existing project abstraction solve it cleanly?
3. Is there an established ASP.NET Core or .NET pattern for the problem?
4. Can the functionality be implemented as a small local method?
5. Is a new abstraction actually required?

Do not automatically create:

- Interfaces
- Services
- Repositories
- Managers
- Providers
- Factories
- Builders
- Wrappers
- Helpers
- Utility classes
- Handlers
- Mediators
- Base classes

unless they solve a concrete problem.

## Use modern C\#

Prefer modern C# syntax when it improves clarity.

Use where appropriate:

```csharp
var user = await userService.GetAsync(id, cancellationToken);

if (user is null)
    return Results.NotFound();

```

Prefer:

- `var` when the type is obvious
- pattern matching
- property patterns
- switch expressions
- records
- primary constructors when appropriate
- collection expressions
- target-typed `new`
- nullable reference types
- async/await
- `using` declarations
- `await using`
- expression-bodied members for simple members
- `required` properties where appropriate
- `init` setters for immutable models
- `IReadOnlyCollection<T>` when mutation is not intended
- `CancellationToken` for asynchronous I/O boundaries

Do not use newer syntax only to make code look modern.

Readability has priority.

## Prefer the .NET Base Class Library

Do not recreate functionality already provided by .NET.

Prefer:

```csharp
string.IsNullOrWhiteSpace(value)

```

instead of:

```csharp
value is null || value.Trim().Length == 0

```

Prefer:

```csharp
Guid.TryParse(value, out var id)

```

instead of exception-based parsing.

Prefer:

```csharp
DateTimeOffset.UtcNow

```

for timestamps that represent points in time.

Prefer:

```csharp
ArgumentNullException.ThrowIfNull(value);

```

instead of manual null checks when an exception is appropriate.

Prefer built-in APIs for:

- JSON
- HTTP
- URI handling
- encoding
- cryptography
- hashing
- collections
- concurrency
- validation primitives
- date and time
- file operations
- dependency injection
- logging

Do not build custom implementations unless the platform implementation is insufficient.

## Minimal implementation rule

Write only the code required for current behavior.

Avoid speculative functionality.

Do not add:

- unused interfaces
- unused configuration
- future extension points
- generic type parameters without need
- extra DTO layers without purpose
- redundant mapping
- wrapper services
- empty abstractions
- duplicate validation
- redundant exception handling
- unnecessary dependency injection
- unnecessary state

Every added type should have a clear reason to exist.

## Interfaces

Do not create an interface only because a class exists.

Avoid:

```csharp
public interface IUserService
{
    Task<User?> GetAsync(Guid id);
}

public sealed class UserService : IUserService
{
}

```

when there is only one implementation and no actual abstraction boundary is needed.

Prefer the concrete type:

```csharp
public sealed class UserService
{
}

```

Use interfaces when they provide real value, such as:

- multiple implementations
- external system boundaries
- plugin architectures
- test seams where substitution is actually needed
- domain abstractions
- infrastructure isolation

Do not create interfaces purely for mocking.

## Services

Do not create service classes that only forward calls.

Avoid:

```csharp
public sealed class UserService(UserRepository repository)
{
    public Task<User?> GetAsync(Guid id)
        => repository.GetAsync(id);
}

```

If no business logic, policy, orchestration, or abstraction exists, use the underlying component directly.

A service should represent meaningful application or domain behavior.

## Repository pattern

Do not automatically wrap Entity Framework Core in repositories.

Avoid:

```text
Controller
-> Service
-> Repository
-> DbContext

```

when each layer only forwards arguments.

Entity Framework Core already provides repository-like and unit-of-work behavior through:

```csharp
DbSet<T>
DbContext

```

Use a repository when it creates a meaningful boundary, such as:

- complex persistence behavior
- aggregate-oriented domain access
- multiple storage implementations
- persistence isolation required by the architecture

Do not introduce repositories by default.

## Entity Framework Core

Prefer direct, efficient queries.

Example:

```csharp
var user = await db.Users
    .AsNoTracking()
    .SingleOrDefaultAsync(
        x => x.Id == id,
        cancellationToken);

```

Use `AsNoTracking()` for read-only queries when change tracking is unnecessary.

Project only required fields when appropriate:

```csharp
var user = await db.Users
    .Where(x => x.Id == id)
    .Select(x => new UserDto(
        x.Id,
        x.Name,
        x.Email))
    .SingleOrDefaultAsync(cancellationToken);

```

Avoid loading full entities only to map a few fields afterward.

Avoid obvious N+1 queries.

Do not call `ToListAsync()` earlier than necessary.

Let filtering, sorting, projection, and aggregation execute in the database when appropriate.

## LINQ

Use LINQ when it improves clarity.

Prefer:

```csharp
var activeUsers = users
    .Where(x => x.IsActive)
    .ToList();

```

over a manual loop when no special control flow is needed.

Do not chain LINQ excessively when a normal loop is clearer.

Avoid:

```csharp
items
    .Where(...)
    .Select(...)
    .Where(...)
    .Select(...)
    .GroupBy(...)
    .SelectMany(...)
    .OrderBy(...)
    .ToList();

```

if the transformation becomes difficult to understand.

Do not use LINQ only to avoid writing a simple loop.

## Collections

Choose the simplest collection that matches the behavior.

Prefer:

- `List<T>` for ordered mutable collections
- `Dictionary<TKey, TValue>` for key-based lookup
- `HashSet<T>` for uniqueness and membership checks
- arrays for fixed-size data
- `IEnumerable<T>` for iteration
- `IReadOnlyCollection<T>` when count matters and mutation should not be exposed

Do not expose mutable collections unless mutation is part of the API.

Do not return `IEnumerable<T>` merely to look generic if the concrete semantics matter.

## Async code

Use async for asynchronous I/O.

Prefer:

```csharp
public async Task<User?> GetUserAsync(
    Guid id,
    CancellationToken cancellationToken)
{
    return await db.Users
        .SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken);
}

```

Avoid fake async:

```csharp
public async Task<int> GetCountAsync()
{
    return 42;
}

```

Prefer:

```csharp
public Task<int> GetCountAsync()
{
    return Task.FromResult(42);
}

```

or make the API synchronous if asynchronous behavior is unnecessary.

Do not use:

```csharp
Task.Run(...)

```

to make synchronous server-side work appear asynchronous.

Pass `CancellationToken` through I/O operations where useful.

## Avoid unnecessary async state machines

If a method only returns an existing task and does not need additional async behavior:

Avoid:

```csharp
public async Task<User?> GetAsync(Guid id)
{
    return await repository.GetAsync(id);
}

```

Prefer:

```csharp
public Task<User?> GetAsync(Guid id)
{
    return repository.GetAsync(id);
}

```

Keep `async` when it improves error handling, disposal, sequencing, or readability.

## Nullability

Enable nullable reference types.

Treat nullability annotations as part of the API contract.

Prefer:

```csharp
User?

```

when null is expected.

Do not suppress warnings using:

```csharp
!

```

unless the invariant is known and cannot be expressed better.

Avoid unnecessary null checks when the type system already guarantees non-null values.

## Guard clauses

Prefer guard clauses over nested conditions.

Prefer:

```csharp
if (user is null)
    return NotFound();

if (!user.IsActive)
    return BadRequest();

return Ok(user);

```

Avoid:

```csharp
if (user is not null)
{
    if (user.IsActive)
    {
        return Ok(user);
    }

    return BadRequest();
}

return NotFound();

```

## Pattern matching

Use pattern matching where it improves intent.

Prefer:

```csharp
if (user is { IsActive: true, EmailVerified: true })
{
    SendEmail(user);
}

```

Use switch expressions for compact value mapping:

```csharp
return status switch
{
    OrderStatus.Pending => "Pending",
    OrderStatus.Completed => "Completed",
    OrderStatus.Cancelled => "Cancelled",
    _ => "Unknown"
};

```

Do not compress complex business flows into dense pattern expressions.

## Boolean logic

Avoid explicit comparisons.

Avoid:

```csharp
if (user.IsActive == true)

```

Prefer:

```csharp
if (user.IsActive)

```

Prefer positive boolean names.

Prefer:

```csharp
IsEnabled
HasAccess
CanEdit

```

Avoid:

```csharp
IsNotDisabled
DoesNotHaveAccess
CannotNotEdit

```

## Variables

Create variables when they improve meaning.

Avoid:

```csharp
var result = user.Name;
return result;

```

Prefer:

```csharp
return user.Name;

```

But prefer a named variable when it clarifies a complex expression:

```csharp
var hasAccess =
    user.IsActive &&
    user.Role is Role.Admin or Role.Editor;

if (!hasAccess)
    return Forbid();

```

Do not optimize for line count at the cost of comprehension.

## Methods

Methods should perform one coherent operation.

Prefer small methods with meaningful names.

Do not split trivial code into many tiny methods when doing so makes execution flow harder to follow.

Avoid this:

```csharp
ValidateUser();
CheckUserStatus();
CheckPermissions();
LoadConfiguration();
ExecuteAction();

```

when the methods contain one line each and are only called once.

Keep related logic together when it improves readability.

## Extension methods

Use extension methods for reusable operations that naturally belong to the extended type.

Do not use extension methods as a dumping ground for unrelated helpers.

Avoid broad classes such as:

```csharp
Extensions
Helpers
Utils
Common

```

Prefer focused naming:

```csharp
StringExtensions
ClaimsPrincipalExtensions
DateTimeExtensions

```

only when extension methods are justified.

## Records

Prefer records for immutable data-centric types.

Example:

```csharp
public sealed record CreateUserRequest(
    string Name,
    string Email);

```

Prefer normal classes when identity, lifecycle, mutable behavior, or encapsulated domain logic matters.

Do not convert every class to a record.

## DTOs

Create DTOs when they define a real boundary.

Examples:

- HTTP requests
- HTTP responses
- messaging contracts
- integration contracts
- projections

Do not create multiple nearly identical DTO layers without a reason.

Avoid:

```text
UserRequest
-> UserRequestDto
-> UserModel
-> UserEntity

```

when each type contains the same fields.

## Mapping

Use direct mapping for simple models.

Prefer:

```csharp
var response = new UserResponse(
    user.Id,
    user.Name,
    user.Email);

```

Do not introduce a mapping library for trivial mappings.

Use mapping tools when they materially reduce repetitive mapping complexity.

## Dependency injection

Use constructor injection for actual dependencies.

Prefer:

```csharp
public sealed class UserService(
    AppDbContext db,
    ILogger<UserService> logger)
{
}

```

Do not inject dependencies that can be accessed directly through parameters or static framework APIs without loss of testability or clarity.

Avoid service locator patterns.

Avoid injecting an entire dependency container.

## Dependency lifetime

Choose DI lifetimes intentionally.

Use:

- Singleton for thread-safe application-wide services
- Scoped for request or operation scoped state, commonly EF Core DbContext
- Transient for lightweight stateless services when a new instance is appropriate

Do not store scoped services inside singletons.

## ASP.NET Core

Prefer framework-native features.

Use:

- model binding
- endpoint filters where appropriate
- middleware for cross-cutting request behavior
- built-in dependency injection
- authorization policies
- configuration options
- built-in logging
- ProblemDetails
- typed results where useful

Do not manually recreate framework behavior.

## Minimal APIs

Use Minimal APIs when they fit the application.

Example:

```csharp
app.MapGet("/users/{id:guid}", async (
    Guid id,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var user = await db.Users
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken);

    return user is null
        ? Results.NotFound()
        : Results.Ok(user);
});

```

Do not add controllers, services, handlers, and repositories around a trivial endpoint unless application complexity requires them.

## Controllers

When controllers are appropriate, keep them focused on HTTP behavior.

Avoid business logic that belongs to the application or domain layer.

Do not force every endpoint through extra architectural layers if those layers do nothing useful.

## Validation

Validate at trust boundaries.

Validate:

- HTTP input
- external messages
- file input
- configuration
- external service responses when necessary

Do not repeatedly validate the same invariant at every method call.

Use framework validation or established validation libraries when appropriate.

Do not write custom validation infrastructure without need.

## Exceptions

Use exceptions for exceptional conditions.

Do not use exceptions for normal control flow.

Prefer:

```csharp
if (!Guid.TryParse(value, out var id))
    return BadRequest();

```

instead of:

```csharp
try
{
    var id = Guid.Parse(value);
}
catch (FormatException)
{
    return BadRequest();
}

```

Do not catch exceptions only to throw them again.

Avoid:

```csharp
try
{
    await SaveAsync();
}
catch (Exception)
{
    throw;
}

```

Catch exceptions when you can:

- recover
- translate
- add meaningful context
- log at an appropriate boundary

Do not log the same exception at every layer.

## Error handling

Prefer centralized HTTP exception handling.

Use ASP.NET Core exception handling and ProblemDetails rather than repetitive try/catch blocks in every endpoint.

Keep domain errors distinct from infrastructure failures when it improves behavior.

Do not build custom result frameworks unless the application genuinely benefits from them.

## Security

Minimal code must remain secure.

Never remove required security controls for fewer lines.

Use established .NET security APIs.

Prefer:

- ASP.NET Core authentication
- ASP.NET Core authorization policies
- `PasswordHasher<TUser>` or established identity providers
- parameterized SQL
- EF Core query APIs
- `RandomNumberGenerator`
- standard cryptographic APIs
- secure cookie settings
- antiforgery protection where applicable
- output encoding
- secure configuration providers

Never implement custom cryptography.

Never concatenate untrusted input into SQL.

Avoid:

```csharp
var sql = $"SELECT * FROM Users WHERE Email = '{email}'";

```

Use parameters or EF Core.

## Secrets

Do not hardcode:

- passwords
- API keys
- connection strings with credentials
- private keys
- tokens

Use appropriate secret and configuration mechanisms.

Do not log sensitive values.

## Logging

Use structured logging.

Prefer:

```csharp
logger.LogInformation(
    "User {UserId} logged in",
    user.Id);

```

Avoid:

```csharp
logger.LogInformation(
    $"User {user.Id} logged in");

```

Do not log every method entry and exit without a concrete operational need.

Log events that help diagnose behavior.

## Configuration

Use the options pattern when configuration forms a coherent configuration object.

Do not create options classes for a single local value when direct configuration access is simpler and sufficiently clear.

Validate important configuration at startup where possible.

## HTTP clients

Use `IHttpClientFactory` or established framework patterns.

Do not instantiate and dispose `HttpClient` for each request.

Prefer typed clients when they provide useful cohesion.

Do not wrap `HttpClient` multiple times without adding value.

## Serialization

Prefer `System.Text.Json`.

Do not introduce another JSON library unless required by missing functionality or compatibility constraints.

Use explicit serialization configuration where contracts require it.

## Time

Prefer `DateTimeOffset` for timestamps representing actual points in time.

Prefer UTC internally for distributed systems.

Use `TimeProvider` when code needs controllable time for testing.

Avoid creating unnecessary custom clock abstractions when `TimeProvider` solves the requirement.

## IDs

Use strongly appropriate identifiers.

Use `Guid` where globally unique identifiers are appropriate.

Do not convert identifiers repeatedly between strings and typed forms.

Parse at the boundary, then use the typed value internally.

## Domain modeling

Use domain objects when they enforce meaningful business invariants or behavior.

Do not create domain entities that are only DTOs with private setters.

Do not use Domain-Driven Design patterns unless the problem has enough domain complexity to justify them.

## SOLID

Use SOLID as guidance.

Do not treat SOLID as a requirement to maximize:

- interfaces
- classes
- dependency injection
- inheritance
- patterns

The objective is low coupling and clear responsibilities.

If a SOLID-inspired abstraction creates more complexity than it removes, prefer the simpler implementation.

## Composition over inheritance

Prefer composition when behavior needs to be combined.

Avoid deep inheritance hierarchies.

Do not create base classes only to share a few utility methods.

Use inheritance when the relationship is genuinely polymorphic and stable.

## YAGNI

Apply You Aren't Gonna Need It.

Do not add capabilities because:

- another database may be used later
- another message broker may be added
- multiple implementations may exist later
- the endpoint may become generic
- another authentication provider may be needed

Implement those abstractions when the requirement exists.

## KISS

Apply Keep It Simple.

Prefer:

- fewer types
- fewer layers
- fewer dependencies
- fewer transformations
- fewer configuration points
- obvious control flow
- framework-native functionality

Do not mistake architectural complexity for maintainability.

## DRY

Avoid duplicated business rules.

Do not extract code simply because two blocks look similar.

A small amount of local duplication can be better than a generic abstraction that couples unrelated behavior.

Extract when code represents the same concept and should change together.

## Performance

Do not optimize without a reason.

But avoid obvious performance problems.

Watch for:

- N+1 database queries
- repeated enumeration
- unnecessary allocations
- unnecessary `ToList()`
- repeated serialization
- unnecessary string creation
- blocking asynchronous calls
- synchronous I/O
- loading entire database entities unnecessarily
- repeated external service calls

Avoid:

```csharp
task.Result
task.Wait()

```

in asynchronous code.

Use async all the way when the operation is asynchronous.

## Memory

Do not introduce advanced memory optimizations without evidence.

Use:

- `Span<T>`
- `Memory<T>`
- pooling
- `ArrayPool<T>`
- stack allocation

only when profiling or workload characteristics justify them.

Do not sacrifice maintainability for theoretical allocation reductions.

## Comments

Do not comment obvious code.

Avoid:

```csharp
// Check if user is null
if (user is null)

```

Comments should explain:

- why a non-obvious decision exists
- external constraints
- security constraints
- performance tradeoffs
- workarounds
- surprising platform behavior

Prefer expressive code over explanatory comments.

## Naming

Use standard .NET naming conventions.

Use PascalCase for:

- types
- methods
- properties
- public members

Use camelCase for:

- parameters
- local variables

Use `_camelCase` only when consistent with the project's private field convention.

Avoid vague names such as:

```text
Manager
Processor
Helper
Utility
Thing
Data
Object
Temp
Value

```

when a domain-specific name is possible.

Prefer:

```text
InvoiceCalculator
TokenValidator
CustomerRepository
RefreshToken
FailedLoginCount

```

when those concepts actually exist.

## Do not over-name

Short local names are acceptable when the scope makes the meaning obvious.

Example:

```csharp
users.Where(x => x.IsActive)

```

Do not force:

```csharp
users.Where(currentUserFromCollection => currentUserFromCollection.IsActive)

```

Clarity depends on scope.

## File structure

Do not create a new file for every tiny type automatically.

Group code according to project conventions and cohesion.

Keep public types discoverable.

Avoid giant files containing unrelated behavior.

Do not create folders only to satisfy architectural terminology.

## Tests

When tests are requested, test behavior rather than implementation details.

Prefer focused tests.

Avoid excessive mocks.

Use real pure objects where practical.

Do not mock:

- simple value objects
- collections
- LINQ
- basic framework functionality

Mock or fake actual boundaries when necessary.

Do not add tests when the user requested only an implementation unless tests are essential to demonstrate correctness.

## Refactoring existing C\#

When reviewing existing code:

1. Preserve behavior.
2. Remove dead code.
3. Remove redundant abstractions.
4. Remove unnecessary interfaces.
5. Remove forwarding services.
6. Simplify LINQ.
7. Simplify conditionals.
8. Replace custom implementations with .NET APIs.
9. Remove unnecessary allocations.
10. Remove duplicate mappings.
11. Reduce needless async/await.
12. Improve nullability.
13. Improve naming.
14. Preserve security controls.
15. Preserve required domain boundaries.

Do not refactor solely to reduce line count.

## Complexity budget

Every new type or abstraction adds cognitive cost.

Before creating one, ask:

- What problem does this solve?
- Does this reduce complexity?
- Does it remove duplication of behavior?
- Does it represent a real domain concept?
- Does it provide an actual boundary?
- Does it make testing meaningfully easier?
- Is it required now?
- Is it easier to understand than direct code?

If not, keep the implementation simpler.

## Code generation behavior

When generating C# code:

Return the smallest complete implementation that solves the request.

Do not add unrelated:

- interfaces
- repositories
- services
- DTO layers
- factories
- base classes
- configuration
- middleware
- abstractions
- dependencies
- comments
- logging
- tests

unless they are required for correctness, security, maintainability, or explicitly requested.

Prefer code that fits naturally into a normal modern .NET project.

## Code review behavior

When reviewing C# code, actively look for:

- unnecessary interfaces
- unnecessary services
- repository wrappers around EF Core
- excessive dependency injection
- redundant DTOs
- redundant mappings
- unnecessary async/await
- sync-over-async
- repeated enumeration
- unnecessary allocations
- excessive LINQ
- unnecessary exceptions
- redundant try/catch blocks
- unnecessary null checks
- custom implementations of BCL features
- speculative generic abstractions
- inheritance that should be composition
- premature design patterns
- duplicate business logic

For each issue, prefer a concrete simpler implementation.

## Final check

Before returning C# code, verify:

- Is every class necessary?
- Is every interface necessary?
- Is every method necessary?
- Is every DTO necessary?
- Is every dependency necessary?
- Is every architectural layer necessary?
- Can the BCL solve this?
- Can ASP.NET Core solve this?
- Can EF Core solve this directly?
- Can modern C# syntax make this clearer?
- Is async actually required?
- Are cancellation tokens propagated where useful?
- Are nullable contracts correct?
- Is the code secure?
- Is there speculative functionality?
- Can code be removed?
- Can another C# developer understand the implementation quickly?

If code can be removed without reducing correctness, security, clarity, or required behavior, remove it.

## Primary rule

Write production-grade C# with the least necessary complexity.

Use established .NET functionality first.

Add abstractions only when they earn their existence.
