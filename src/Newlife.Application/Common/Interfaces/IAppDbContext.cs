using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Newlife.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);

    bool IsSerializationConflict(Exception exception);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
