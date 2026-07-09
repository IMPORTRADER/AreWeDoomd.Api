using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetPersonaCatalog;

public sealed class GetPersonaCatalogQueryHandler(IPersonaCatalog catalog)
    : IRequestHandler<GetPersonaCatalogQuery, Result<PersonaCatalogData>>
{
    public Task<Result<PersonaCatalogData>> Handle(
        GetPersonaCatalogQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<PersonaCatalogData>.Success(catalog.Get()));
    }
}
