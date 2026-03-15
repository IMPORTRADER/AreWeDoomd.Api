# Endpoint Rules

These rules define how API endpoints must be implemented in this
project.

The project uses **.NET 10**, **CQRS**, and **MediatR**.

Controllers are responsible only for handling HTTP concerns. All
application logic must live in the application layer.

------------------------------------------------------------------------

## 1. REST Conventions

Endpoints must follow REST principles.

Use resource-based routes and proper HTTP verbs.

Examples:

GET /api/v1/users\
GET /api/v1/users/{id}\
POST /api/v1/users\
PUT /api/v1/users/{id}\
DELETE /api/v1/users/{id}

Avoid RPC-style endpoints such as:

POST /api/v1/getUsers

------------------------------------------------------------------------

## 2. Thin Controllers

Controllers **must not contain business logic**.

Controller responsibilities:

-   Receive HTTP requests
-   Bind request DTOs
-   Send commands/queries through MediatR
-   Return HTTP responses

Controllers must not:

-   Implement domain rules
-   Access repositories
-   Contain complex logic
-   Perform data persistence

All application behavior must be handled by **CQRS handlers**.

------------------------------------------------------------------------

## 3. CQRS + MediatR Usage

The API layer communicates with the application layer through
**MediatR**.

Rules:

-   Controllers send **Commands** for write operations.
-   Controllers send **Queries** for read operations.
-   Controllers must never call services or repositories directly.

Example flow:

Controller → MediatR → Command/Query Handler → Domain/Application Logic

------------------------------------------------------------------------

## 4. Controller Structure

Rules:

-   One controller per resource.
-   Controllers must stay small and focused.
-   Avoid large controllers (\>200 lines).

Naming:

UsersController\
OrdersController

Routes:

/api/v1/{resource}

------------------------------------------------------------------------

## 5. Request DTOs

Endpoints must use **request models** instead of domain entities.

Rules:

-   Never expose domain entities in API contracts.
-   Request models represent input contracts only.
-   Keep DTOs small and explicit.

Naming examples:

CreateUserRequest\
UpdateUserRequest

------------------------------------------------------------------------

## 6. Response DTOs

Endpoints must return **response models**, not domain entities.

Rules:

-   Never return database entities.
-   Map application/domain objects to response DTOs.

Naming examples:

UserResponse\
OrderResponse

------------------------------------------------------------------------

## 7. Validation

Input validation must occur before application logic executes.

Preferred approaches:

-   FluentValidation
-   DataAnnotations

Controllers should not contain manual validation logic.

------------------------------------------------------------------------

## 8. Status Codes

Use consistent HTTP status codes.

200 OK --- Successful read\
201 Created --- Resource created\
204 No Content --- Successful operation without body\
400 Bad Request --- Invalid request\
404 Not Found --- Resource not found\
409 Conflict --- Conflict error\
500 Internal Server Error --- Unexpected error

------------------------------------------------------------------------

## 9. Error Handling

Controllers must not implement custom try/catch error handling.

Use centralized exception handling middleware.

Errors should return standardized **ProblemDetails** responses.

------------------------------------------------------------------------

## 10. Authorization

Use ASP.NET authorization attributes.

Examples:

\[Authorize\]\
\[Authorize(Roles = "Admin")\]\
\[AllowAnonymous\]

Authorization rules should not be implemented manually inside
controllers.
