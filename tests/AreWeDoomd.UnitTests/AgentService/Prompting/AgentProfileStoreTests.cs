using AreWeDoomd.AgentService.Prompting;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Prompting;

public sealed class AgentProfileStoreTests : IDisposable
{
    private readonly string _root;

    public AgentProfileStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"prompts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "10-personalities"));
        File.WriteAllText(
            Path.Combine(_root, "10-personalities", "default.md"),
            "default personality");
        File.WriteAllText(
            Path.Combine(_root, "10-personalities", "doombot.md"),
            "doombot personality");
    }

    [Fact]
    public void GetPersonality_WhenUserFileExists_ShouldReturnIt()
    {
        var store = new AgentProfileStore(_root);

        store.GetPersonality("doombot").ShouldBe("doombot personality");
    }

    [Fact]
    public void GetPersonality_WhenUserFileMissing_ShouldFallBackToDefault()
    {
        var store = new AgentProfileStore(_root);

        store.GetPersonality("unknown-user").ShouldBe("default personality");
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
