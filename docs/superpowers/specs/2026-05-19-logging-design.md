# Logging Infrastructure Design

**Date:** 2026-05-19  
**Project:** AreWeDoomd.Api  
**Status:** Approved

---

## Overview

Add a full structured logging infrastructure to the AreWeDoomd.Api (.NET 10 ASP.NET Core) project using **Serilog**. Logs are written to the existing SQL Server database and to the console. Every HTTP request, MediatR command/query, and error is automatically captured with structured context.

---

## Architecture & Dependencies

Three NuGet packages are added:

| Package | Project |
|---|---|
| `Serilog.AspNetCore` | `AreWeDoomd.Api` |
| `Serilog.Sinks.MSSqlServer` | `AreWeDoomd.Infrastructure` |
| `Serilog.Sinks.Console` | `AreWeDoomd.Infrastructure` |

Serilog is bootstrapped in `Program.cs` via `UseSerilog()`, replacing the default Microsoft logging provider entirely. The SQL Server connection string is read from existing `appsettings.json`. A `Serilog` config section controls minimum log levels.

Sink registration lives in `InfrastructureServiceRegistration` so the API project has no direct dependency on SQL Server sink specifics.

---

## Logs Table Schema

The `Serilog.Sinks.MSSqlServer` sink auto-creates a `Logs` table in the existing database.

**Standard columns:**

| Column | Type | Purpose |
|---|---|---|
| `Id` | INT IDENTITY | Primary key |
| `Message` | NVARCHAR(MAX) | Rendered log message |
| `MessageTemplate` | NVARCHAR(MAX) | Raw Serilog template |
| `Level` | NVARCHAR(128) | Verbose/Debug/Information/Warning/Error/Fatal |
| `TimeStamp` | DATETIME | When the log was written |
| `Exception` | NVARCHAR(MAX) | Full exception + stack trace (nullable) |
| `Properties` | NVARCHAR(MAX) | JSON blob of all structured properties |

**Custom extracted columns:**

| Column | Source |
|---|---|
| `SourceContext` | Class/handler that emitted the log |
| `RequestPath` | HTTP path (from request logging middleware) |
| `UserId` | Pulled from JWT claims when available |
| `ElapsedMs` | Request or command duration in milliseconds |

---

## MediatR Pipeline Behavior

A `LoggingPipelineBehavior<TRequest, TResponse>` is added to the **Application layer** (`AreWeDoomd.Application`).

**Behavior:**
- **Before execution** — logs `Executing {RequestName}` with the full request as a structured property
- **After success** — logs `Executed {RequestName}` with elapsed milliseconds
- **After exception** — logs `Failed {RequestName}` at Error level with the exception attached

**Sensitive data protection:**
- A `[SensitiveProperties]` attribute marks request properties that must not be logged (passwords, reset codes)
- The behavior inspects this attribute before serializing the request body

**Registration:**
Registered in `ApplicationServiceRegistration` alongside the existing MediatR registration. Applies automatically to all handlers with zero changes to handler code.

---

## HTTP Request Logging & Serilog Configuration

`UseSerilogRequestLogging()` middleware is added in `Program.cs`. It emits one structured line per HTTP request: method, path, status code, elapsed milliseconds.

**`appsettings.json` Serilog section:**

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System": "Warning"
    }
  }
}
```

This suppresses noisy EF Core and ASP.NET Core framework logs while keeping application logs at `Information` and above. Error/Fatal always pass through.

---

## What Gets Logged Automatically

| Event | Level | Source |
|---|---|---|
| Every HTTP request (method, path, status, ms) | Information | Serilog request middleware |
| Every MediatR command/query start | Information | LoggingPipelineBehavior |
| Every MediatR command/query completion + duration | Information | LoggingPipelineBehavior |
| Any unhandled exception in a handler | Error | LoggingPipelineBehavior |
| Application startup | Information | Program.cs |
| Email send attempts | Information | SmtpEmailSender (existing) |

---

## Out of Scope

- External log aggregation (Seq, Elastic, Datadog) — can be added later as a Serilog sink
- Log retention/cleanup policies — handled at the DB level separately
- Distributed tracing (OpenTelemetry) — separate concern
