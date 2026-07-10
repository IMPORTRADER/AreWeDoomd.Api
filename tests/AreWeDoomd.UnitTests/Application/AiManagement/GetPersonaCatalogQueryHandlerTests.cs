using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetPersonaCatalog;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetPersonaCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_Always_ReturnsCatalogFromIPersonaCatalog()
    {
        var data = new PersonaCatalogData(
            Archetypes:
            [
                new PersonaArchetype(
                    "doomer", "Doomer", "desc",
                    ["doom_{noun}{nn}"], ["pessimistic", "sarcastic", "dry"],
                    ["style one"], ["summary one"]),
            ],
            TraitCategories: [new PersonaTraitCategory("Tone", ["dry"])],
            TypingStyleSuggestions: ["short sentences"],
            UsernameWordPools: new Dictionary<string, IReadOnlyList<string>>
            {
                ["noun"] = ["ember"],
            });

        var catalog = new Mock<IPersonaCatalog>();
        catalog.Setup(c => c.Get()).Returns(data);

        var handler = new GetPersonaCatalogQueryHandler(catalog.Object);
        var result = await handler.Handle(new GetPersonaCatalogQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(data);
    }
}
