using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.LlmSettings.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.LlmSettings.Queries.GetLlmSettings;

public sealed record GetLlmSettingsQuery : IRequest<Result<LlmSettingsResult>>;
