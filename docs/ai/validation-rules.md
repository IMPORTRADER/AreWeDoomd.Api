# Validation Rules

FluentValidation must be implemented in the **Application layer**, not in controllers.

Rules:

- Validators must target **Commands or Queries**
- Validation must run through a **MediatR pipeline behavior**
- Controllers must not perform manual validation
- Controllers must not instantiate validators directly
- Validation errors must be returned through centralized exception handling
- FluentValidation is for **input validation only**, not domain/business rules