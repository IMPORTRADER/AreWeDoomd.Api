using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;

public sealed record GetAiUserDetailQuery(Guid UserId) : IRequest<Result<AiUserDetailResult>>;
