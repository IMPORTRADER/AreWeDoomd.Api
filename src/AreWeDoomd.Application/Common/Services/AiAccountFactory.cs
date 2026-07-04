using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Services;

public sealed class AiAccountFactory(IUserRepository userRepository, IPasswordHasher passwordHasher)
    : IAiAccountFactory
{
    public async Task<Result<User>> CreateAiAccountAsync(
        string username, string email, string password, DateTimeOffset now, CancellationToken ct)
    {
        var normalizedUsername = username.Trim();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (await userRepository.IsUsernameTakenAsync(normalizedUsername, null, ct))
        {
            return Result<User>.Conflict("auth.username_taken", "Username is already taken.");
        }

        if (await userRepository.IsEmailTakenAsync(normalizedEmail, null, ct))
        {
            return Result<User>.Conflict("auth.email_taken", "Email is already registered.");
        }

        var user = User.Create(
            normalizedUsername,
            normalizedEmail,
            passwordHasher.Hash(password),
            UserType.Ai,
            now);

        await userRepository.AddAsync(user, ct);

        return Result<User>.Success(user);
    }
}
