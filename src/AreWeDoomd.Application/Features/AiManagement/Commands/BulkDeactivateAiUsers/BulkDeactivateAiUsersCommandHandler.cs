using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.BulkDeactivateAiUsers;

public sealed class BulkDeactivateAiUsersCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<BulkDeactivateAiUsersCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        BulkDeactivateAiUsersCommand request, CancellationToken cancellationToken)
    {
        var distinctIds = request.UserIds.Distinct().ToList();
        var users = await userRepository.GetByIdsAsync(distinctIds, cancellationToken);

        if (users.Count != distinctIds.Count)
        {
            return Result<int>.NotFound("BulkDeactivate.UserNotFound", "One or more users were not found.");
        }

        if (users.Any(u => u.UserType != UserType.Ai))
        {
            return Result<int>.Failure("BulkDeactivate.NonAiUser", "Only AI users can be deactivated.");
        }

        var now = dateTimeProvider.UtcNow;
        foreach (var user in users)
        {
            if (request.Deactivate)
            {
                user.Deactivate(now);
            }
            else
            {
                user.Reactivate(now);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(users.Count);
    }
}
