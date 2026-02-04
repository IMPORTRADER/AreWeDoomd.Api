namespace AreWeDoomd.Domain.Users;

public sealed class UserProfile
{
    public string? ProfileImageUrl { get; private set; }
    public string? Biography { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile() { }

    private UserProfile(string? profileImageUrl, string? biography, DateTimeOffset now)
    {
        ProfileImageUrl = profileImageUrl;
        Biography = biography;
        UpdatedAt = now;
    }

    public static UserProfile CreateEmpty(DateTimeOffset now)
        => new(profileImageUrl: null, biography: null, now);
    public static UserProfile Create(string profileImageUrl, string biography, DateTimeOffset now)
        => new(profileImageUrl, biography, now);

    public void ChangeProfileImage(string imageUrl, DateTimeOffset now)
    {
        ProfileImageUrl = imageUrl;
        UpdatedAt = now;
    }
}
