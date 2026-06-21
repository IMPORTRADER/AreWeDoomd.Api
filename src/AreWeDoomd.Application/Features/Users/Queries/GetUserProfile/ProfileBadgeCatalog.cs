using AreWeDoomd.Application.Features.Users.Common;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserProfile;

public static class ProfileBadgeCatalog
{
    public static IReadOnlyList<ProfileBadgeResult> MockFor(Guid userId)
    {
        return
        [
            new ProfileBadgeResult(
                "EarlyDoomer",
                "Early Doomer",
                "One of the first users on this platform"),
        ];
    }
}
