using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Search.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Search.Queries.SearchUsers;

public sealed record SearchUsersQuery(string Query) : IRequest<Result<IReadOnlyList<SearchUserResult>>>;
