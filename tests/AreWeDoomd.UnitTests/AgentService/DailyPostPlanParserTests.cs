using AreWeDoomd.AgentService.Decisions;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService;

public sealed class DailyPostPlanParserTests
{
    private readonly DailyPostPlanParser _parser = new();

    [Fact]
    public void ParseScores_WhenValidJson_ShouldReturnScores()
    {
        var id = Guid.NewGuid();
        string json = $$"""{"accounts":[{"runItemId":"{{id}}","reasoning":"sakin gün","desireScore":42,"hypotheticalPostCount":2}]}""";

        var result = _parser.ParseScores(json, new HashSet<Guid> { id });

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].DesireScore.ShouldBe(42);
    }

    [Fact]
    public void ParseScores_WhenUnknownRunItemId_ShouldDropThatEntry()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        string json = $"{{\"accounts\":[" +
            $"{{\"runItemId\":\"{known}\",\"reasoning\":\"r\",\"desireScore\":50,\"hypotheticalPostCount\":1}}," +
            $"{{\"runItemId\":\"{unknown}\",\"reasoning\":\"halucinasyon\",\"desireScore\":90,\"hypotheticalPostCount\":3}}" +
            $"]}}";

        var result = _parser.ParseScores(json, new HashSet<Guid> { known });

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].RunItemId.ShouldBe(known);
    }

    [Fact]
    public void ParseScores_WhenMarkdownFenced_ShouldStripAndParse()
    {
        var id = Guid.NewGuid();
        string json = $"```json\n{{\"accounts\":[{{\"runItemId\":\"{id}\",\"reasoning\":\"r\",\"desireScore\":10,\"hypotheticalPostCount\":1}}]}}\n```";

        _parser.ParseScores(json, new HashSet<Guid> { id }).ShouldNotBeNull();
    }

    [Fact]
    public void ParseScores_WhenMalformed_ShouldReturnNull()
    {
        _parser.ParseScores("{truncated", new HashSet<Guid>()).ShouldBeNull();
    }

    [Fact]
    public void ParseCompose_WhenValid_ShouldReturnPosts()
    {
        string json = """{"posts":[{"content":"selam","scheduledTimeUtc":"2026-07-05T15:00:00Z"}]}""";
        var plan = _parser.ParseCompose(json);
        plan.ShouldNotBeNull();
        plan.Posts.Count.ShouldBe(1);
        plan.Posts[0].Content.ShouldBe("selam");
    }

    [Fact]
    public void ParseCompose_WhenUnparseableDate_ShouldReturnNull()
    {
        _parser.ParseCompose("""{"posts":[{"content":"x","scheduledTimeUtc":"yarin ogle"}]}""").ShouldBeNull();
    }
}
