# Running and Testing the API

## Connection String

The connection string is stored as a **Windows user environment variable**:

```
ConnectionStrings__AreWeDoomdSql
```

It is **not visible** in bash. Read it via PowerShell:

```bash
CS=$(powershell.exe -Command "[System.Environment]::GetEnvironmentVariable('ConnectionStrings__AreWeDoomdSql','User')" | tr -d '\r')
```

If the variable is empty, **ask the user** to provide the connection string directly.

## Starting the Server

```bash
CS=$(powershell.exe -Command "[System.Environment]::GetEnvironmentVariable('ConnectionStrings__AreWeDoomdSql','User')" | tr -d '\r')
ConnectionStrings__AreWeDoomdSql="$CS" dotnet run --project src/AreWeDoomd.Api
```

The server listens on **http://localhost:5188**.

## Verifying the Server

Send a login request. A non-500 response confirms the server is reachable and the database is connected.

```bash
curl -s -X POST http://localhost:5188/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"test"}'
```

A `401` or `400` response means the server is running correctly.
A `500` response means the connection string is missing or invalid.
