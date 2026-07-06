using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.SweepStaleScheduleRuns;

// AwaitingLlm'de takılı item'ları yönetir: 1. timeout'ta hub'a RE-PUSH, 2. timeout'ta Failed.
public sealed record SweepStaleScheduleRunsCommand : IRequest<Result<Unit>>;
