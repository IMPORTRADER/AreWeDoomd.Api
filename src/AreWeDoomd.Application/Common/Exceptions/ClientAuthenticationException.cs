namespace AreWeDoomd.Application.Common.Exceptions;

public class ClientAuthenticationException : InvalidOperationException
{
    public ClientAuthenticationException(string message) : base(message)
    {
    }
}

