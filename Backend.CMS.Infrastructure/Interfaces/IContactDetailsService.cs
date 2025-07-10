using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IContactDetailsService
    {
        Task<ContactDetailsDto> GetContactDetailsByIdAsync(int contactId);
        Task<PagedResult<ContactDetailsDto>> GetContactDetailsPagedAsync(ContactDetailsSearchDto searchDto);
        Task<List<ContactDetailsDto>> GetContactDetailsByEntityAsync(string entityType, int entityId);
        Task<ContactDetailsDto> CreateContactDetailsAsync(CreateContactDetailsDto createContactDetailsDto, string entityType, int entityId);
        Task<ContactDetailsDto> UpdateContactDetailsAsync(int contactId, UpdateContactDetailsDto updateContactDetailsDto);
        Task<bool> DeleteContactDetailsAsync(int contactId);
        Task<bool> SetDefaultContactDetailsAsync(int contactId, string entityType, int entityId);
        Task<List<ContactDetailsDto>> GetRecentContactDetailsAsync(int count);
        Task<Dictionary<string, object>> GetContactDetailsStatisticsAsync();
        Task<bool> BulkUpdateContactDetailsAsync(IEnumerable<int> contactIds, UpdateContactDetailsDto updateDto);
        Task<bool> BulkDeleteContactDetailsAsync(IEnumerable<int> contactIds);
    }
}