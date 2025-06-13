using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class CompanyRepository : Repository<Company>, ICompanyRepository
    {
        public CompanyRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Company?> GetCompanyWithDetailsAsync()
        {
            return await _dbSet
                .Include(c => c.Locations.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(c => c.Locations.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.ContactDetails.Where(cd => !cd.IsDeleted))
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                .Include(c => c.ContactDetails.Where(cd => !cd.IsDeleted))
                .FirstOrDefaultAsync();
        }

        public async Task<Company?> GetCompanyWithDetailsAsync(int companyId)
        {
            return await _dbSet
                .Include(c => c.Locations.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.Addresses.Where(a => !a.IsDeleted))
                .Include(c => c.Locations.Where(l => !l.IsDeleted))
                    .ThenInclude(l => l.ContactDetails.Where(cd => !cd.IsDeleted))
                .Include(c => c.Addresses.Where(a => !a.IsDeleted))
                .Include(c => c.ContactDetails.Where(cd => !cd.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == companyId);
        }
    }
}
