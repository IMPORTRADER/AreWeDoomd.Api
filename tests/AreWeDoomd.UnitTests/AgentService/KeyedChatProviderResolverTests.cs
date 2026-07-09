using AreWeDoomd.AgentService;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService;

public class KeyedChatProviderResolverTests
{
    private static (KeyedChatProviderResolver Resolver, IChatProvider Gemini, IChatProvider OpenRouter) Build()
    {
        var gemini = Mock.Of<IChatProvider>(p => p.Name == "gemini");
        var openRouter = Mock.Of<IChatProvider>(p => p.Name == "openrouter");
        var services = new ServiceCollection();
        services.AddKeyedSingleton("gemini", gemini);
        services.AddKeyedSingleton("openrouter", openRouter);
        var provider = services.BuildServiceProvider();
        var options = Options.Create(new AgentServiceOptions { ChatProvider = "gemini" });
        var resolver = new KeyedChatProviderResolver(
            provider, options, NullLogger<KeyedChatProviderResolver>.Instance);
        return (resolver, gemini, openRouter);
    }

    [Fact]
    public void Resolve_WhenNameRegistered_ShouldReturnNamedProvider()
    {
        var (resolver, _, openRouter) = Build();
        resolver.Resolve("openrouter").ShouldBeSameAs(openRouter);
    }

    [Fact]
    public void Resolve_WhenNameEmpty_ShouldReturnEnvFallback()
    {
        var (resolver, gemini, _) = Build();
        resolver.Resolve("").ShouldBeSameAs(gemini);
    }

    [Fact]
    public void Resolve_WhenNameUnknown_ShouldReturnEnvFallback()
    {
        var (resolver, gemini, _) = Build();
        resolver.Resolve("banana").ShouldBeSameAs(gemini);
    }
}
