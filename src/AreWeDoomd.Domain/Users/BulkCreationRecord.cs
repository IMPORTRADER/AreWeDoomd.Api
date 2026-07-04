namespace AreWeDoomd.Domain.Users;

public sealed class BulkCreationRecord
{
    public Guid JobId { get; private set; }
    public Guid UserId { get; private set; }
    public string Username { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core constructor
    private BulkCreationRecord() { }

    public static BulkCreationRecord Create(Guid jobId, Guid userId, string username, DateTimeOffset createdAt)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("JobId cannot be empty.", nameof(jobId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        return new BulkCreationRecord
        {
            JobId = jobId,
            UserId = userId,
            Username = username,
            CreatedAt = createdAt
        };
    }
}
