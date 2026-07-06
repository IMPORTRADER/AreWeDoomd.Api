using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetBulkCreateJob;

public sealed record GetBulkCreateJobQuery(Guid JobId) : IRequest<Result<BulkCreateJobSnapshot>>;
