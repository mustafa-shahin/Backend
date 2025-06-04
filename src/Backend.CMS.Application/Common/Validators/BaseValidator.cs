using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

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

            // Implement HTML sanitization logic
            var sanitizer = new HtmlSanitizer();
            var sanitized = sanitizer.Sanitize(html);
            return sanitized == html;
        }
    }
}
