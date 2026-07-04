using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAiAccountFactory
{
    /// <summary>
    /// Normalizes, checks uniqueness, creates the User (UserType.Ai) and stages it via AddAsync.
    /// Does NOT SaveChanges — the caller owns the transaction. Password arrives pre-generated, is hashed here.
    /// </summary>
    Task<Result<User>> CreateAiAccountAsync(
        string username, string email, string password, DateTimeOffset now, CancellationToken ct);
}
