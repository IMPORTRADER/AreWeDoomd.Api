using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserSuggestions;

public sealed record GetUserSuggestionsQuery(
    Guid? ViewerId,
    int Offset,
    int PageSize) : IRequest<Result<UserSummaryPageResult>>;
