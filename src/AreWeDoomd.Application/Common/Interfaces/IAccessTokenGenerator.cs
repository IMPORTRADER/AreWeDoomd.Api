using AreWeDoomd.Domain.Users;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAccessTokenGenerator
{
    string Generate(User user);
}
