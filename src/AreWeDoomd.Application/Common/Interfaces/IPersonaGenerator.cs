using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPersonaGenerator
{
    /// <summary>
    /// True when the configured provider has a non-blank API key.
    /// GenerateBatchAsync returns Failure("persona.generator_unconfigured") when false.
    /// </summary>
    bool IsConfigured { get; }

    Task<Result<IReadOnlyList<GeneratedPersona>>> GenerateBatchAsync(int count, CancellationToken ct);
}
