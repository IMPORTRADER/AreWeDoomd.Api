namespace AreWeDoomd.Application.Common.Interfaces;

public interface ISessionLogReader
{
    Task<string?> ReadAsync(string sessionRef, CancellationToken ct);
}
