using Backend.CMS.Application.DTOs;
using FluentValidation;

namespace Backend.CMS.Infrastructure.Validation
{
    #region Page Validators
    public class CreatePageDtoValidator : AbstractValidator<CreatePageDto>
    {
        public CreatePageDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Page name is required")
                .MaximumLength(200).WithMessage("Page name cannot exceed 200 characters");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Page title is required")
                .MaximumLength(200).WithMessage("Page title cannot exceed 200 characters");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Page slug is required")
                .MaximumLength(200).WithMessage("Page slug cannot exceed 200 characters")
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaTitle));

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaDescription));
        }
    }

    public class UpdatePageDtoValidator : AbstractValidator<UpdatePageDto>
    {
        public UpdatePageDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Page name is required")
                .MaximumLength(200).WithMessage("Page name cannot exceed 200 characters");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Page title is required")
                .MaximumLength(200).WithMessage("Page title cannot exceed 200 characters");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Page slug is required")
                .MaximumLength(200).WithMessage("Page slug cannot exceed 200 characters")
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaTitle));

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaDescription));
        }
    }

    #endregion

    #region User Validators
    public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MinimumLength(3).WithMessage("Username must be at least 3 characters")
                .MaximumLength(256).WithMessage("Username cannot exceed 256 characters")
                .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Username can only contain letters, numbers, underscores, hyphens, and periods");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");

            RuleForEach(x => x.Addresses)
                .SetValidator(new CreateAddressDtoValidator())
                .When(x => x.Addresses?.Any() == true);

            RuleForEach(x => x.ContactDetails)
                .SetValidator(new CreateContactDetailsDtoValidator())
                .When(x => x.ContactDetails?.Any() == true);
        }
    }

    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MinimumLength(3).WithMessage("Username must be at least 3 characters")
                .MaximumLength(256).WithMessage("Username cannot exceed 256 characters")
                .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Username can only contain letters, numbers, underscores, hyphens, and periods");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");

            RuleForEach(x => x.Addresses)
                .SetValidator(new UpdateAddressDtoValidator())
                .When(x => x.Addresses?.Any() == true);

            RuleForEach(x => x.ContactDetails)
                .SetValidator(new UpdateContactDetailsDtoValidator())
                .When(x => x.ContactDetails?.Any() == true);
        }
    }

    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required");
        }
    }

    public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordDtoValidator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Current password is required");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("Confirm password is required")
                .Equal(x => x.NewPassword).WithMessage("Passwords do not match");
        }
    }
    #endregion

    #region Address Validators
    public class CreateAddressDtoValidator : AbstractValidator<CreateAddressDto>
    {
        public CreateAddressDtoValidator()
        {
            RuleFor(x => x.Street)
                .NotEmpty().WithMessage("Street is required")
                .MaximumLength(500).WithMessage("Street cannot exceed 500 characters");

            RuleFor(x => x.HouseNr)
                .MaximumLength(500).WithMessage("House number cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.HouseNr));

            RuleFor(x => x.City)
                .NotEmpty().WithMessage("City is required")
                .MaximumLength(100).WithMessage("City cannot exceed 100 characters");

            RuleFor(x => x.State)
                .NotEmpty().WithMessage("State is required")
                .MaximumLength(100).WithMessage("State cannot exceed 100 characters");

            RuleFor(x => x.Country)
                .NotEmpty().WithMessage("Country is required")
                .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");

            RuleFor(x => x.PostalCode)
                .NotEmpty().WithMessage("Postal code is required")
                .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters");

            RuleFor(x => x.Region)
                .MaximumLength(100).WithMessage("Region cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Region));

            RuleFor(x => x.District)
                .MaximumLength(100).WithMessage("District cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.District));

            RuleFor(x => x.AddressType)
                .MaximumLength(50).WithMessage("Address type cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.AddressType));

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }

    public class UpdateAddressDtoValidator : AbstractValidator<UpdateAddressDto>
    {
        public UpdateAddressDtoValidator()
        {
            RuleFor(x => x.Street)
                .NotEmpty().WithMessage("Street is required")
                .MaximumLength(500).WithMessage("Street cannot exceed 500 characters");

            RuleFor(x => x.HouseNr)
                .MaximumLength(500).WithMessage("House number cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.HouseNr ));

            RuleFor(x => x.City)
                .NotEmpty().WithMessage("City is required")
                .MaximumLength(100).WithMessage("City cannot exceed 100 characters");

            RuleFor(x => x.State)
                .NotEmpty().WithMessage("State is required")
                .MaximumLength(100).WithMessage("State cannot exceed 100 characters");

            RuleFor(x => x.Country)
                .NotEmpty().WithMessage("Country is required")
                .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");

            RuleFor(x => x.PostalCode)
                .NotEmpty().WithMessage("Postal code is required")
                .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters");

            RuleFor(x => x.Region)
                .MaximumLength(100).WithMessage("Region cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Region));

            RuleFor(x => x.District)
                .MaximumLength(100).WithMessage("District cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.District));

            RuleFor(x => x.AddressType)
                .MaximumLength(50).WithMessage("Address type cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.AddressType));

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }
    #endregion

    #region Contact Details Validators
    public class CreateContactDetailsDtoValidator : AbstractValidator<CreateContactDetailsDto>
    {
        public CreateContactDetailsDtoValidator()
        {
            RuleFor(x => x.PrimaryPhone)
                .MaximumLength(50).WithMessage("Primary phone cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.PrimaryPhone));

            RuleFor(x => x.SecondaryPhone)
                .MaximumLength(50).WithMessage("Secondary phone cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.SecondaryPhone));

            RuleFor(x => x.Mobile)
                .MaximumLength(50).WithMessage("Mobile cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.Mobile));

            RuleFor(x => x.Fax)
                .MaximumLength(50).WithMessage("Fax cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.Fax));

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters")
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.SecondaryEmail)
                .EmailAddress().WithMessage("Invalid secondary email format")
                .MaximumLength(256).WithMessage("Secondary email cannot exceed 256 characters")
                .When(x => !string.IsNullOrEmpty(x.SecondaryEmail));

            RuleFor(x => x.Website)
                .Must(BeAValidUrl).WithMessage("Invalid website URL")
                .MaximumLength(500).WithMessage("Website URL cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Website));

            RuleFor(x => x.LinkedInProfile)
                .MaximumLength(500).WithMessage("LinkedIn profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.LinkedInProfile));

            RuleFor(x => x.TwitterProfile)
                .MaximumLength(500).WithMessage("Twitter profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.TwitterProfile));

            RuleFor(x => x.FacebookProfile)
                .MaximumLength(500).WithMessage("Facebook profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.FacebookProfile));

            RuleFor(x => x.InstagramProfile)
                .MaximumLength(500).WithMessage("Instagram profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.InstagramProfile));

            RuleFor(x => x.WhatsAppNumber)
                .MaximumLength(50).WithMessage("WhatsApp number cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.WhatsAppNumber));

            RuleFor(x => x.TelegramHandle)
                .MaximumLength(100).WithMessage("Telegram handle cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.TelegramHandle));


            RuleFor(x => x.ContactType)
                .MaximumLength(50).WithMessage("Contact type cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.ContactType));
        }

        private bool BeAValidUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return true;
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }
    }

    public class UpdateContactDetailsDtoValidator : AbstractValidator<UpdateContactDetailsDto>
    {
        public UpdateContactDetailsDtoValidator()
        {
            RuleFor(x => x.PrimaryPhone)
                .MaximumLength(50).WithMessage("Primary phone cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.PrimaryPhone));

            RuleFor(x => x.SecondaryPhone)
                .MaximumLength(50).WithMessage("Secondary phone cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.SecondaryPhone));

            RuleFor(x => x.Mobile)
                .MaximumLength(50).WithMessage("Mobile cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.Mobile));

            RuleFor(x => x.Fax)
                .MaximumLength(50).WithMessage("Fax cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.Fax));

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters")
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.SecondaryEmail)
                .EmailAddress().WithMessage("Invalid secondary email format")
                .MaximumLength(256).WithMessage("Secondary email cannot exceed 256 characters")
                .When(x => !string.IsNullOrEmpty(x.SecondaryEmail));

            RuleFor(x => x.Website)
                .Must(BeAValidUrl).WithMessage("Invalid website URL")
                .MaximumLength(500).WithMessage("Website URL cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Website));

            RuleFor(x => x.LinkedInProfile)
                .MaximumLength(500).WithMessage("LinkedIn profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.LinkedInProfile));

            RuleFor(x => x.TwitterProfile)
                .MaximumLength(500).WithMessage("Twitter profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.TwitterProfile));

            RuleFor(x => x.FacebookProfile)
                .MaximumLength(500).WithMessage("Facebook profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.FacebookProfile));

            RuleFor(x => x.InstagramProfile)
                .MaximumLength(500).WithMessage("Instagram profile cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.InstagramProfile));

            RuleFor(x => x.WhatsAppNumber)
                .MaximumLength(50).WithMessage("WhatsApp number cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.WhatsAppNumber));

            RuleFor(x => x.TelegramHandle)
                .MaximumLength(100).WithMessage("Telegram handle cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.TelegramHandle));

            RuleFor(x => x.ContactType)
                .MaximumLength(50).WithMessage("Contact type cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.ContactType));
        }

        private bool BeAValidUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return true;
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }
    }
    #endregion

    #region Company Validators
    public class UpdateCompanyDtoValidator : AbstractValidator<UpdateCompanyDto>
    {
        public UpdateCompanyDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Company name is required")
                .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.Logo)
                .MaximumLength(500).WithMessage("Logo URL cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Logo));

            RuleFor(x => x.Favicon)
                .MaximumLength(500).WithMessage("Favicon URL cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Favicon));

            RuleFor(x => x.Timezone)
                .MaximumLength(100).WithMessage("Timezone cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Timezone));

            RuleFor(x => x.Currency)
                .MaximumLength(10).WithMessage("Currency cannot exceed 10 characters")
                .When(x => !string.IsNullOrEmpty(x.Currency));

            RuleFor(x => x.Language)
                .MaximumLength(10).WithMessage("Language cannot exceed 10 characters")
                .When(x => !string.IsNullOrEmpty(x.Language));

            RuleForEach(x => x.Addresses)
                .SetValidator(new UpdateAddressDtoValidator())
                .When(x => x.Addresses?.Any() == true);

            RuleForEach(x => x.ContactDetails)
                .SetValidator(new UpdateContactDetailsDtoValidator())
                .When(x => x.ContactDetails?.Any() == true);
        }
    }
    #endregion




    #region Auth DTOs Validators
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .MinimumLength(3).WithMessage("Username must be at least 3 characters")
                .MaximumLength(256).WithMessage("Username cannot exceed 256 characters")
                .Matches(@"^[a-zA-Z0-9_.-]+$").WithMessage("Username can only contain letters, numbers, underscores, hyphens, and periods");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");
        }
    }

    public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
    {
        public ForgotPasswordDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");
        }
    }

    public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordDtoValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Reset token is required");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters")
                .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]").WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character");
        }
    }

    public class RefreshTokenDtoValidator : AbstractValidator<RefreshTokenDto>
    {
        public RefreshTokenDtoValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh token is required");
        }
    }

    public class Verify2FADtoValidator : AbstractValidator<Verify2FADto>
    {
        public Verify2FADtoValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Verification code is required")
                .Length(6).WithMessage("Verification code must be 6 digits")
                .Matches(@"^\d{6}$").WithMessage("Verification code must contain only digits");
        }
    }

    #endregion

    #region Folder Validators
    public class CreateFolderDtoValidator : AbstractValidator<CreateFolderDto>
    {
        public CreateFolderDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Folder name is required")
                .MaximumLength(255).WithMessage("Folder name cannot exceed 255 characters")
                .Matches(@"^[a-zA-Z0-9\s\-_().]+$").WithMessage("Folder name contains invalid characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.ParentFolderId)
                .GreaterThan(0).WithMessage("Parent folder ID must be greater than 0")
                .When(x => x.ParentFolderId.HasValue);

            RuleFor(x => x.FolderType)
                .IsInEnum().WithMessage("Invalid folder type");

            RuleFor(x => x.Metadata)
                .NotNull().WithMessage("Metadata cannot be null");
        }
    }

    public class UpdateFolderDtoValidator : AbstractValidator<UpdateFolderDto>
    {
        public UpdateFolderDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Folder name is required")
                .MaximumLength(255).WithMessage("Folder name cannot exceed 255 characters")
                .Matches(@"^[a-zA-Z0-9\s\-_().]+$").WithMessage("Folder name contains invalid characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.Metadata)
                .NotNull().WithMessage("Metadata cannot be null");
        }
    }

    public class MoveFolderDtoValidator : AbstractValidator<MoveFolderDto>
    {
        public MoveFolderDtoValidator()
        {
            RuleFor(x => x.FolderId)
                .NotEmpty().WithMessage("Folder ID is required")
                .GreaterThan(0).WithMessage("Folder ID must be greater than 0");

            RuleFor(x => x.NewParentFolderId)
                .GreaterThan(0).WithMessage("New parent folder ID must be greater than 0")
                .When(x => x.NewParentFolderId.HasValue);

        }
    }
    #endregion

    #region File Validators
    public class FileUploadDtoValidator : AbstractValidator<FileUploadDto>
    {
        public FileUploadDtoValidator()
        {
            RuleFor(x => x.File)
                .NotNull().WithMessage("File is required");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.Alt)
                .MaximumLength(255).WithMessage("Alt text cannot exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.Alt));

            RuleFor(x => x.FolderId)
                .GreaterThan(0).WithMessage("Folder ID must be greater than 0")
                .When(x => x.FolderId.HasValue);

            RuleFor(x => x.Tags)
                .NotNull().WithMessage("Tags cannot be null");
        }
    }

    public class MultipleFileUploadDtoValidator : AbstractValidator<MultipleFileUploadDto>
    {
        public MultipleFileUploadDtoValidator()
        {
            RuleFor(x => x.Files)
                .NotNull().WithMessage("Files collection is required")
                .Must(files => files.Count > 0).WithMessage("At least one file is required")
                .Must(files => files.Count <= 20).WithMessage("Cannot upload more than 20 files at once");

            RuleFor(x => x.FolderId)
                .GreaterThan(0).WithMessage("Folder ID must be greater than 0")
                .When(x => x.FolderId.HasValue);
        }
    }

    public class UpdateFileDtoValidator : AbstractValidator<UpdateFileDto>
    {
        public UpdateFileDtoValidator()
        {
            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.Alt)
                .MaximumLength(255).WithMessage("Alt text cannot exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.Alt));

            RuleFor(x => x.FolderId)
                .GreaterThan(0).WithMessage("Folder ID must be greater than 0")
                .When(x => x.FolderId.HasValue);

            RuleFor(x => x.Tags)
                .NotNull().WithMessage("Tags cannot be null");
        }
    }

    public class MoveFileDtoValidator : AbstractValidator<MoveFileDto>
    {
        public MoveFileDtoValidator()
        {
            RuleFor(x => x.FileId)
                .NotEmpty().WithMessage("File ID is required")
                .GreaterThan(0).WithMessage("File ID must be greater than 0");

            RuleFor(x => x.NewFolderId)
                .GreaterThan(0).WithMessage("New folder ID must be greater than 0")
                .When(x => x.NewFolderId.HasValue);
        }
    }

    public class CopyFileDtoValidator : AbstractValidator<CopyFileDto>
    {
        public CopyFileDtoValidator()
        {
            RuleFor(x => x.FileId)
                .NotEmpty().WithMessage("File ID is required")
                .GreaterThan(0).WithMessage("File ID must be greater than 0");

            RuleFor(x => x.DestinationFolderId)
                .GreaterThan(0).WithMessage("Destination folder ID must be greater than 0")
                .When(x => x.DestinationFolderId.HasValue);

            RuleFor(x => x.NewName)
                .MaximumLength(255).WithMessage("New name cannot exceed 255 characters")
                .When(x => !string.IsNullOrEmpty(x.NewName));
        }
    }

    public class FileSearchDtoValidator : AbstractValidator<FileSearchDto>
    {
        public FileSearchDtoValidator()
        {

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Page size must be greater than 0")
                .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");

            RuleFor(x => x.MinSize)
                .GreaterThanOrEqualTo(0).WithMessage("Minimum size must be non-negative")
                .When(x => x.MinSize.HasValue);

            RuleFor(x => x.MaxSize)
                .GreaterThanOrEqualTo(0).WithMessage("Maximum size must be non-negative")
                .GreaterThanOrEqualTo(x => x.MinSize).WithMessage("Maximum size must be greater than or equal to minimum size")
                .When(x => x.MaxSize.HasValue && x.MinSize.HasValue);

            RuleFor(x => x.CreatedTo)
                .GreaterThanOrEqualTo(x => x.CreatedFrom).WithMessage("End date must be after start date")
                .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);

            RuleFor(x => x.SortBy)
                .NotEmpty().WithMessage("Sort by field is required")
                .Must(BeValidSortField).WithMessage("Invalid sort field");

            RuleFor(x => x.SortDirection)
                .NotEmpty().WithMessage("Sort direction is required")
                .Must(BeValidSortDirection).WithMessage("Sort direction must be 'asc' or 'desc'");

            RuleFor(x => x.SearchTerm)
                .MaximumLength(500).WithMessage("Search term cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.SearchTerm));

            RuleFor(x => x.Tags)
                .NotNull().WithMessage("Tags list cannot be null");
        }

        private bool BeValidSortField(string sortField)
        {
            var validFields = new[] { "name", "size", "createdat", "updatedat", "filename", "type" };
            return validFields.Contains(sortField.ToLowerInvariant());
        }

        private bool BeValidSortDirection(string sortDirection)
        {
            return sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                   sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        }
    }
    #endregion

    #region Additional Folder Validators (for new DTOs only)
    public class RenameFolderDtoValidator : AbstractValidator<RenameFolderDto>
    {
        public RenameFolderDtoValidator()
        {
            RuleFor(x => x.NewName)
                .NotEmpty().WithMessage("New name is required")
                .MaximumLength(255).WithMessage("New name cannot exceed 255 characters")
                .Matches(@"^[a-zA-Z0-9\s\-_().]+$").WithMessage("New name contains invalid characters");
        }
    }

    public class CopyFolderDtoValidator : AbstractValidator<CopyFolderDto>
    {
        public CopyFolderDtoValidator()
        {
            RuleFor(x => x.FolderId)
                .NotEmpty().WithMessage("Folder ID is required")
                .GreaterThan(0).WithMessage("Folder ID must be greater than 0");

            RuleFor(x => x.DestinationFolderId)
                .GreaterThan(0).WithMessage("Destination folder ID must be greater than 0")
                .When(x => x.DestinationFolderId.HasValue);

            RuleFor(x => x.NewName)
                .MaximumLength(255).WithMessage("New name cannot exceed 255 characters")
                .Matches(@"^[a-zA-Z0-9\s\-_().]+$").WithMessage("New name contains invalid characters")
                .When(x => !string.IsNullOrEmpty(x.NewName));
        }
    }
    #endregion

    #region Category Validators
    public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
    {
        public CreateCategoryDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Category name is required")
                .MaximumLength(200).WithMessage("Category name cannot exceed 200 characters");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Category slug is required")
                .MaximumLength(200).WithMessage("Category slug cannot exceed 200 characters")
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.ShortDescription)
                .MaximumLength(500).WithMessage("Short description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.ShortDescription));


            RuleFor(x => x.ParentCategoryId)
                .GreaterThan(0).WithMessage("Parent category ID must be greater than 0")
                .When(x => x.ParentCategoryId.HasValue);

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaTitle));

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaDescription));

            RuleFor(x => x.MetaKeywords)
                .MaximumLength(500).WithMessage("Meta keywords cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaKeywords));

            RuleFor(x => x.CustomFields)
                .NotNull().WithMessage("Custom fields cannot be null");
        }
    }

    public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
    {
        public UpdateCategoryDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Category name is required")
                .MaximumLength(200).WithMessage("Category name cannot exceed 200 characters");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Category slug is required")
                .MaximumLength(200).WithMessage("Category slug cannot exceed 200 characters")
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.ShortDescription)
                .MaximumLength(500).WithMessage("Short description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.ShortDescription));

            RuleFor(x => x.ParentCategoryId)
                .GreaterThan(0).WithMessage("Parent category ID must be greater than 0")
                .When(x => x.ParentCategoryId.HasValue);

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaTitle));

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaDescription));

            RuleFor(x => x.MetaKeywords)
                .MaximumLength(500).WithMessage("Meta keywords cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaKeywords));

            RuleFor(x => x.CustomFields)
                .NotNull().WithMessage("Custom fields cannot be null");
        }
    }

    public class CategorySearchDtoValidator : AbstractValidator<CategorySearchDto>
    {
        public CategorySearchDtoValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page must be greater than 0");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Page size must be greater than 0")
                .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");

            RuleFor(x => x.ParentCategoryId)
                .GreaterThan(0).WithMessage("Parent category ID must be greater than 0")
                .When(x => x.ParentCategoryId.HasValue);

            RuleFor(x => x.SearchTerm)
                .MaximumLength(500).WithMessage("Search term cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.SearchTerm));

            RuleFor(x => x.SortBy)
                .NotEmpty().WithMessage("Sort by field is required")
                .Must(BeValidSortField).WithMessage("Invalid sort field");

            RuleFor(x => x.SortDirection)
                .NotEmpty().WithMessage("Sort direction is required")
                .Must(BeValidSortDirection).WithMessage("Sort direction must be 'asc' or 'desc'");
        }

        private bool BeValidSortField(string sortField)
        {
            var validFields = new[] { "name", "createdat", "updatedat", "sortorder" };
            return validFields.Contains(sortField.ToLowerInvariant());
        }

        private bool BeValidSortDirection(string sortDirection)
        {
            return sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                   sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        }
    }
    #endregion

    #region Product Validators
    public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required")
                .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Product slug is required")
                .MaximumLength(200).WithMessage("Product slug cannot exceed 200 characters")
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");


            RuleFor(x => x.Vendor)
                .MaximumLength(200).WithMessage("Vendor cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Vendor));

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaTitle));

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaDescription));

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Invalid product status");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Invalid product type");

            RuleFor(x => x.CategoryIds)
                .NotNull().WithMessage("Category IDs cannot be null");

            RuleFor(x => x.Variants)
                .NotNull().WithMessage("Variants cannot be null");

            RuleFor(x => x.CustomFields)
                .NotNull().WithMessage("Custom fields cannot be null");

            RuleFor(x => x.SEOSettings)
                .NotNull().WithMessage("SEO settings cannot be null");

            RuleForEach(x => x.Variants)
                .SetValidator(new CreateProductVariantDtoValidator())
                .When(x => x.Variants?.Any() == true);
        }
    }

    public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
    {
        public UpdateProductDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Product name is required")
                .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Product slug is required")
                .MaximumLength(200).WithMessage("Product slug cannot exceed 200 characters")
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaTitle));

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.MetaDescription));

            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Invalid product status");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Invalid product type");

            RuleFor(x => x.CategoryIds)
                .NotNull().WithMessage("Category IDs cannot be null");

            RuleFor(x => x.Variants)
                .NotNull().WithMessage("Variants cannot be null");

            RuleFor(x => x.CustomFields)
                .NotNull().WithMessage("Custom fields cannot be null");

            RuleFor(x => x.SEOSettings)
                .NotNull().WithMessage("SEO settings cannot be null");
        }
    }

    public class ProductSearchDtoValidator : AbstractValidator<ProductSearchDto>
    {
        public ProductSearchDtoValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page must be greater than 0");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Page size must be greater than 0")
                .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");

            RuleFor(x => x.SearchTerm)
                .MaximumLength(500).WithMessage("Search term cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.SearchTerm));

            RuleFor(x => x.Vendor)
                .MaximumLength(200).WithMessage("Vendor cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Vendor));

            RuleFor(x => x.CategoryIds)
                .NotNull().WithMessage("Category IDs cannot be null");

            RuleFor(x => x.SortBy)
                .NotEmpty().WithMessage("Sort by field is required")
                .Must(BeValidSortField).WithMessage("Invalid sort field");

            RuleFor(x => x.SortDirection)
                .NotEmpty().WithMessage("Sort direction is required")
                .Must(BeValidSortDirection).WithMessage("Sort direction must be 'asc' or 'desc'");
        }

        private bool BeValidSortField(string sortField)
        {
            var validFields = new[] { "name", "price", "createdat", "updatedat", "vendor" };
            return validFields.Contains(sortField.ToLowerInvariant());
        }

        private bool BeValidSortDirection(string sortDirection)
        {
            return sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                   sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        }
    }
    #endregion

    #region Product Variant Validators
    public class CreateProductVariantDtoValidator : AbstractValidator<CreateProductVariantDto>
    {
        public CreateProductVariantDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Variant title is required")
                .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0");

            RuleFor(x => x.CompareAtPrice)
                .GreaterThan(x => x.Price).WithMessage("Compare at price must be greater than price")
                .When(x => x.CompareAtPrice.HasValue);

            RuleFor(x => x.CostPerItem)
                .GreaterThanOrEqualTo(0).WithMessage("Cost per item must be greater than or equal to 0")
                .When(x => x.CostPerItem.HasValue);

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity must be greater than or equal to 0");

            RuleFor(x => x.Weight)
                .GreaterThanOrEqualTo(0).WithMessage("Weight must be greater than or equal to 0");

            RuleFor(x => x.WeightUnit)
                .MaximumLength(10).WithMessage("Weight unit cannot exceed 10 characters")
                .When(x => !string.IsNullOrEmpty(x.WeightUnit));

            RuleFor(x => x.Barcode)
                .MaximumLength(100).WithMessage("Barcode cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Barcode));


            RuleFor(x => x.Option1)
                .MaximumLength(100).WithMessage("Option 1 cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Option1));

            RuleFor(x => x.Option2)
                .MaximumLength(100).WithMessage("Option 2 cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Option2));

            RuleFor(x => x.Option3)
                .MaximumLength(100).WithMessage("Option 3 cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Option3));

            RuleFor(x => x.CustomFields)
                .NotNull().WithMessage("Custom fields cannot be null");
        }
    }

    public class UpdateProductVariantDtoValidator : AbstractValidator<UpdateProductVariantDto>
    {
        public UpdateProductVariantDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Variant title is required")
                .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");


            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0");

            RuleFor(x => x.CompareAtPrice)
                .GreaterThan(x => x.Price).WithMessage("Compare at price must be greater than price")
                .When(x => x.CompareAtPrice.HasValue);

            RuleFor(x => x.CostPerItem)
                .GreaterThanOrEqualTo(0).WithMessage("Cost per item must be greater than or equal to 0")
                .When(x => x.CostPerItem.HasValue);

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity must be greater than or equal to 0");

            RuleFor(x => x.Weight)
                 .GreaterThanOrEqualTo(0).WithMessage("Weight must be greater than or equal to 0");

            RuleFor(x => x.WeightUnit)
                .MaximumLength(10).WithMessage("Weight unit cannot exceed 10 characters")
                .When(x => !string.IsNullOrEmpty(x.WeightUnit));

            RuleFor(x => x.Barcode)
                .MaximumLength(100).WithMessage("Barcode cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Barcode));

            RuleFor(x => x.Option1)
                .MaximumLength(100).WithMessage("Option 1 cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Option1));

            RuleFor(x => x.Option2)
                .MaximumLength(100).WithMessage("Option 2 cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Option2));

            RuleFor(x => x.Option3)
                .MaximumLength(100).WithMessage("Option 3 cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Option3));

            RuleFor(x => x.CustomFields)
                .NotNull().WithMessage("Custom fields cannot be null");
        }
    }
    #endregion

    #region Product Image Validators
    public class CreateProductImageDtoValidator : AbstractValidator<CreateProductImageDto>
    {
        public CreateProductImageDtoValidator()
        {
            RuleFor(x => x.FileId)
                .GreaterThan(0).WithMessage("File ID must be greater than 0");

            RuleFor(x => x.Alt)
                .MaximumLength(200).WithMessage("Alt text cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Alt));

            RuleFor(x => x.Caption)
                .MaximumLength(500).WithMessage("Caption cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Caption));

            RuleFor(x => x.Position)
                .GreaterThanOrEqualTo(0).WithMessage("Position must be greater than or equal to 0");
        }
    }

    public class UpdateProductImageDtoValidator : AbstractValidator<UpdateProductImageDto>
    {
        public UpdateProductImageDtoValidator()
        {
            RuleFor(x => x.FileId)
                .GreaterThan(0).WithMessage("File ID must be greater than 0");

            RuleFor(x => x.Alt)
                .MaximumLength(200).WithMessage("Alt text cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.Alt));

            RuleFor(x => x.Caption)
                .MaximumLength(500).WithMessage("Caption cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Caption));

            RuleFor(x => x.Position)
                .GreaterThanOrEqualTo(0).WithMessage("Position must be greater than or equal to 0");
        }
    }
    #endregion

    #region Product Option Validators
    public class CreateProductOptionDtoValidator : AbstractValidator<CreateProductOptionDto>
    {
        public CreateProductOptionDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Option name is required")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

            RuleFor(x => x.Values)
                .NotNull().WithMessage("Option values cannot be null")
                .Must(x => x.Count > 0).WithMessage("At least one option value is required");

            RuleForEach(x => x.Values)
                .SetValidator(new CreateProductOptionValueDtoValidator());
        }
    }

    public class UpdateProductOptionDtoValidator : AbstractValidator<UpdateProductOptionDto>
    {
        public UpdateProductOptionDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Option name is required")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

            RuleFor(x => x.Values)
                .NotNull().WithMessage("Option values cannot be null")
                .Must(x => x.Count > 0).WithMessage("At least one option value is required");

            RuleForEach(x => x.Values)
                .SetValidator(new UpdateProductOptionValueDtoValidator());
        }
    }

    public class CreateProductOptionValueDtoValidator : AbstractValidator<CreateProductOptionValueDto>
    {
        public CreateProductOptionValueDtoValidator()
        {
            RuleFor(x => x.Value)
                .NotEmpty().WithMessage("Option value is required")
                .MaximumLength(100).WithMessage("Value cannot exceed 100 characters");
        }
    }

    public class UpdateProductOptionValueDtoValidator : AbstractValidator<UpdateProductOptionValueDto>
    {
        public UpdateProductOptionValueDtoValidator()
        {
            RuleFor(x => x.Value)
                .NotEmpty().WithMessage("Option value is required")
                .MaximumLength(100).WithMessage("Value cannot exceed 100 characters");
        }
    }
    #endregion
}