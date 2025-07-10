using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        ICategoryRepository Categories { get; }
        ICompanyRepository Companies { get; }
        ILocationRepository Locations { get; }
        IPageRepository Pages { get; }
        IProductRepository Products { get; }
        IProductVariantRepository ProductVariants { get; }
        IFileRepository Files { get; }
        IFolderRepository Folders { get; }
        IAddressRepository Addresses { get; }
        IContactDetailsRepository ContactDetails { get; }

        IRepository<T> GetRepository<T>() where T : BaseEntity;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default);
        Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);

        void ClearChangeTracker();
        void DetachEntity<T>(T entity) where T : class;
        void AttachEntity<T>(T entity) where T : class;

        bool HasChanges();
        IEnumerable<object> GetTrackedEntities();
        void ResetToSnapshot();
    }
}