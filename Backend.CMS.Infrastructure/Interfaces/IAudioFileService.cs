

using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IAudioFileService
    {
        Task<AudioFileDto> UploadAudioAsync(FileUploadDto uploadDto);
        Task<List<AudioFileDto>> UploadMultipleAudiosAsync(MultipleFileUploadDto uploadDto);
        Task<PaginatedResult<AudioFileDto>> GetAudiosPagedAsync(AudioSearchDto searchDto);
        Task<AudioFileDto?> GetAudioByIdAsync(int fileId);
        Task<AudioFileDto> UpdateAudioAsync(int fileId, UpdateAudioDto updateDto);
        Task<bool> DeleteAudioAsync(int fileId);
    }
 
}