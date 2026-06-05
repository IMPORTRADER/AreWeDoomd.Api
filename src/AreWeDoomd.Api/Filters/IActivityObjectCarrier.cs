using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.Api.Filters;

public interface IActivityObjectCarrier
{
    string ActivityObjectId { get; }
    ActivityObjectType ActivityObjectType { get; }
    string? ActivityObjectTextPreview => null;
}
