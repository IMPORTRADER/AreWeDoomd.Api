using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Api.Contracts.Comments;
using AreWeDoomd.Api.Contracts.Common;
using AreWeDoomd.Api.Filters;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Contracts;

public sealed class CommentResponseTests
{
    [Fact]
    public void CommentResponse_ExposesActivityObjectId()
    {
        var id = Guid.NewGuid();
        IActivityObjectCarrier carrier = BuildResponse(id: id);

        carrier.ActivityObjectId.ShouldBe(id.ToString());
    }

    [Fact]
    public void CommentResponse_ExposesActivityObjectType_AsComment()
    {
        IActivityObjectCarrier carrier = BuildResponse();

        carrier.ActivityObjectType.ShouldBe(ActivityObjectType.Comment);
    }

    [Fact]
    public void CommentResponse_ExposesActivityObjectTextPreview_AsContent()
    {
        IActivityObjectCarrier carrier = BuildResponse(content: "merhaba dünya");

        carrier.ActivityObjectTextPreview.ShouldBe("merhaba dünya");
    }

    private static CommentResponse BuildResponse(Guid? id = null, string content = "test")
        => new(
            Id: id ?? Guid.NewGuid(),
            PostId: Guid.NewGuid(),
            Author: new PostAuthor(Guid.NewGuid(), "ali", "Human", null),
            Content: content,
            LikeCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);
}
