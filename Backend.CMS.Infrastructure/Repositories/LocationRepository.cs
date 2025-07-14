using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class LocationRepository : Repository<Location>, ILocationRepository
    {
        public LocationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Location?> GetMainLocationAsync()
        {
            return await _dbSet
                .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                .FirstOrDefaultAsync(l => l.IsMainLocation && !l.IsDeleted);
        }

        public async Task<IEnumerable<Location>> GetLocationsByCompanyAsync(int companyId)
        {
            return await _dbSet
                .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                .Where(l => l.CompanyId == companyId && !l.IsDeleted)
                .OrderBy(l => l.Name)
                .ToListAsync();
        }

        public async Task<Location?> GetWithAddressesAndContactsAsync(int locationId)
        {
            return await _dbSet
                .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                .FirstOrDefaultAsync(l => l.Id == locationId && !l.IsDeleted);
        }

        public async Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null)
        {
            var query = _dbSet.Where(l => !l.IsDeleted && l.LocationCode == locationCode);

            if (excludeLocationId.HasValue)
                query = query.Where(l => l.Id != excludeLocationId.Value);

            return await query.AnyAsync();
        }

        public IQueryable<Location> GetQueryableWithIncludes()
        {
            return _dbSet
                .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                .Where(l => !l.IsDeleted);
        }

        public IQueryable<Location> ApplySearchFilter(IQueryable<Location> query, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return query;

            var lowerSearchTerm = searchTerm.ToLower();
            return query.Where(l =>
                l.Name.ToLower().Contains(lowerSearchTerm) ||
                (l.LocationCode != null && l.LocationCode.ToLower().Contains(lowerSearchTerm)) ||
                (l.Description != null && l.Description.ToLower().Contains(lowerSearchTerm)));
        }
    }
}