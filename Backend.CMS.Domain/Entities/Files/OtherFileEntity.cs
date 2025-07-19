using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class OtherFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Other;


        // Other file type validation
        public override ValidationResult ValidateFileType()
        {
            return ValidationResult.Success!;
        }

        // Other file type processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}