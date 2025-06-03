using FluentValidation;
using Backend.CMS.Application.Features.Pages.DTOs;

namespace Backend.CMS.Application.Features.Pages.Validators
{
    public class CreatePageValidator : AbstractValidator<CreatePageDto>
    {
        public CreatePageValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Page name is required.")
                .MaximumLength(200).WithMessage("Page name must not exceed 200 characters.");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Page title is required.")
                .MaximumLength(200).WithMessage("Page title must not exceed 200 characters.");

            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Page slug is required.")
                .MaximumLength(200).WithMessage("Page slug must not exceed 200 characters.")
                .Matches(@"^[a-z0-9-]+$").WithMessage("Slug can only contain lowercase letters, numbers, and hyphens.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

            RuleFor(x => x.MetaTitle)
                .MaximumLength(200).WithMessage("Meta title must not exceed 200 characters.");

            RuleFor(x => x.MetaDescription)
                .MaximumLength(500).WithMessage("Meta description must not exceed 500 characters.");

            RuleFor(x => x.MetaKeywords)
                .MaximumLength(500).WithMessage("Meta keywords must not exceed 500 characters.");

            RuleFor(x => x.Template)
                .MaximumLength(100).WithMessage("Template name must not exceed 100 characters.");
        }
    }
}
