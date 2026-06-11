using System.Collections.Concurrent;

namespace AreWeDoomd.AgentService.Prompting;

public sealed class AgentProfileStore
{
    private readonly string _personalitiesRoot;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AgentProfileStore(string? promptsRoot = null)
    {
        string root = promptsRoot ?? Path.Combine(AppContext.BaseDirectory, "Prompts");
        _personalitiesRoot = Path.Combine(root, "10-personalities");
    }

    public string GetPersonality(string username)
    {
        return _cache.GetOrAdd(username, Load);
    }

    private string Load(string username)
    {
        string specific = Path.Combine(_personalitiesRoot, $"{username}.md");
        string fallback = Path.Combine(_personalitiesRoot, "default.md");
        return File.ReadAllText(File.Exists(specific) ? specific : fallback);
    }
}
