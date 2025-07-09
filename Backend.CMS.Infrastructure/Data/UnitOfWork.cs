using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Data
{
    /// <summary>
    /// Unit of Work implementation for managing transactions and repository coordination
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<UnitOfWork> _logger;
        private IDbContextTransaction? _currentTransaction;
        private bool _disposed = false;

        // Repository backing fields
        private IUserRepository? _users;
        private IPageRepository? _pages;
        private IFileRepository? _files;
        private IFolderRepository? _folders;
        private ICategoryRepository? _categories;
        private IProductRepository? _products;
        private IProductVariantRepository? _productVariants;
        private ILocationRepository? _locations;
        private ICompanyRepository? _companies;

        public UnitOfWork(
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            ILogger<UnitOfWork> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Repository Properties

        public IUserRepository Users => _users ??= new UserRepository(_context, _userSessionService);
        public IPageRepository Pages => _pages ??= new PageRepository(_context, _userSessionService);
        public IFileRepository Files => _files ??= new FileRepository(_context, _userSessionService);
        public IFolderRepository Folders => _folders ??= new FolderRepository(_context, _userSessionService);
        public ICategoryRepository Categories => _categories ??= new CategoryRepository(_context, _userSessionService);
        public IProductRepository Products => _products ??= new ProductRepository(_context, _userSessionService);
        public IProductVariantRepository ProductVariants => _productVariants ??= new ProductVariantRepository(_context, _userSessionService);
        public ILocationRepository Locations => _locations ??= new LocationRepository(_context, _userSessionService);
        public ICompanyRepository Companies => _companies ??= new CompanyRepository(_context, _userSessionService);

        #endregion

        #region Transaction Management

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_currentTransaction != null)
                {
                    throw new InvalidOperationException("Transaction already in progress");
                }

                _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                _logger.LogDebug("Transaction started with ID: {TransactionId}", _currentTransaction.TransactionId);
                return _currentTransaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to begin transaction");
                throw;
            }
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_currentTransaction == null)
                {
                    throw new InvalidOperationException("No transaction in progress");
                }

                await _currentTransaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Transaction committed successfully with ID: {TransactionId}", _currentTransaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit transaction");
                await RollbackTransactionAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_currentTransaction == null)
                {
                    _logger.LogWarning("Attempted to rollback transaction, but no transaction in progress");
                    return;
                }

                await _currentTransaction.RollbackAsync(cancellationToken);
                _logger.LogDebug("Transaction rolled back successfully with ID: {TransactionId}", _currentTransaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback transaction");
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        #endregion

        #region Save Changes

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var changeCount = await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Saved {ChangeCount} changes to database", changeCount);
                return changeCount;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict occurred while saving changes");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error occurred while saving changes");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while saving changes");
                throw;
            }
        }

        public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            try
            {
                var changeCount = await _context.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                _logger.LogDebug("Saved {ChangeCount} changes to database", changeCount);
                return changeCount;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict occurred while saving changes");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error occurred while saving changes");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while saving changes");
                throw;
            }
        }

        #endregion

        #region Bulk Operations

        public async Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var entityList = entities.ToList();
                if (!entityList.Any())
                    return 0;

                await _context.Set<T>().AddRangeAsync(entityList, cancellationToken);
                var result = await SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Bulk inserted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk insert entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public async Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var entityList = entities.ToList();
                if (!entityList.Any())
                    return 0;

                _context.Set<T>().UpdateRange(entityList);
                var result = await SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Bulk updated {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk update entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public async Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var entityList = entities.ToList();
                if (!entityList.Any())
                    return 0;

                _context.Set<T>().RemoveRange(entityList);
                var result = await SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Bulk deleted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk delete entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        #endregion

        #region Performance Operations

        public async Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters)
        {
            try
            {
                return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute raw SQL: {Sql}", sql);
                throw;
            }
        }

        public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken, params object[] parameters)
        {
            try
            {
                return await _context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute raw SQL: {Sql}", sql);
                throw;
            }
        }

        #endregion

        #region State Management

        public void DetachAllEntities()
        {
            try
            {
                var entries = _context.ChangeTracker.Entries().ToList();
                foreach (var entry in entries)
                {
                    entry.State = EntityState.Detached;
                }
                _logger.LogDebug("Detached {Count} entities from change tracker", entries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detach all entities");
                throw;
            }
        }

        public async Task ReloadEntityAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                await _context.Entry(entity).ReloadAsync(cancellationToken);
                _logger.LogDebug("Reloaded entity of type {EntityType}", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public Task<List<string>> GetPendingAuditChangesAsync()
        {
            try
            {
                var changes = new List<string>();
                var entries = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .ToList();

                foreach (var entry in entries)
                {
                    var entityName = entry.Entity.GetType().Name;
                    var state = entry.State.ToString();

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            changes.Add($"Added {entityName}");
                            break;
                        case EntityState.Modified:
                            var modifiedProperties = entry.Properties
                                .Where(p => p.IsModified)
                                .Select(p => p.Metadata.Name);
                            changes.Add($"Modified {entityName}: {string.Join(", ", modifiedProperties)}");
                            break;
                        case EntityState.Deleted:
                            changes.Add($"Deleted {entityName}");
                            break;
                    }
                }

                return Task.FromResult(changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending audit changes");
                throw;
            }
        }

        public async Task ClearChangeTrackerAsync()
        {
            try
            {
                _context.ChangeTracker.Clear();
                _logger.LogDebug("Change tracker cleared");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear change tracker");
                throw;
            }
        }

        #endregion

        #region Dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    if (_currentTransaction != null)
                    {
                        _currentTransaction.Dispose();
                        _currentTransaction = null;
                    }

                    _context?.Dispose();
                    _logger.LogDebug("UnitOfWork disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while disposing UnitOfWork");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}