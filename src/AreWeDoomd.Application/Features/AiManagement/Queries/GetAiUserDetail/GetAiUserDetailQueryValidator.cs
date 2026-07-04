using FluentValidation;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiUserDetail;

public sealed class GetAiUserDetailQueryValidator : AbstractValidator<GetAiUserDetailQuery>
{
    public GetAiUserDetailQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
    }
}
