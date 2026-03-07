namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string value);
    bool Verify(string hash, string candidate);
}

