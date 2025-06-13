using Backend.CMS.Application.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Validation
{
    #region Location Validators
    public class CreateLocationDtoValidator : AbstractValidator<CreateLocationDto>
    {
        public CreateLocationDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Location name is required")
                .MaximumLength(200).WithMessage("Location name cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.LocationCode)
                .MaximumLength(50).WithMessage("Location code cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.LocationCode));

            RuleFor(x => x.LocationType)
                .NotEmpty().WithMessage("Location type is required")
                .MaximumLength(50).WithMessage("Location type cannot exceed 50 characters");

            RuleForEach(x => x.OpeningHours)
                .SetValidator(new CreateLocationOpeningHourDtoValidator())
                .When(x => x.OpeningHours?.Any() == true);

            RuleForEach(x => x.Addresses)
                .SetValidator(new CreateAddressDtoValidator())
                .When(x => x.Addresses?.Any() == true);

            RuleForEach(x => x.ContactDetails)
                .SetValidator(new CreateContactDetailsDtoValidator())
                .When(x => x.ContactDetails?.Any() == true);
        }
    }

    public class UpdateLocationDtoValidator : AbstractValidator<UpdateLocationDto>
    {
        public UpdateLocationDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Location name is required")
                .MaximumLength(200).WithMessage("Location name cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Description));

            RuleFor(x => x.LocationCode)
                .MaximumLength(50).WithMessage("Location code cannot exceed 50 characters")
                .When(x => !string.IsNullOrEmpty(x.LocationCode));

            RuleFor(x => x.LocationType)
                .NotEmpty().WithMessage("Location type is required")
                .MaximumLength(50).WithMessage("Location type cannot exceed 50 characters");

            RuleForEach(x => x.OpeningHours)
                .SetValidator(new UpdateLocationOpeningHourDtoValidator())
                .When(x => x.OpeningHours?.Any() == true);

            RuleForEach(x => x.Addresses)
                .SetValidator(new UpdateAddressDtoValidator())
                .When(x => x.Addresses?.Any() == true);

            RuleForEach(x => x.ContactDetails)
                .SetValidator(new UpdateContactDetailsDtoValidator())
                .When(x => x.ContactDetails?.Any() == true);
        }
    }

    public class CreateLocationOpeningHourDtoValidator : AbstractValidator<CreateLocationOpeningHourDto>
    {
        public CreateLocationOpeningHourDtoValidator()
        {
            RuleFor(x => x.DayOfWeek)
                .IsInEnum().WithMessage("Invalid day of week");

            RuleFor(x => x.OpenTime)
                .NotEmpty().WithMessage("Open time is required")
                .When(x => !x.IsClosed && !x.IsOpen24Hours);

            RuleFor(x => x.CloseTime)
                .NotEmpty().WithMessage("Close time is required")
                .When(x => !x.IsClosed && !x.IsOpen24Hours);

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }

    public class UpdateLocationOpeningHourDtoValidator : AbstractValidator<UpdateLocationOpeningHourDto>
    {
        public UpdateLocationOpeningHourDtoValidator()
        {
            RuleFor(x => x.DayOfWeek)
                .IsInEnum().WithMessage("Invalid day of week");

            RuleFor(x => x.OpenTime)
                .NotEmpty().WithMessage("Open time is required")
                .When(x => !x.IsClosed && !x.IsOpen24Hours);

            RuleFor(x => x.CloseTime)
                .NotEmpty().WithMessage("Close time is required")
                .When(x => !x.IsClosed && !x.IsOpen24Hours);

            RuleFor(x => x.Notes)
                .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }
    #endregion
}
