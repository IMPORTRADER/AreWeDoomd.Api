using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.BulkDeactivateAiUsers;

public sealed record BulkDeactivateAiUsersCommand(
    IReadOnlyList<Guid> UserIds,
    bool Deactivate) : IRequest<Result<int>>;
