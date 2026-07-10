using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.ListAiUsers;

public sealed record ListAiUsersQuery(string? Trait, string? Search, string? Status, int Offset, int PageSize)
    : IRequest<Result<AiUserListResult>>;
