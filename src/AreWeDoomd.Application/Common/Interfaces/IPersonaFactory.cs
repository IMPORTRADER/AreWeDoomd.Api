using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

/// <summary>
/// Composes a random AI persona from the curated persona catalog.
/// Replaces the former LLM-backed persona generation.
/// </summary>
public interface IPersonaFactory
{
    GeneratedPersona CreateRandom();
}
