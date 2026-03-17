# Error Handling Rules

Known failures must not be modeled with exceptions.

Use explicit result models for expected outcomes such as validation failures, not found cases, conflicts, and business rule failures.

Exceptions must be reserved for unexpected or truly exceptional situations such as infrastructure failures, misconfiguration, and programming errors.

Rules:

- Validation failures must return a known failure model, not throw exceptions
- Handlers should return result objects for expected failure paths
- API endpoints must map known failure results to HTTP responses explicitly
- Global exception handling must be used only for unexpected failures
- Do not use exceptions for control flow
- Do not use exceptions for business outcomes that the system already knows how to represent
- Domain guard clauses may still throw when protecting invariants from invalid construction or illegal state transitions

Guidance:

- Prefer a single shared result model for commands and queries
- Represent validation errors as structured data
- Represent not found and conflict cases as explicit result types or error codes
- Keep error mapping consistent across endpoints
