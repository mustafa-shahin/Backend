using Backend.CMS.Domain.Entities;
using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IContactDetailsRepository : IRepository<ContactDetails>
    {
        Task<ContactDetails?> GetDefaultContactDetailsAsync(int entityId, string entityType);
        Task<IEnumerable<ContactDetails>> GetContactDetailsByEntityAsync(int entityId, string entityType);
        Task<IEnumerable<ContactDetails>> GetContactDetailsByTypeAsync(string contactType);
        Task<ContactDetails?> GetByEmailAsync(string email);
        Task<ContactDetails?> GetByPhoneAsync(string phone);
        Task<bool> SetDefaultContactDetailsAsync(int contactDetailsId, int entityId, string entityType);
        Task<int> CountContactDetailsByEntityAsync(int entityId, string entityType);
        Task<PagedResult<ContactDetails>> SearchContactDetailsAsync(string searchTerm, int page, int pageSize);
        Task<bool> EmailExistsAsync(string email, int? excludeContactDetailsId = null);
        Task<bool> PhoneExistsAsync(string phone, int? excludeContactDetailsId = null);
        Task<PagedResult<ContactDetails>> GetPagedContactDetailsByEntityAsync(int entityId, string entityType, int page, int pageSize);
        Task<PagedResult<ContactDetails>> GetPagedContactDetailsByTypeAsync(string contactType, int page, int pageSize);
    }
}