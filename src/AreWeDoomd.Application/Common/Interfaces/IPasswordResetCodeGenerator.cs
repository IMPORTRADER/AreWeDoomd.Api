namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPasswordResetCodeGenerator
{
    string Generate(int digits);
}

