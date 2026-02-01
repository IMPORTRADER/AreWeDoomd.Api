namespace AreWeDoomd.Domain.Users;

public sealed class UserProfile
{
    public Guid UserId { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public string? Biography { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile() { }

    public UserProfile(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        UserId = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeProfileImage(string imageUrl, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("ImageUrl cannot be empty.", nameof(imageUrl));
        }

        ProfileImageUrl = imageUrl;
        UpdatedAt = now;
    }
}
