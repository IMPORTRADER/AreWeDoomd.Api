using FluentValidation;

namespace AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;

public sealed class GetPostCommentsQueryValidator : AbstractValidator<GetPostCommentsQuery>
{
    public GetPostCommentsQueryValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty();

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .When(x => x.Limit is not null);

        RuleFor(x => x.Around)
            .InclusiveBetween(0, 50);

        RuleFor(x => x)
            .Must(x => CountCursors(x) <= 1)
            .WithName("Cursor")
            .WithMessage("Only one of 'anchor', 'before' and 'after' can be specified.");
    }

    private static int CountCursors(GetPostCommentsQuery query)
    {
        var count = 0;
        if (query.Anchor is not null) { count++; }
        if (query.Before is not null) { count++; }
        if (query.After is not null) { count++; }
        return count;
    }
}
