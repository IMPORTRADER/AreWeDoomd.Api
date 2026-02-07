using System.Diagnostics.CodeAnalysis;

namespace AreWeDoomd.Domain.Users;

public sealed class PasswordResetRequest
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string CodeHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private PasswordResetRequest() { }

    private PasswordResetRequest(Guid id, Guid userId, string codeHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("CodeHash is required.", nameof(codeHash));
        }

        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static PasswordResetRequest Create(Guid userId, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be positive.");
        }

        return new PasswordResetRequest(Guid.NewGuid(), userId, codeHash, now, now.Add(lifetime));
    }

    public bool IsActive(DateTimeOffset now)
    {
        return !IsRevoked && ConsumedAt is null && now <= ExpiresAt;
    }

    public bool CanBeConsumed(DateTimeOffset now) => IsActive(now);

    public void Consume(DateTimeOffset now)
    {
        if (!CanBeConsumed(now))
        {
            throw new InvalidOperationException("Reset request cannot be consumed.");
        }

        ConsumedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return;
        }

        IsRevoked = true;
        ConsumedAt ??= now;
    }
}

