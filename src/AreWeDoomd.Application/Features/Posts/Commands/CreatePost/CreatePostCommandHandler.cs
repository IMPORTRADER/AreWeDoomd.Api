using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using AreWeDoomd.Domain.Posts;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Commands.CreatePost;

public sealed class CreatePostCommandHandler(
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePostCommand, Result<PostResult>>
{
    public async Task<Result<PostResult>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;

        var post = Post.Create(request.UserId, request.Content, now);

        await postRepository.AddAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var result = await postRepository.GetByIdProjectedAsync(post.Id, cancellationToken);

        return Result<PostResult>.Success(result!);
    }
}
