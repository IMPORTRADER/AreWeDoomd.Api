using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Commands.StartBulkCreateAiUsers;

public sealed record StartBulkCreateAiUsersCommand(int Count) : IRequest<Result<Guid>>;
