namespace AreWeDoomd.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to save all changes. Returns false — instead of throwing — when the save
    /// fails due to a unique-index violation or an optimistic-concurrency conflict
    /// (DbUpdateException / DbUpdateConcurrencyException). All other exceptions propagate normally.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}

