using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetPersonaCatalog;

public sealed record GetPersonaCatalogQuery() : IRequest<Result<PersonaCatalogData>>;
