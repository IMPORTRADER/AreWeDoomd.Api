# Logging Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full structured logging to AreWeDoomd.Api using Serilog — writing to SQL Server and console, with automatic MediatR pipeline behavior coverage and HTTP request logging.

**Architecture:** Serilog replaces the default Microsoft logging provider. A `LoggingPipelineBehavior<TRequest, TResponse>` in the Application layer automatically wraps every MediatR handler. Sink configuration (SQL Server + Console) lives in the Infrastructure layer via an `AddInfrastructureSinks` extension method, keeping the API project clean.

**Tech Stack:** Serilog 3.x, Serilog.AspNetCore 8.x, Serilog.Sinks.MSSqlServer 8.x, Serilog.Sinks.Console 6.x, MediatR 12.2.0, xUnit + Moq + Shouldly (tests)

---

## File Map

| Action | File |
|---|---|
| Create | `src/AreWeDoomd.Application/Common/Attributes/SensitivePropertiesAttribute.cs` |
| Create | `src/AreWeDoomd.Application/Common/Behaviors/LoggingPipelineBehavior.cs` |
| Create | `src/AreWeDoomd.Infrastructure/Common/Logging/SerilogConfiguration.cs` |
| Create | `tests/AreWeDoomd.UnitTests/Common/Behaviors/LoggingPipelineBehaviorTests.cs` |
| Modify | `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj` |
| Modify | `src/AreWeDoomd.Application/AreWeDoomd.Application.csproj` |
| Modify | `src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj` |
| Modify | `src/AreWeDoomd.Application/DependencyInjection.cs` |
| Modify | `src/AreWeDoomd.Api/Program.cs` |
| Modify | `src/AreWeDoomd.Api/appsettings.json` |
| Modify | `src/AreWeDoomd.Application/Features/Authentication/Commands/RegisterUser/RegisterUserCommand.cs` |
| Modify | `src/AreWeDoomd.Application/Features/Authentication/Commands/LoginUser/LoginUserCommand.cs` |
| Modify | `src/AreWeDoomd.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommand.cs` |
| Modify | `src/AreWeDoomd.Application/Features/Users/Commands/ChangePassword/ChangePasswordCommand.cs` |

---

## Task 1: Add NuGet Packages

**Files:**
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`
- Modify: `src/AreWeDoomd.Application/AreWeDoomd.Application.csproj`
- Modify: `src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj`

- [ ] **Step 1: Add Serilog.AspNetCore to the API project**

Run from repo root:
```powershell
dotnet add src/AreWeDoomd.Api/AreWeDoomd.Api.csproj package Serilog.AspNetCore
```
Expected: output ending with `PackageReference ... added`

- [ ] **Step 2: Add Microsoft.Extensions.Logging.Abstractions to the Application project**

```powershell
dotnet add src/AreWeDoomd.Application/AreWeDoomd.Application.csproj package Microsoft.Extensions.Logging.Abstractions
```
Expected: output ending with `PackageReference ... added`

- [ ] **Step 3: Add Serilog sinks to the Infrastructure project**

```powershell
dotnet add src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj package Serilog.Sinks.MSSqlServer
dotnet add src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj package Serilog.Sinks.Console
```
Expected: both lines end with `PackageReference ... added`

- [ ] **Step 4: Restore and verify build**

```powershell
dotnet build AreWeDoomd.sln
```
Expected: `Build succeeded` with 0 errors

- [ ] **Step 5: Commit**

```powershell
git add src/AreWeDoomd.Api/AreWeDoomd.Api.csproj
git add src/AreWeDoomd.Application/AreWeDoomd.Application.csproj
git add src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj
git commit -m "feat: add Serilog NuGet packages for logging infrastructure"
```

---

## Task 2: Create SensitivePropertiesAttribute and Mark Sensitive Commands

**Files:**
- Create: `src/AreWeDoomd.Application/Common/Attributes/SensitivePropertiesAttribute.cs`
- Modify: `src/AreWeDoomd.Application/Features/Authentication/Commands/RegisterUser/RegisterUserCommand.cs`
- Modify: `src/AreWeDoomd.Application/Features/Authentication/Commands/LoginUser/LoginUserCommand.cs`
- Modify: `src/AreWeDoomd.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommand.cs`
- Modify: `src/AreWeDoomd.Application/Features/Users/Commands/ChangePassword/ChangePasswordCommand.cs`

- [ ] **Step 1: Create the attribute**

Create `src/AreWeDoomd.Application/Common/Attributes/SensitivePropertiesAttribute.cs`:

```csharp
namespace AreWeDoomd.Application.Common.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SensitivePropertiesAttribute : Attribute { }
```

- [ ] **Step 2: Mark RegisterUserCommand**

Replace the full contents of `src/AreWeDoomd.Application/Features/Authentication/Commands/RegisterUser/RegisterUserCommand.cs`:

```csharp
using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.RegisterUser;

[SensitiveProperties]
public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string Password,
    UserType UserType) : IRequest<Result<AuthResult>>;
```

- [ ] **Step 3: Mark LoginUserCommand**

Replace the full contents of `src/AreWeDoomd.Application/Features/Authentication/Commands/LoginUser/LoginUserCommand.cs`:

```csharp
using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.LoginUser;

[SensitiveProperties]
public sealed record LoginUserCommand(string Username, string Password) : IRequest<Result<AuthResult>>;
```

- [ ] **Step 4: Mark ResetPasswordCommand**

Replace the full contents of `src/AreWeDoomd.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommand.cs`:

```csharp
using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Features.Authentication.Common;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Authentication.Commands.ResetPassword;

[SensitiveProperties]
public sealed record ResetPasswordCommand(string Username, string Code, string NewPassword) : IRequest<Result<AuthResult>>;
```

- [ ] **Step 5: Mark ChangePasswordCommand**

Replace the full contents of `src/AreWeDoomd.Application/Features/Users/Commands/ChangePassword/ChangePasswordCommand.cs`:

```csharp
using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Commands.ChangePassword;

[SensitiveProperties]
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result<ChangePasswordResult>>;
```

- [ ] **Step 6: Verify build still passes**

```powershell
dotnet build AreWeDoomd.sln
```
Expected: `Build succeeded` with 0 errors

- [ ] **Step 7: Commit**

```powershell
git add src/AreWeDoomd.Application/Common/Attributes/SensitivePropertiesAttribute.cs
git add src/AreWeDoomd.Application/Features/Authentication/Commands/RegisterUser/RegisterUserCommand.cs
git add src/AreWeDoomd.Application/Features/Authentication/Commands/LoginUser/LoginUserCommand.cs
git add src/AreWeDoomd.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommand.cs
git add src/AreWeDoomd.Application/Features/Users/Commands/ChangePassword/ChangePasswordCommand.cs
git commit -m "feat: add SensitivePropertiesAttribute and mark password commands"
```

---

## Task 3: Implement LoggingPipelineBehavior (TDD)

**Files:**
- Create: `tests/AreWeDoomd.UnitTests/Common/Behaviors/LoggingPipelineBehaviorTests.cs`
- Create: `src/AreWeDoomd.Application/Common/Behaviors/LoggingPipelineBehavior.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/AreWeDoomd.UnitTests/Common/Behaviors/LoggingPipelineBehaviorTests.cs`:

```csharp
using AreWeDoomd.Application.Common.Attributes;
using AreWeDoomd.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Common.Behaviors;

public sealed class LoggingPipelineBehaviorTests
{
    private sealed record TestRequest(string Value) : IRequest<string>;

    [SensitiveProperties]
    private sealed record SensitiveRequest(string Password) : IRequest<string>;

    private static Mock<ILogger<LoggingPipelineBehavior<TRequest, string>>> BuildLogger<TRequest>()
        where TRequest : notnull
        => new();

    [Fact]
    public async Task Handle_ReturnsResultFromNextDelegate()
    {
        var logger = BuildLogger<TestRequest>();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger.Object);
        var request = new TestRequest("hello");

        var result = await behavior.Handle(request, () => Task.FromResult("world"), CancellationToken.None);

        result.ShouldBe("world");
    }

    [Fact]
    public async Task Handle_LogsExecutingAndExecuted_OnSuccess()
    {
        var logger = BuildLogger<TestRequest>();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger.Object);

        await behavior.Handle(new TestRequest("x"), () => Task.FromResult("ok"), CancellationToken.None);

        VerifyLogContains(logger, LogLevel.Information, "Executing", Times.Once());
        VerifyLogContains(logger, LogLevel.Information, "Executed", Times.Once());
    }

    [Fact]
    public async Task Handle_RedactsRequest_WhenSensitivePropertiesAttributePresent()
    {
        var logger = BuildLogger<SensitiveRequest>();
        var behavior = new LoggingPipelineBehavior<SensitiveRequest, string>(logger.Object);

        await behavior.Handle(new SensitiveRequest("secret123"), () => Task.FromResult("ok"), CancellationToken.None);

        VerifyLogContains(logger, LogLevel.Information, "[REDACTED]", Times.Once());
    }

    [Fact]
    public async Task Handle_LogsErrorAndRethrows_WhenNextThrows()
    {
        var logger = BuildLogger<TestRequest>();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger.Object);
        var exception = new InvalidOperationException("boom");

        var act = async () => await behavior.Handle(
            new TestRequest("x"),
            () => throw exception,
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidOperationException>();
        VerifyLogContains(logger, LogLevel.Error, "Failed", Times.Once());
    }

    private static void VerifyLogContains<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel level,
        string containsText,
        Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(containsText)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail (class not found)**

```powershell
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~LoggingPipelineBehaviorTests"
```
Expected: build error — `LoggingPipelineBehavior` does not exist yet

- [ ] **Step 3: Implement LoggingPipelineBehavior**

Create `src/AreWeDoomd.Application/Common/Behaviors/LoggingPipelineBehavior.cs`:

```csharp
using System.Diagnostics;
using System.Reflection;
using AreWeDoomd.Application.Common.Attributes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.Application.Common.Behaviors;

public sealed class LoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var isSensitive = typeof(TRequest).GetCustomAttribute<SensitivePropertiesAttribute>() is not null;

        if (isSensitive)
            logger.LogInformation("Executing {RequestName} [REDACTED]", requestName);
        else
            logger.LogInformation("Executing {RequestName} {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await next();
            stopwatch.Stop();
            logger.LogInformation("Executed {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed {RequestName} after {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```powershell
dotnet test tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj --filter "FullyQualifiedName~LoggingPipelineBehaviorTests"
```
Expected:
```
Passed!  - Failed: 0, Passed: 4, Skipped: 0
```

- [ ] **Step 5: Run the full test suite to confirm no regressions**

```powershell
dotnet test AreWeDoomd.sln
```
Expected: `Passed!` with 0 failures

- [ ] **Step 6: Commit**

```powershell
git add tests/AreWeDoomd.UnitTests/Common/Behaviors/LoggingPipelineBehaviorTests.cs
git add src/AreWeDoomd.Application/Common/Behaviors/LoggingPipelineBehavior.cs
git commit -m "feat: implement LoggingPipelineBehavior with sensitive data redaction"
```

---

## Task 4: Register LoggingPipelineBehavior in Application DI

**Files:**
- Modify: `src/AreWeDoomd.Application/DependencyInjection.cs`

- [ ] **Step 1: Register the behavior in ApplicationServiceRegistration**

Replace the full contents of `src/AreWeDoomd.Application/DependencyInjection.cs`:

```csharp
using System.Reflection;
using AreWeDoomd.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AreWeDoomd.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
```

Note: `LoggingPipelineBehavior` is registered before `ValidationBehavior` so logging wraps validation — a failed validation is logged as a completed (not failed) execution at the outer level.

- [ ] **Step 2: Build to verify**

```powershell
dotnet build AreWeDoomd.sln
```
Expected: `Build succeeded` with 0 errors

- [ ] **Step 3: Run all tests**

```powershell
dotnet test AreWeDoomd.sln
```
Expected: `Passed!` with 0 failures

- [ ] **Step 4: Commit**

```powershell
git add src/AreWeDoomd.Application/DependencyInjection.cs
git commit -m "feat: register LoggingPipelineBehavior in MediatR pipeline"
```

---

## Task 5: Configure Serilog Sinks in Infrastructure

**Files:**
- Create: `src/AreWeDoomd.Infrastructure/Common/Logging/SerilogConfiguration.cs`

- [ ] **Step 1: Create SerilogConfiguration.cs with AddInfrastructureSinks**

Create `src/AreWeDoomd.Infrastructure/Common/Logging/SerilogConfiguration.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Sinks.MSSqlServer;

namespace AreWeDoomd.Infrastructure.Common.Logging;

public static class SerilogConfiguration
{
    public static LoggerConfiguration AddInfrastructureSinks(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AreWeDoomdSql");

        var columnOptions = new ColumnOptions();
        columnOptions.Store.Remove(StandardColumn.Properties);
        columnOptions.Store.Add(StandardColumn.LogEvent);
        columnOptions.AdditionalColumns = new Collection<SqlColumn>
        {
            new() { ColumnName = "SourceContext", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true },
            new() { ColumnName = "RequestPath", DataType = SqlDbType.NVarChar, DataLength = 2048, AllowNull = true },
            new() { ColumnName = "UserId", DataType = SqlDbType.NVarChar, DataLength = 50, AllowNull = true },
            new() { ColumnName = "ElapsedMs", DataType = SqlDbType.BigInt, AllowNull = true },
        };

        return loggerConfiguration
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "Logs",
                    AutoCreateSqlTable = true,
                },
                columnOptions: columnOptions);
    }
}
```

- [ ] **Step 2: Build Infrastructure project**

```powershell
dotnet build src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj
```
Expected: `Build succeeded` with 0 errors

- [ ] **Step 3: Commit**

```powershell
git add src/AreWeDoomd.Infrastructure/Common/Logging/SerilogConfiguration.cs
git commit -m "feat: add Serilog SQL Server and console sink configuration"
```

---

## Task 6: Bootstrap Serilog in Program.cs and Update appsettings.json

**Files:**
- Modify: `src/AreWeDoomd.Api/Program.cs`
- Modify: `src/AreWeDoomd.Api/appsettings.json`

- [ ] **Step 1: Replace appsettings.json**

Replace the full contents of `src/AreWeDoomd.Api/appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    }
  },
  "PasswordReset": {
    "CodeLength": 6,
    "LifetimeMinutes": 15
  },
  "Jwt": {
    "Issuer": "AreWeDoomd.Api",
    "Audience": "AreWeDoomd.Client",
    "Key": "dev-signing-key-change-this-before-production-12345",
    "AccessTokenExpirationMinutes": 60
  },
  "Smtp": {
    "Host": "",
    "Port": 587,
    "EnableSsl": true,
    "Username": "",
    "Password": "",
    "From": ""
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 2: Replace Program.cs**

Replace the full contents of `src/AreWeDoomd.Api/Program.cs`:

```csharp
using AreWeDoomd.Api.Common.Errors;
using AreWeDoomd.Application;
using AreWeDoomd.Infrastructure;
using AreWeDoomd.Infrastructure.Common.Logging;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, svc, logConfig) => logConfig
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(svc)
    .Enrich.FromLogContext()
    .AddInfrastructureSinks(ctx.Configuration));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "AreWeDoomd? API";
    });
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

Note: `UseSerilogRequestLogging()` is placed after `UseExceptionHandler()` and before `UseHttpsRedirection()` so that request logs capture the final status code including exception-handled responses.

- [ ] **Step 3: Build the full solution**

```powershell
dotnet build AreWeDoomd.sln
```
Expected: `Build succeeded` with 0 errors

- [ ] **Step 4: Run all tests**

```powershell
dotnet test AreWeDoomd.sln
```
Expected: `Passed!` with 0 failures

- [ ] **Step 5: Commit**

```powershell
git add src/AreWeDoomd.Api/Program.cs
git add src/AreWeDoomd.Api/appsettings.json
git commit -m "feat: bootstrap Serilog with HTTP request logging and SQL Server sink"
```

---

## Self-Review Notes

- **Spec coverage check:**
  - SQL Server + Console sinks: Task 5 ✓
  - Custom columns (SourceContext, RequestPath, UserId, ElapsedMs): Task 5 ✓
  - MediatR pipeline behavior: Task 3 ✓
  - Sensitive data redaction: Task 2 + Task 3 ✓
  - HTTP request logging via `UseSerilogRequestLogging()`: Task 6 ✓
  - `Serilog` config section in appsettings.json: Task 6 ✓
  - Framework log suppression (Microsoft/EF/System at Warning): Task 6 ✓
  - Behavior registered before ValidationBehavior: Task 4 ✓

- **Type consistency:** `SensitivePropertiesAttribute` defined in Task 2, used in Task 3 behavior and applied to commands in Task 2. `AddInfrastructureSinks` defined in Task 5, consumed in Task 6 Program.cs. All consistent.

- **No placeholders:** All steps contain full, exact code. No TBDs.
