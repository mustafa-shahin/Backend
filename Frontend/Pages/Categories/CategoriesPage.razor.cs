// Frontend/Pages/Categories/CategoriesPage.razor.cs
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

        #region Data Loading and CRUD Operations

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

                // Apply filters if they are set
                if (!ignoreFiltersOnNextLoad)
                {
                    if (!string.IsNullOrEmpty(StatusFilter))
                    {
                        searchDto.IsActive = StatusFilter == "active";
                    }

                    if (!string.IsNullOrEmpty(VisibilityFilter))
                    {
                        searchDto.IsVisible = VisibilityFilter == "visible";
                    }

                    if (!string.IsNullOrEmpty(ParentFilter))
                    {
                        if (int.TryParse(ParentFilter, out var parentId))
                        {
                            searchDto.ParentCategoryId = parentId;
                        }
                        else if (ParentFilter == "root")
                        {
                            searchDto.ParentCategoryId = null;
                        }
                    }
                }
                else
                {
                    ignoreFiltersOnNextLoad = false;
                }

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
                // Validate that images are properly set
                if (createDto.Images?.Any() == true)
                {
                    // Ensure at least one image is marked as featured if images exist
                    if (!createDto.Images.Any(img => img.IsFeatured) && createDto.Images.Any())
                    {
                        createDto.Images.First().IsFeatured = true;
                    }

                    // Ensure positions are sequential
                    for (int i = 0; i < createDto.Images.Count; i++)
                    {
                        createDto.Images[i].Position = i;
                    }
                }

                var result = await CategoryService.CreateCategoryAsync(createDto);

                if (result != null)
                {
                    // If images were uploaded during form editing, they need to be associated with the new category
                    await AssociateImagesWithCategory(result.Id, createDto.Images);
                }

                return result;
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
                // Handle image updates through the UpdateCategoryDto if needed
                var result = await CategoryService.UpdateCategoryAsync(id, updateDto);

                if (result != null)
                {
                    // Update category images if they were modified
                    await SynchronizeCategoryImages(id, result);
                }

                return result;
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
                // Check if category can be deleted
                var canDelete = await CategoryService.CanDeleteAsync(id);
                if (!canDelete)
                {
                    NotificationService.ShowError("Cannot delete category: it has associated products or subcategories");
                    return false;
                }

                // Delete associated files first if needed
                try
                {
                    await FileService.DeleteFilesForEntityAsync("Category", id);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the category deletion
                    NotificationService.ShowWarning($"Warning: Could not delete associated files: {ex.Message}");
                }

                return await CategoryService.DeleteCategoryAsync(id);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to delete category: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Model Factories and Mappers

        private CreateCategoryDto CreateModelFactory()
        {
            return new CreateCategoryDto
            {
                IsActive = true,
                IsVisible = true,
                SortOrder = 0,
                Images = new List<CreateCategoryImageDto>(),
                CustomFields = new Dictionary<string, object>()
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
                CustomFields = createDto.CustomFields,
                Images = createDto.Images?.Select(img => new UpdateCategoryImageDto
                {
                    Id = 0, // Will be handled by the backend
                    FileId = img.FileId,
                    Alt = img.Alt,
                    Caption = img.Caption,
                    Position = img.Position,
                    IsFeatured = img.IsFeatured
                }).ToList() ?? new List<UpdateCategoryImageDto>()
            };
        }

        #endregion

        #region Image Management Support

        private async Task AssociateImagesWithCategory(int categoryId, List<CreateCategoryImageDto>? images)
        {
            if (images?.Any() != true) return;

            try
            {
                foreach (var imageDto in images)
                {
                    await CategoryService.AddCategoryImageAsync(categoryId, imageDto);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowWarning($"Category created but some images could not be associated: {ex.Message}");
            }
        }

        private async Task SynchronizeCategoryImages(int categoryId, CategoryDto updatedCategory)
        {
            try
            {
                // This is handled by the UpdateCategoryDto.Images property
                // The backend should automatically synchronize the images
                // Additional synchronization logic can be added here if needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                NotificationService.ShowWarning($"Category updated but image synchronization failed: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

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

        private async Task OnCategoryImagesChanged(List<CategoryImageDto> images)
        {
            // Handle any additional logic when category images are changed
            // This can be used for validation, notifications, or other processing
            try
            {
                if (images?.Any() == true)
                {
                    var featuredCount = images.Count(img => img.IsFeatured);
                    if (featuredCount == 0)
                    {
                        // Ensure at least one image is featured if images exist
                        NotificationService.ShowInfo("Please select a featured image for the category");
                    }
                    else if (featuredCount > 1)
                    {
                        // Ensure only one image is featured
                        NotificationService.ShowWarning("Only one image can be marked as featured");
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error processing image changes: {ex.Message}");
            }
        }

        #endregion

        #region Validation Support

        private async Task<Dictionary<string, string>> ValidateCategoryForm(CreateCategoryDto model, bool isEditMode)
        {
            var errors = new Dictionary<string, string>();

            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    errors["Name"] = "Category name is required";
                }
                else if (model.Name.Length > 255)
                {
                    errors["Name"] = "Category name cannot exceed 255 characters";
                }

                if (string.IsNullOrWhiteSpace(model.Slug))
                {
                    errors["Slug"] = "URL slug is required";
                }
                else if (model.Slug.Length > 255)
                {
                    errors["Slug"] = "URL slug cannot exceed 255 characters";
                }
                else
                {
                    // Validate slug format
                    if (!IsValidSlug(model.Slug))
                    {
                        errors["Slug"] = "URL slug can only contain lowercase letters, numbers, and hyphens";
                    }
                }

                // Validate field lengths
                if (!string.IsNullOrEmpty(model.ShortDescription) && model.ShortDescription.Length > 500)
                {
                    errors["ShortDescription"] = "Short description cannot exceed 500 characters";
                }

                if (!string.IsNullOrEmpty(model.Description) && model.Description.Length > 1000)
                {
                    errors["Description"] = "Description cannot exceed 1000 characters";
                }

                if (!string.IsNullOrEmpty(model.MetaTitle) && model.MetaTitle.Length > 255)
                {
                    errors["MetaTitle"] = "Meta title cannot exceed 255 characters";
                }

                if (!string.IsNullOrEmpty(model.MetaDescription) && model.MetaDescription.Length > 500)
                {
                    errors["MetaDescription"] = "Meta description cannot exceed 500 characters";
                }

                if (!string.IsNullOrEmpty(model.MetaKeywords) && model.MetaKeywords.Length > 500)
                {
                    errors["MetaKeywords"] = "Meta keywords cannot exceed 500 characters";
                }

                // Validate sort order
                if (model.SortOrder < 0 || model.SortOrder > 9999)
                {
                    errors["SortOrder"] = "Sort order must be between 0 and 9999";
                }

                // Validate images
                if (model.Images?.Any() == true)
                {
                    var featuredCount = model.Images.Count(img => img.IsFeatured);
                    if (featuredCount > 1)
                    {
                        errors["Images"] = "Only one image can be marked as featured";
                    }

                    // Validate image file IDs exist
                    var invalidImages = new List<string>();
                    foreach (var image in model.Images)
                    {
                        try
                        {
                            var file = await FileService.GetFileByIdAsync(image.FileId);
                            if (file == null || file.FileType != Backend.CMS.Domain.Enums.FileType.Image)
                            {
                                invalidImages.Add($"Image {image.FileId}");
                            }
                        }
                        catch
                        {
                            invalidImages.Add($"Image {image.FileId}");
                        }
                    }

                    if (invalidImages.Any())
                    {
                        errors["Images"] = $"Invalid images: {string.Join(", ", invalidImages)}";
                    }
                }
            }
            catch (Exception ex)
            {
                errors["General"] = $"Validation error: {ex.Message}";
            }

            return errors;
        }

        private bool IsValidSlug(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return false;

            // Check if slug contains only valid characters
            return slug.All(c => char.IsLetterOrDigit(c) || c == '-') &&
                   !slug.StartsWith("-") &&
                   !slug.EndsWith("-") &&
                   !slug.Contains("--");
        }

        #endregion
    }
}