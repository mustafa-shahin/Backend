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
        private async Task<PaginatedResult<CategoryDto>> LoadCategoriesAsync(int page, int pageSize, string? search)
        {
            var searchDto = new CategorySearchDto
            {
                SearchTerm = search,
                PageNumber = page,
                PageSize = pageSize,
                SortBy = "Name",
                SortDirection = "Asc"
            };

            // Apply filters only if not ignoring them
            if (!ignoreFiltersOnNextLoad)
            {
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    searchDto.IsActive = statusFilter == "active";
                }

                if (!string.IsNullOrEmpty(visibilityFilter))
                {
                    searchDto.IsVisible = visibilityFilter == "visible";
                }

                if (!string.IsNullOrEmpty(parentFilter))
                {
                    searchDto.ParentCategoryId = parentFilter == "root" ? null : (parentFilter == "sub" ? -1 : null);
                }
            }
            else
            {
                // Reset the flag after use
                ignoreFiltersOnNextLoad = false;
            }

            return await CategoryService.GetCategoriesAsync(searchDto);
        }

        private async Task<CategoryDto?> GetCategoryByIdAsync(int id)
        {
            return await CategoryService.GetCategoryByIdAsync(id);
        }

        private async Task<CategoryDto?> CreateCategoryAsync(CreateCategoryDto createDto)
        {
            return await CategoryService.CreateCategoryAsync(createDto);
        }

        private async Task<CategoryDto?> UpdateCategoryAsync(int id, UpdateCategoryDto updateDto)
        {
            return await CategoryService.UpdateCategoryAsync(id, updateDto);
        }

        private async Task<bool> DeleteCategoryAsync(int id)
        {
            return await CategoryService.DeleteCategoryAsync(id);
        }

        private CreateCategoryDto CreateModelFactory()
        {
            return new CreateCategoryDto
            {
                IsActive = true,
                IsVisible = true,
                SortOrder = 0
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
                CustomFields = category.CustomFields,
                Images = [.. category.Images.Select(img => new CreateCategoryImageDto
                {
                    FileId = img.FileId,
                    Alt = img.Alt,
                    Caption = img.Caption,
                    Position = img.Position,
                    IsFeatured = img.IsFeatured
                })]
            };
        }

        private UpdateCategoryDto CreateToUpdateMapper(CreateCategoryDto createDto)
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
                CustomFields = createDto.CustomFields,
                Images = [.. createDto.Images.Select(img => new UpdateCategoryImageDto
                {
                    Id = 0, // Will be set by the backend for new images
                    FileId = img.FileId,
                    Alt = img.Alt,
                    Caption = img.Caption,
                    Position = img.Position,
                    IsFeatured = img.IsFeatured
                })]
            };
        }

        private async Task<Dictionary<string, string>> ValidateCategoryAsync(CreateCategoryDto model, bool isEdit)
        {
            var errors = new Dictionary<string, string>();

            // Only basic required field validation - let backend handle business logic
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                errors["Name"] = "Category name is required.";
            }

            if (string.IsNullOrWhiteSpace(model.Slug))
            {
                errors["Slug"] = "Category slug is required.";
            }

            return errors;
        }

        private void OnFilterChanged()
        {
            StateHasChanged();
        }

        private void OnCategoryCreated(CategoryDto category)
        {
            // Set flag to ignore filters on next load
            ignoreFiltersOnNextLoad = true;

            // Clear all filters for future loads
            statusFilter = string.Empty;
            visibilityFilter = string.Empty;
            parentFilter = string.Empty;

            // Show success message
            NotificationService.ShowSuccess($"Category '{category.Name}' created successfully!");

            // The GenericCrudPage will call LoadData automatically after this
            // The ignoreFiltersOnNextLoad flag will ensure new category is visible
        }

        private void OnCategoryUpdated(CategoryDto category)
        {
            // Set flag to ignore filters on next load
            ignoreFiltersOnNextLoad = true;

            // Clear filters to ensure updated category is visible
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
    }
}

