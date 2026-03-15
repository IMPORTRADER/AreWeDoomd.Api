# Architecture Layer Responsibilities

## Domain

Contains:

- entities
- value objects
- domain logic
- domain invariants

Rules:

- no infrastructure dependencies
- no framework dependencies
- domain model must follow Always Valid Domain Model principles

---

## Application

Contains:

- CQRS commands and queries
- MediatR handlers
- application services
- use cases
- interfaces

Rules:

- may reference Abstractions NuGet packages
- must not reference infrastructure packages
- must not contain infrastructure implementation details

---

## Infrastructure

Contains:

- database access
- external service integrations
- file storage
- message brokers
- implementations of application interfaces

Infrastructure may depend on:

Application  
Domain

but not the other way around.

---

## API

Contains:

- controllers
- endpoint contracts
- request/response DTOs
- HTTP configuration

API must remain **thin** and delegate work to the Application layer.