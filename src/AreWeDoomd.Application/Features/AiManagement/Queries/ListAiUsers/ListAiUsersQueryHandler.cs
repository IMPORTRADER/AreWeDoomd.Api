using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.ListAiUsers;

public sealed class ListAiUsersQueryHandler(IAiUserReadRepository repository)
    : IRequestHandler<ListAiUsersQuery, Result<AiUserListResult>>
{
    public async Task<Result<AiUserListResult>> Handle(ListAiUsersQuery request, CancellationToken cancellationToken)
    {
        int offset = Math.Max(0, request.Offset);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await repository.ListAsync(
            request.Trait, request.Search, request.Status, offset, pageSize, cancellationToken);

        bool hasMore = offset + items.Count < totalCount;

        return Result<AiUserListResult>.Success(new AiUserListResult(items, totalCount, hasMore));
    }
}
