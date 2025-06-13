namespace Backend.CMS.Application.DTOs
{
    public class ForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class Enable2FAResponseDto
    {
        public string Secret { get; set; } = string.Empty;
        public string QrCodeUrl { get; set; } = string.Empty;
    }

    public class Verify2FADto
    {
        public string Code { get; set; } = string.Empty;
    }
    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}
