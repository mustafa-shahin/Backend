using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface ISocialAuthService
    {
        Task<LoginResponseDto> SocialLoginAsync(SocialLoginDto socialLoginDto);
        Task<string> GetSocialLoginUrlAsync(string provider, string returnUrl);
        Task<LoginResponseDto> HandleSocialCallbackAsync(string provider, SocialAuthCallbackDto callbackDto);
        Task<SocialUserInfoDto> GetSocialUserInfoAsync(string provider, string accessToken);
        Task<bool> LinkSocialAccountAsync(int userId, LinkSocialAccountDto linkAccountDto);
        Task<bool> UnlinkSocialAccountAsync(int userId, string provider);
        Task<List<UserExternalLogin>> GetUserSocialAccountsAsync(int userId);
        Task<UserDto?> FindUserBySocialIdAsync(string provider, string externalUserId);
        Task<bool> RefreshSocialTokenAsync(int userId, string provider);
    }
}