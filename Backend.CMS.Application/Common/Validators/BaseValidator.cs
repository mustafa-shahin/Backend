using FluentValidation;
using Ganss.Xss;
using System.Linq.Expressions;

namespace Backend.CMS.Application.Common.Validators
{
    public abstract class BaseValidator<T> : AbstractValidator<T>
    {
        protected void RuleForHtml(Expression<Func<T, string?>> expression)
        {
            RuleFor(expression)
                .Must(BeValidHtml)
                .WithMessage("Invalid HTML content detected");
        }

        private bool BeValidHtml(string? html)
        {
            if (string.IsNullOrEmpty(html)) return true;

            try
            {
                var sanitizer = new HtmlSanitizer();
                var sanitized = sanitizer.Sanitize(html);
                return sanitized == html;
            }
            catch
            {
                return false;
            }
        }
    }
}