using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Search.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Search.Queries.SearchUsers;

public sealed class SearchUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<SearchUsersQuery, Result<IReadOnlyList<SearchUserResult>>>
{
    public async Task<Result<IReadOnlyList<SearchUserResult>>> Handle(
        SearchUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await userRepository.SearchByQueryAsync(request.Query, cancellationToken);

        return Result<IReadOnlyList<SearchUserResult>>.Success(users);
    }
}
