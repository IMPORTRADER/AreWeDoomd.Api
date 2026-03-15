## Purpose

The goal of this document is to ensure unit tests are consistent, reliable,
maintainable, and aligned with idiomatic .NET testing practices.

Primary goals:

- ensure deterministic and reliable unit tests
- keep tests focused on the smallest meaningful unit
- isolate external dependencies through mocking
- enforce consistent naming and structure
- follow .NET and Microsoft testing best practices when in doubt

------------------------------------------------------------------------

# Scope

Load this guide only when the task involves writing or modifying unit tests.

If the task does not involve unit tests, do not load this document.

------------------------------------------------------------------------

# Testing Stack

Use the following libraries for unit tests:

- xUnit
- Moq
- Shouldly

Do not introduce alternative testing libraries unless explicitly required.

------------------------------------------------------------------------

# Unit Test Naming Rules

Use the following naming convention for test methods:

`MethodName_StateUnderTest_ExpectedBehaviour`

Examples:

```csharp
CreateUser_WhenEmailIsInvalid_ShouldThrowValidationException
GetById_WhenUserExists_ShouldReturnUserDto
Delete_WhenEntityDoesNotExist_ShouldReturnFalse