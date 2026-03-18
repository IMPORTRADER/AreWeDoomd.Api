# AGENTS.md

## Purpose
Ensure the AI agent operates with minimal context, follows architectural boundaries,
and adheres to repository conventions.

Primary goals:

- minimize context consumption
- prevent irrelevant rule loading
- maintain strict architectural boundaries
- ensure consistent code quality
- enforce repository conventions

---

# Context Loading Rules

The agent must not automatically load all documentation.

Load additional guides **only when the task requires them**.

Guide | When to Load
----- | ------------
/docs/ai/unit-test-rules.md | unit test creation or modification
/docs/ai/endpoint-rules.md | editing API controllers or endpoint contracts
/docs/ai/error-handling-rules.md | modifying exception handling, result models, endpoint error responses, or MediatR request failure flow
/docs/ai/validation-rules.md | input validation or FluentValidation usage
/docs/ai/architecture-layer-details.md | when modifying or creating layer-specific logic
/docs/ai/clean-code-guidelines.md | when refactoring or improving code quality
/docs/ai/running-and-testing-api.md | running the API, testing endpoints, or making HTTP requests

---

# Architecture

The project follows **Clean Architecture** with strict layer separation.

Layers:

API  
Application  
Domain  
Infrastructure

Dependency direction must always follow:

API → Application → Domain  
Infrastructure → Application / Domain

The **Domain layer must remain completely independent**.

---

# CQRS

The application uses **CQRS with MediatR**.

Rules:

- commands modify state
- queries read data
- commands must not return domain entities
- queries must not mutate state

Handlers must contain only the logic required for the use case.

---

# Domain Model Rules

The project follows **Always Valid Domain Model** principles.

Requirements:

- entities enforce their invariants internally
- invalid state must be impossible to construct
- validation belongs in the domain when it protects invariants
- guard clauses should be used when necessary

Domain objects must never exist in an invalid state.

---

# Coding Standards

## One Class Per File

A `.cs` file must contain **exactly one class**.

Multiple classes in the same file are not allowed.

Example:

User.cs  
UserService.cs  
CreateUserCommand.cs

Each class must have its own file.

---

# Brace Rules

Braces must always be used for control flow statements, even for single-line statements.

Incorrect:

```csharp
if (condition)
    DoSomething();
```

Correct:

```csharp
if (condition)
{
    DoSomething();
}
```

This applies to:

- if
- else
- for
- foreach
- while
- switch
- any control flow statement

---

# Naming Conventions

Type | Convention
---- | ----------
Classes | PascalCase
Methods | PascalCase
Properties | PascalCase
Parameters | camelCase
Private fields | _camelCase
Interfaces | I prefix

Names must be descriptive and intention revealing.

---

# Repository Behavior Rules

The agent must **not scan the entire repository by default**.

Instead:

1. Identify the task scope
2. Load only relevant files
3. Load additional documentation only if necessary

Avoid unnecessary context expansion.

---

# When in Doubt

If a task conflicts with these rules:

1. Preserve architectural boundaries
2. Maintain domain validity
3. Follow idiomatic .NET practices
4. Prefer simpler solutions
