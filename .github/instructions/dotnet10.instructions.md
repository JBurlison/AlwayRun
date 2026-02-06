---
name: C# .NET 10.0 Standards
description: Coding standards and conventions for C# .NET 10.0 development
applyTo: "**/*.cs"
---

# C# .NET 10.0 Development Standards

These standards apply to all C# code in this workspace.

## Language Features

### Use Modern C# Features
- **File-scoped namespaces**: One namespace per file, no braces
- **Primary constructors**: For dependency injection in classes
- **Collection expressions**: Use `[item1, item2]` syntax
- **Records**: For DTOs, value objects, and immutable data
- **Pattern matching**: Prefer over type checks and casts
- **Nullable reference types**: Always enabled, handle nulls explicitly

### Code Examples

```csharp
// ✅ Correct: File-scoped namespace
namespace MyProject.Services;

// ✅ Correct: Primary constructor for DI
public class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    // ✅ Correct: Async with CancellationToken
    public async Task<User?> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogInformation("Getting user {UserId}", id);
        return await repository.FindByIdAsync(id, ct);
    }
}

// ✅ Correct: Record for DTO
public record UserDto(Guid Id, string Name, string Email);

// ✅ Correct: Result pattern
public readonly record struct Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}
```

## Async/Await

- All I/O operations must be async
- Always accept `CancellationToken` parameter (default to `default`)
- Use `ConfigureAwait(false)` in library code
- Prefer `ValueTask` for hot paths that often complete synchronously
- Use `IAsyncEnumerable<T>` for streaming results

## Error Handling

- Use Result pattern for expected failures
- Throw exceptions only for unexpected/exceptional conditions
- Always log exceptions with structured logging
- Include correlation IDs in error responses

## Dependency Injection

- Use constructor injection via primary constructors
- Register services with appropriate lifetime (Scoped for request-bound, Singleton for stateless)
- Use `IOptions<T>` pattern for configuration
- Avoid service locator anti-pattern

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `UserService` |
| Interfaces | IPascalCase | `IUserRepository` |
| Methods | PascalCase | `GetUserAsync` |
| Async methods | PascalCaseAsync | `SaveChangesAsync` |
| Properties | PascalCase | `FirstName` |
| Private fields | _camelCase | `_repository` |
| Parameters | camelCase | `userId` |
| Constants | PascalCase | `MaxRetryCount` |

## Testing Standards

- Use xUnit for unit tests
- Use FluentAssertions for assertions
- Use NSubstitute or Moq for mocking
- Follow Arrange-Act-Assert pattern
- Name tests: `MethodName_WhenCondition_ShouldExpectedBehavior`

```csharp
[Fact]
public async Task GetUserAsync_WhenUserExists_ShouldReturnUser()
{
    // Arrange
    var repository = Substitute.For<IUserRepository>();
    repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns(new User { Id = _userId, Name = "Test" });
    var sut = new UserService(repository, _logger);

    // Act
    var result = await sut.GetUserAsync(_userId);

    // Assert
    result.Should().NotBeNull();
    result!.Name.Should().Be("Test");
}
```

## Project Structure

```
src/
├── Domain/           # Entities, value objects, domain events
├── Application/      # Use cases, commands, queries, DTOs
├── Infrastructure/   # Database, external services, implementations
└── Api/              # Controllers, endpoints, middleware

tests/
├── Domain.Tests/
├── Application.Tests/
├── Infrastructure.Tests/
└── Api.Tests/
```

## API Design (Minimal APIs)

```csharp
// ✅ Correct: Minimal API pattern
app.MapGet("/users/{id:guid}", async (Guid id, IUserService service, CancellationToken ct) =>
{
    var result = await service.GetUserAsync(id, ct);
    return result is not null 
        ? Results.Ok(result) 
        : Results.NotFound();
})
.WithName("GetUser")
.WithOpenApi();
```

## Entity Framework Core

- Use migrations for schema changes
- Configure entities with Fluent API in separate configuration classes
- Use `AsNoTracking()` for read-only queries
- Avoid lazy loading
- Use explicit includes for related data

## Security

- Never log sensitive data (passwords, tokens, PII)
- Use parameterized queries (EF Core does this automatically)
- Validate all input at API boundary
- Use HTTPS everywhere
- Store secrets in Azure Key Vault or similar, never in code
