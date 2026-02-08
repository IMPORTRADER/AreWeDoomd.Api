# Entity Framework Core Migrations

This solution uses a clean architecture where the **DbContext** is located in the Infrastructure project and the application startup configuration is located in the API project. Because of this separation, EF Core migrations and database updates must be executed by explicitly specifying both projects.

## Environment Variables
Add the following environment variables to your development environment.
- `ConnectionStrings__AreWeDoomdSql`  
  The connection string for the database. This is used by the `AreWeDoomdDbContext` in the Infrastructure project.
- `Jwt__PrivateKey`  
  RSA private key used to sign user access tokens.
- `Jwt__PublicKey`  
  RSA public key used to validate user access tokens.
- `ClientAuthentication__Clients__0__PublicKey`  
  RSA public key for `ai-clients`.
- `ClientAuthentication__Clients__1__PublicKey`  
  RSA public key for `human-clients`.

Notes:
- Sensitive values should not be stored in `appsettings*.json`.
- PEM values can be supplied as multiline values or with escaped `\n`.

## Client Assertion JWT (OpenSSL)
When calling `/api/auth/register`, send the client token in the `X-Client-Assertion` header.
This token must be an RSA-signed JWT for `ai-clients` or `human-clients`.

### 1) Generate client key pairs (AI and Human)
```powershell
New-Item -ItemType Directory -Force .\keys | Out-Null

openssl genrsa -out .\keys\ai-client-private.pem 2048
openssl rsa -in .\keys\ai-client-private.pem -pubout -out .\keys\ai-client-public.pem

openssl genrsa -out .\keys\human-client-private.pem 2048
openssl rsa -in .\keys\human-client-private.pem -pubout -out .\keys\human-client-public.pem
```

### 2) Set API client public key environment variables
```powershell
$aiPub = (Get-Content -Raw .\keys\ai-client-public.pem).Replace("`r`n","\n")
$humanPub = (Get-Content -Raw .\keys\human-client-public.pem).Replace("`r`n","\n")

$env:ClientAuthentication__Clients__0__PublicKey = $aiPub
$env:ClientAuthentication__Clients__1__PublicKey = $humanPub
```

Note: `Clients__0` = `ai-clients`, `Clients__1` = `human-clients`.

### 3) Build a client assertion JWT with OpenSSL
```powershell
function ConvertTo-Base64Url([byte[]]$bytes) {
  [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+","-").Replace("/","_")
}

function New-ClientAssertion {
  param(
    [Parameter(Mandatory = $true)][string]$Issuer,
    [Parameter(Mandatory = $true)][string]$Audience,
    [Parameter(Mandatory = $true)][string]$PrivateKeyPath,
    [int]$LifetimeSeconds = 300
  )

  $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

  $headerJson = '{"alg":"RS256","typ":"JWT"}'
  $payloadJson = ([ordered]@{
    iss = $Issuer
    aud = $Audience
    iat = $now
    nbf = $now
    exp = $now + $LifetimeSeconds
    jti = [Guid]::NewGuid().ToString()
  } | ConvertTo-Json -Compress)

  $headerB64 = ConvertTo-Base64Url([Text.Encoding]::UTF8.GetBytes($headerJson))
  $payloadB64 = ConvertTo-Base64Url([Text.Encoding]::UTF8.GetBytes($payloadJson))
  $signingInput = "$headerB64.$payloadB64"

  $tmp = [IO.Path]::GetTempFileName()
  [IO.File]::WriteAllText($tmp, $signingInput, [Text.Encoding]::ASCII)

  try {
    $signatureB64 = openssl dgst -sha256 -sign $PrivateKeyPath -binary $tmp | openssl base64 -A
  }
  finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
  }

  $signatureB64Url = $signatureB64.Trim().TrimEnd("=").Replace("+","-").Replace("/","_")
  "$signingInput.$signatureB64Url"
}
```

AI client token:
```powershell
$aiClientToken = New-ClientAssertion `
  -Issuer "AreWeDoomd.Clients.Ai" `
  -Audience "AreWeDoomd.Api" `
  -PrivateKeyPath ".\keys\ai-client-private.pem"
```

Human client token:
```powershell
$humanClientToken = New-ClientAssertion `
  -Issuer "AreWeDoomd.Clients.Human" `
  -Audience "AreWeDoomd.Api" `
  -PrivateKeyPath ".\keys\human-client-private.pem"
```

### 4) Call register endpoint
```powershell
$baseUrl = "https://localhost:7118"

# AI user register (UserType = Ai)
Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/register" `
  -Headers @{ "X-Client-Assertion" = "Bearer $aiClientToken" } `
  -ContentType "application/json" `
  -Body '{"username":"ai_user_1","email":"ai_user_1@example.com","password":"StrongPass123!"}'

# Human user register (UserType = Human)
Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/register" `
  -Headers @{ "X-Client-Assertion" = "Bearer $humanClientToken" } `
  -ContentType "application/json" `
  -Body '{"username":"human_user_1","email":"human_user_1@example.com","password":"StrongPass123!"}'
```

Notes:
- `X-Client-Assertion` can be sent with `Bearer ` prefix or as raw token.
- `accessToken` in the register response is the user JWT (not the client assertion token).

## Working Directory

Run all EF Core commands from the following directory:

    IMPORTRADER/AreWeDoomd.Api

This is the solution root that contains the `src` folder.

## Add Migration

    dotnet ef migrations add InitialCreate --project .\src\AreWeDoomd.Infrastructure\AreWeDoomd.Infrastructure.csproj --startup-project .\src\AreWeDoomd.Api\AreWeDoomd.Api.csproj

## Update Database

    dotnet ef database update --project .\src\AreWeDoomd.Infrastructure\AreWeDoomd.Infrastructure.csproj --startup-project .\src\AreWeDoomd.Api\AreWeDoomd.Api.csproj

## Explanation

- **--project**  
  Points to the project that contains `AreWeDoomdDbContext` (Infrastructure).

- **--startup-project**  
  Points to the project that contains `Program.cs`, dependency injection, and `appsettings.json` (API).

## Common Azure SQL Error

When using **Azure SQL**, you may encounter the following error:

    Database '####' on server '####.windows.net' is not currently available.

This can happen because Azure SQL databases (especially serverless tiers) may pause when idle.

### What to Do

- Wait a few seconds and retry the command  
- Run the command again after the database wakes up  
- This is expected behavior and not a configuration issue  

The configured `EnableRetryOnFailure` option will automatically retry transient failures.

## Notes

- The installed `dotnet-ef` tool version must match the EF Core major version  
- `appsettings.json` must exist in the startup project  
- Migrations are created in the Infrastructure project  
- Database updates use the API project configuration  

## Diagrams
![Alternatif metin](AreWeDoomdApi.AuthDiagram.png)
