using Backend.CMS.Application.DTOs;

namespace Frontend.Pages.Categories
{
    public partial class CategoriesPage
    {
        private string statusFilter = string.Empty;
        private string visibilityFilter = string.Empty;
        private string parentFilter = string.Empty;
        private bool ignoreFiltersOnNextLoad = false;

        private string StatusFilter
        {
            get => statusFilter;
            set
            {
                if (statusFilter != value)
                {
                    statusFilter = value;
                    OnFilterChanged();
                }
            }
        }

        private string VisibilityFilter
        {
            get => visibilityFilter;
            set
            {
                if (visibilityFilter != value)
                {
                    visibilityFilter = value;
                    OnFilterChanged();
                }
            }
        }

        private string ParentFilter
        {
            get => parentFilter;
            set
            {
                if (parentFilter != value)
                {
                    parentFilter = value;
                    OnFilterChanged();
                }
            }
        }

        private async Task<PaginatedResult<CategoryDto>> LoadCategories(int page, int pageSize, string? searchTerm)
        {
            try
            {
                var searchDto = new CategorySearchDto
                {
                    PageNumber = page,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    SortBy = "Name",
                    SortDirection = "Asc"
                };

                return await CategoryService.SearchCategoriesAsync(searchDto);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load categories: {ex.Message}");
                return new PaginatedResult<CategoryDto>();
            }
        }

        private async Task<CategoryDto?> GetCategoryById(int id)
        {
            try
            {
                return await CategoryService.GetCategoryByIdAsync(id);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load category: {ex.Message}");
                return null;
            }
        }

        private async Task<CategoryDto?> CreateCategory(CreateCategoryDto createDto)
        {
            try
            {
                return await CategoryService.CreateCategoryAsync(createDto);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to create category: {ex.Message}");
                return null;
            }
        }

        private async Task<CategoryDto?> UpdateCategory(int id, UpdateCategoryDto updateDto)
        {
            try
            {
                return await CategoryService.UpdateCategoryAsync(id, updateDto);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update category: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> DeleteCategory(int id)
        {
            try
            {
                return await CategoryService.DeleteCategoryAsync(id);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to delete category: {ex.Message}");
                return false;
            }
        }


        private CreateCategoryDto CreateModelFactory()
        {
            return new CreateCategoryDto
            {
                IsActive = true,
                IsVisible = true,
                SortOrder = 0,
                Images = new List<CreateCategoryImageDto>()
            };
        }
        private CreateCategoryDto EditModelFactory(CategoryDto category)
        {
            return new CreateCategoryDto
            {
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ShortDescription = category.ShortDescription,
                ParentCategoryId = category.ParentCategoryId,
                IsActive = category.IsActive,
                IsVisible = category.IsVisible,
                SortOrder = category.SortOrder,
                MetaTitle = category.MetaTitle,
                MetaDescription = category.MetaDescription,
                MetaKeywords = category.MetaKeywords,
                CustomFields = category.CustomFields ?? new Dictionary<string, object>(),
                Images = category.Images?.Select(img => new CreateCategoryImageDto
                {
                    FileId = img.FileId,
                    Alt = img.Alt,
                    Caption = img.Caption,
                    Position = img.Position,
                    IsFeatured = img.IsFeatured
                }).ToList() ?? new List<CreateCategoryImageDto>()
            };
        }
        private UpdateCategoryDto MapCreateToUpdate(CreateCategoryDto createDto)
        {
            return new UpdateCategoryDto
            {
                Name = createDto.Name,
                Slug = createDto.Slug,
                Description = createDto.Description,
                ShortDescription = createDto.ShortDescription,
                ParentCategoryId = createDto.ParentCategoryId,
                IsActive = createDto.IsActive,
                IsVisible = createDto.IsVisible,
                SortOrder = createDto.SortOrder,
                MetaTitle = createDto.MetaTitle,
                MetaDescription = createDto.MetaDescription,
                MetaKeywords = createDto.MetaKeywords,
                CustomFields = createDto.CustomFields
            };
        }
        //private UpdateCategoryDto CreateToUpdateMapper(CreateCategoryDto createDto)
        //{
        //    return new UpdateCategoryDto
        //    {
        //        Name = createDto.Name,
        //        Slug = createDto.Slug,
        //        Description = createDto.Description,
        //        ShortDescription = createDto.ShortDescription,
        //        ParentCategoryId = createDto.ParentCategoryId,
        //        IsActive = createDto.IsActive,
        //        IsVisible = createDto.IsVisible,
        //        SortOrder = createDto.SortOrder,
        //        MetaTitle = createDto.MetaTitle,
        //        MetaDescription = createDto.MetaDescription,
        //        MetaKeywords = createDto.MetaKeywords,
        //        CustomFields = createDto.CustomFields,
        //        Images = createDto.Images.Select(img => new UpdateCategoryImageDto
        //        {
        //            Id = 0, // Will be handled by the backend
        //            FileId = img.FileId,
        //            Alt = img.Alt,
        //            Caption = img.Caption,
        //            Position = img.Position,
        //            IsFeatured = img.IsFeatured
        //        }).ToList()
        //    };
        //}

        //private async Task<Dictionary<string, string>> ValidateCategory(CreateCategoryDto model, bool isEditMode)
        //{
        //    var errors = new Dictionary<string, string>();

        //    try
        //    {
        //        // Basic validation
        //        if (string.IsNullOrWhiteSpace(model.Name))
        //        {
        //            errors["Name"] = "Category name is required";
        //        }
        //        else if (model.Name.Length > 255)
        //        {
        //            errors["Name"] = "Category name cannot exceed 255 characters";
        //        }

        //        if (string.IsNullOrWhiteSpace(model.Slug))
        //        {
        //            errors["Slug"] = "URL slug is required";
        //        }
        //        else if (model.Slug.Length > 255)
        //        {
        //            errors["Slug"] = "URL slug cannot exceed 255 characters";
        //        }
        //        else
        //        {
        //            // Validate slug format
        //            if (!IsValidSlug(model.Slug))
        //            {
        //                errors["Slug"] = "URL slug can only contain lowercase letters, numbers, and hyphens";
        //            }
        //            // Note: Slug uniqueness validation is handled by the CategoryForm itself
        //        }

        //        // Validate field lengths
        //        if (!string.IsNullOrEmpty(model.ShortDescription) && model.ShortDescription.Length > 500)
        //        {
        //            errors["ShortDescription"] = "Short description cannot exceed 500 characters";
        //        }

        //        if (!string.IsNullOrEmpty(model.Description) && model.Description.Length > 1000)
        //        {
        //            errors["Description"] = "Description cannot exceed 1000 characters";
        //        }

        //        if (!string.IsNullOrEmpty(model.MetaTitle) && model.MetaTitle.Length > 255)
        //        {
        //            errors["MetaTitle"] = "Meta title cannot exceed 255 characters";
        //        }

        //        if (!string.IsNullOrEmpty(model.MetaDescription) && model.MetaDescription.Length > 500)
        //        {
        //            errors["MetaDescription"] = "Meta description cannot exceed 500 characters";
        //        }

        //        if (!string.IsNullOrEmpty(model.MetaKeywords) && model.MetaKeywords.Length > 500)
        //        {
        //            errors["MetaKeywords"] = "Meta keywords cannot exceed 500 characters";
        //        }

        //        // Validate sort order
        //        if (model.SortOrder < 0 || model.SortOrder > 9999)
        //        {
        //            errors["SortOrder"] = "Sort order must be between 0 and 9999";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        errors["General"] = $"Validation error: {ex.Message}";
        //    }

        //    return errors;
        //}

        private void OnFilterChanged()
        {
            StateHasChanged();
        }

        private async Task OnCategoryCreated(CategoryDto category)
        {
            ignoreFiltersOnNextLoad = true;
            statusFilter = string.Empty;
            visibilityFilter = string.Empty;
            parentFilter = string.Empty;
            NotificationService.ShowSuccess($"Category '{category.Name}' created successfully!");
        }

        private async Task OnCategoryUpdated(CategoryDto category)
        {
            ignoreFiltersOnNextLoad = true;
            statusFilter = string.Empty;
            visibilityFilter = string.Empty;
            parentFilter = string.Empty;
            NotificationService.ShowSuccess($"Category '{category.Name}' updated successfully!");
        }

        private Task OnCategoryDeleted(int categoryId)
        {
            NotificationService.ShowSuccess("Category deleted successfully!");
            return Task.CompletedTask;
        }
        //private bool IsValidSlug(string slug)
        //{
        //    if (string.IsNullOrEmpty(slug))
        //        return false;

        //    // Check if slug contains only valid characters
        //    return slug.All(c => char.IsLetterOrDigit(c) || c == '-') &&
        //           !slug.StartsWith("-") &&
        //           !slug.EndsWith("-") &&
        //           !slug.Contains("--");
        //}

        private async Task OnCategoryImagesChanged(List<CategoryImageDto> images)
        {

        }
    }
}