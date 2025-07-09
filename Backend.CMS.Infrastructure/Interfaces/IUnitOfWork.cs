using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Unit of Work pattern implementation for managing transactions and repository coordination
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // Repository properties
        IUserRepository Users { get; }
        IPageRepository Pages { get; }
        IFileRepository Files { get; }
        IFolderRepository Folders { get; }
        ICategoryRepository Categories { get; }
        IProductRepository Products { get; }
        IProductVariantRepository ProductVariants { get; }
        ILocationRepository Locations { get; }
        ICompanyRepository Companies { get; }

        // Transaction management
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

        // Save changes
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);

        // Bulk operations
        Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;
        Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;
        Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;

        // Performance operations
        Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters);
        Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken, params object[] parameters);

        // State management
        void DetachAllEntities();
        Task ReloadEntityAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;

        // Audit operations
        Task<List<string>> GetPendingAuditChangesAsync();
        Task ClearChangeTrackerAsync();
    }
}