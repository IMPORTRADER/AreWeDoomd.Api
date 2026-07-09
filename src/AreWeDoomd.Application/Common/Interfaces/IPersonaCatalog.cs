using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

/// <summary>
/// Curated, code-maintained catalog of AI persona archetypes, traits, and
/// typing-style suggestions. Single source of truth for both bulk persona
/// generation and the admin UI's guided create form.
/// </summary>
public interface IPersonaCatalog
{
    PersonaCatalogData Get();
}
