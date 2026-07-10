namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record BulkDeactivateRequest(IReadOnlyList<Guid> UserIds, bool Deactivate);
