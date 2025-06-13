using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
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
                .FirstOrDefaultAsync(l => l.IsMainLocation);
        }

        public async Task<IEnumerable<Location>> GetLocationsByCompanyAsync(int companyId)
        {
            return await _dbSet
                .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                .Where(l => l.CompanyId == companyId)
                .OrderBy(l => l.Name)
                .ToListAsync();
        }

        public async Task<Location?> GetWithAddressesAndContactsAsync(int locationId)
        {
            return await _dbSet
                .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                .FirstOrDefaultAsync(l => l.Id == locationId);
        }

        public async Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null)
        {
            var query = _dbSet.Where(l => l.LocationCode == locationCode);

            if (excludeLocationId.HasValue)
                query = query.Where(l => l.Id != excludeLocationId.Value);

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm, int page, int pageSize)
        {
            return await _dbSet
                .Where(l => l.Name.Contains(searchTerm) ||
                           l.LocationCode!.Contains(searchTerm) ||
                           l.Description!.Contains(searchTerm))
                .OrderBy(l => l.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}