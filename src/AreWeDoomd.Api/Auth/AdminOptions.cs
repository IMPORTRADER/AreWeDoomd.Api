namespace AreWeDoomd.Api.Auth;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";
    public List<string> Usernames { get; set; } = [];
}
