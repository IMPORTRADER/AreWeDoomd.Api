using FluentValidation;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserPosts;

public sealed class GetUserPostsQueryValidator : AbstractValidator<GetUserPostsQuery>
{
    public GetUserPostsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
