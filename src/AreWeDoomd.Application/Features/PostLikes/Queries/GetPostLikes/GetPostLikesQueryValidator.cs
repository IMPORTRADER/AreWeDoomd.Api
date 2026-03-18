using FluentValidation;

namespace AreWeDoomd.Application.Features.PostLikes.Queries.GetPostLikes;

public sealed class GetPostLikesQueryValidator : AbstractValidator<GetPostLikesQuery>
{
    public GetPostLikesQueryValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();
    }
}
