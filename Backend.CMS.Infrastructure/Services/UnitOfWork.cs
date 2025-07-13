using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly Dictionary<Type, object> _repositories;
        private bool _disposed = false;

        private IUserRepository? _users;
        private ICategoryRepository? _categories;
        private ICompanyRepository? _companies;
        private ILocationRepository? _locations;
        private IPageRepository? _pages;
        private IProductRepository? _products;
        private IProductVariantRepository? _productVariants;
        private IFileRepository? _files;
        private IFolderRepository? _folders;
        private IAddressRepository? _addresses;
        private IContactDetailsRepository? _contactDetails;

        public UnitOfWork(
            ApplicationDbContext context,
            ILogger<UnitOfWork> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repositories = new Dictionary<Type, object>();
        }

        public IUserRepository Users =>
            _users ??= new UserRepository(_context);

        public ICategoryRepository Categories =>
            _categories ??= new CategoryRepository(_context);

        public ICompanyRepository Companies =>
            _companies ??= new CompanyRepository(_context);

        public ILocationRepository Locations =>
            _locations ??= new LocationRepository(_context);

        public IPageRepository Pages =>
            _pages ??= new PageRepository(_context);

        public IProductRepository Products =>
            _products ??= new ProductRepository(_context);

        public IProductVariantRepository ProductVariants =>
            _productVariants ??= new ProductVariantRepository(_context);

        public IFileRepository Files =>
            _files ??= new FileRepository(_context);

        public IFolderRepository Folders =>
            _folders ??= new FolderRepository(_context);

        public IAddressRepository Addresses =>
            _addresses ??= new AddressRepository(_context);

        public IContactDetailsRepository ContactDetails =>
            _contactDetails ??= new ContactDetailsRepository(_context);

        public IRepository<T> GetRepository<T>() where T : BaseEntity
        {
            var type = typeof(T);
            if (_repositories.ContainsKey(type))
            {
                return (IRepository<T>)_repositories[type];
            }

            var repository = new Repository<T>(_context);
            _repositories[type] = repository;
            return repository;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("SaveChanges completed successfully. {EntityCount} entities affected.", result);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict occurred during SaveChanges");

                // Handle concurrency conflicts by refreshing entities
                foreach (var entry in ex.Entries)
                {
                    if (entry.Entity is BaseEntity)
                    {
                        await entry.ReloadAsync(cancellationToken);
                    }
                }
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error occurred during SaveChanges");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during SaveChanges");
                throw;
            }
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                _logger.LogDebug("Transaction started: {TransactionId}", transaction.TransactionId);
                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to begin transaction");
                throw;
            }
        }

        public async Task CommitTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                await transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Transaction committed successfully: {TransactionId}", transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit transaction: {TransactionId}", transaction.TransactionId);
                throw;
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }

        public async Task RollbackTransactionAsync(IDbContextTransaction transaction, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogDebug("Transaction rolled back successfully: {TransactionId}", transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback transaction: {TransactionId}", transaction.TransactionId);
                throw;
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                IDbContextTransaction? transaction = null;
                try
                {
                    transaction = await BeginTransactionAsync(cancellationToken);
                    await action();
                    await CommitTransactionAsync(transaction, cancellationToken);
                }
                catch
                {
                    if (transaction != null)
                    {
                        await RollbackTransactionAsync(transaction, cancellationToken);
                    }
                    throw;
                }
            });
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                IDbContextTransaction? transaction = null;
                try
                {
                    transaction = await BeginTransactionAsync(cancellationToken);
                    var result = await action();
                    await CommitTransactionAsync(transaction, cancellationToken);
                    return result;
                }
                catch
                {
                    if (transaction != null)
                    {
                        await RollbackTransactionAsync(transaction, cancellationToken);
                    }
                    throw;
                }
            });
        }

        public void ClearChangeTracker()
        {
            try
            {
                _context.ChangeTracker.Clear();
                _logger.LogDebug("ChangeTracker cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing ChangeTracker");
                throw;
            }
        }

        public void DetachEntity<T>(T entity) where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            try
            {
                var entry = _context.Entry(entity);
                if (entry.State != EntityState.Detached)
                {
                    entry.State = EntityState.Detached;
                    _logger.LogDebug("Entity detached: {EntityType}", typeof(T).Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detaching entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public void AttachEntity<T>(T entity) where T : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            try
            {
                var entry = _context.Entry(entity);
                if (entry.State == EntityState.Detached)
                {
                    entry.State = EntityState.Unchanged;
                    _logger.LogDebug("Entity attached: {EntityType}", typeof(T).Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public bool HasChanges()
        {
            try
            {
                var hasChanges = _context.ChangeTracker.HasChanges();
                _logger.LogDebug("ChangeTracker has changes: {HasChanges}", hasChanges);
                return hasChanges;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if ChangeTracker has changes");
                return false;
            }
        }

        public IEnumerable<object> GetTrackedEntities()
        {
            try
            {
                return _context.ChangeTracker.Entries()
                    .Select(e => e.Entity)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracked entities");
                return Enumerable.Empty<object>();
            }
        }

        public void ResetToSnapshot()
        {
            try
            {
                foreach (var entry in _context.ChangeTracker.Entries())
                {
                    switch (entry.State)
                    {
                        case EntityState.Modified:
                            entry.State = EntityState.Unchanged;
                            break;
                        case EntityState.Added:
                            entry.State = EntityState.Detached;
                            break;
                        case EntityState.Deleted:
                            entry.State = EntityState.Unchanged;
                            break;
                    }
                }
                _logger.LogDebug("ChangeTracker reset to snapshot");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting ChangeTracker to snapshot");
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Clear any pending changes to avoid issues during disposal
                    if (_context.ChangeTracker.HasChanges())
                    {
                        _context.ChangeTracker.Clear();
                    }

                    _context.Dispose();
                    _logger.LogDebug("UnitOfWork disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while disposing UnitOfWork");
                }
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}