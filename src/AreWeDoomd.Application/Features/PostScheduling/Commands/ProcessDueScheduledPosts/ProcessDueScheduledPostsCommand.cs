using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.ProcessDueScheduledPosts;

// Publisher hosted service'in (Task 9) her tick çağırdığı komut.
// Dönüş: bir sonraki Pending gönderinin UTC zamanı (adaptif uyku için) — yoksa null.
public sealed record ProcessDueScheduledPostsCommand : IRequest<Result<DateTimeOffset?>>;
