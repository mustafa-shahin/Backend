// Frontend/Forms/Products/ProductForm.razor.cs
using Backend.CMS.Application.DTOs;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace Frontend.Forms.Products
{
    public partial class ProductForm : ComponentBase, IDisposable
    {
        [Parameter] public CreateProductDto Model { get; set; } = new();
        [Parameter] public Dictionary<string, string> ValidationErrors { get; set; } = new();
        [Parameter] public bool IsEditMode { get; set; }
        [Parameter] public bool IsSaving { get; set; }

        private List<CategoryTreeDto>? availableCategories;
        private bool isLoadingCategories = true;
        private bool isValidatingSlug = false;
        private bool? slugValidationResult = null;
        private System.Threading.Timer? slugValidationTimer;
        private string previousSlug = string.Empty;

        // Image picker support
        private List<object> productImageObjects = new();

        // State tracking for immediate updates
        private int variantCounter = 0;

        protected override void OnParametersSet()
        {
            // Convert CreateProductImageDto to objects for the generic ImagePicker
            productImageObjects = [.. Model.Images.Cast<object>()];

            // Ensure at least one variant exists if HasVariants is false
            if (!Model.HasVariants && Model.Variants.Count == 0)
            {
                Model.Variants.Add(CreateDefaultVariant());
                StateHasChanged();
            }

            // Ensure exactly one default variant
            EnsureDefaultVariant();
        }

        protected override async Task OnInitializedAsync()
        {
            await LoadCategories();
        }

        private async Task LoadCategories()
        {
            try
            {
                isLoadingCategories = true;
                StateHasChanged();

                availableCategories = await CategoryService.GetCategoryTreeAsync();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load categories: {ex.Message}");
            }
            finally
            {
                isLoadingCategories = false;
                StateHasChanged();
            }
        }

        private void OnNameChanged(ChangeEventArgs e)
        {
            var name = e.Value?.ToString() ?? string.Empty;
            Model.Name = name;

            var generatedSlug = GenerateSlugFromName(name);

            if (string.IsNullOrEmpty(Model.Slug) || Model.Slug == previousSlug)
            {
                Model.Slug = generatedSlug;
                OnSlugChanged();
            }

            previousSlug = generatedSlug;
            StateHasChanged();
        }

        private void OnSlugChanged(ChangeEventArgs? e = null)
        {
            if (e != null)
            {
                Model.Slug = e.Value?.ToString() ?? string.Empty;
            }

            // Reset validation state
            slugValidationResult = null;

            // Debounce slug validation
            slugValidationTimer?.Dispose();
            slugValidationTimer = new System.Threading.Timer(async _ => await ValidateSlugAsync(), null, 500, Timeout.Infinite);

            StateHasChanged();
        }

        private async Task ValidateSlugAsync()
        {
            if (string.IsNullOrWhiteSpace(Model.Slug))
            {
                return;
            }

            try
            {
                isValidatingSlug = true;
                await InvokeAsync(StateHasChanged);

                // Note: You would need to add this method to IProductService
                // var isValid = await ProductService.ValidateSlugAsync(Model.Slug, IsEditMode ? null : null);
                var isValid = true; // Placeholder - implement validation in service
                slugValidationResult = isValid;
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to validate slug: {ex.Message}");
                slugValidationResult = false;
            }
            finally
            {
                isValidatingSlug = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private string GenerateSlugFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return name.ToLowerInvariant()
              .Replace(" ", "-")
              .Replace("_", "-")
              .Where(c => char.IsLetterOrDigit(c) || c == '-')
              .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
              .ToString()
              .Trim('-');
        }

        private string GetValidationClass(string fieldName)
        {
            return ValidationErrors.ContainsKey(fieldName) ? "border-red-500 dark:border-red-400" : string.Empty;
        }

        private string GetCategoryDisplayName(CategoryTreeDto category)
        {
            var prefix = new string('—', category.Level * 2);
            return $"{prefix} {category.Name}";
        }

        private void ToggleCategory(int categoryId, bool isSelected)
        {
            if (isSelected && !Model.CategoryIds.Contains(categoryId))
            {
                Model.CategoryIds.Add(categoryId);
            }
            else if (!isSelected && Model.CategoryIds.Contains(categoryId))
            {
                Model.CategoryIds.Remove(categoryId);
            }
            StateHasChanged();
        }

        // Variant Management with immediate UI updates
        private void OnHasVariantsChanged(ChangeEventArgs e)
        {
            var hasVariants = (bool)(e.Value ?? false);
            Model.HasVariants = hasVariants;

            if (hasVariants && Model.Variants.Count == 0)
            {
                // Add default variant
                Model.Variants.Add(CreateDefaultVariant());
            }
            else if (!hasVariants && Model.Variants.Count > 1)
            {
                // Keep only the first variant
                Model.Variants = [.. Model.Variants.Take(1)];
                Model.Variants[0].IsDefault = true;
            }

            StateHasChanged();
        }

        private void AddVariant()
        {
            var newVariant = CreateDefaultVariant();
            newVariant.Position = Model.Variants.Count;
            newVariant.Title = $"Variant {Model.Variants.Count + 1}";
            Model.Variants.Add(newVariant);

            // Immediate UI update
            StateHasChanged();

            // Focus on the new variant's title field
            _ = Task.Delay(100).ContinueWith(_ => InvokeAsync(() =>
            {
                // This would focus the newly added variant's title input
                StateHasChanged();
            }));
        }

        private void RemoveVariant(int index)
        {
            if (index >= 0 && index < Model.Variants.Count && Model.Variants.Count > 1)
            {
                var wasDefault = Model.Variants[index].IsDefault;
                Model.Variants.RemoveAt(index);

                // If we removed the default variant, set the first one as default
                if (wasDefault && Model.Variants.Any())
                {
                    Model.Variants[0].IsDefault = true;
                }

                // Update positions
                for (int i = 0; i < Model.Variants.Count; i++)
                {
                    Model.Variants[i].Position = i;
                }

                // Immediate UI update
                StateHasChanged();
            }
        }

        private void SetDefaultVariant(int index)
        {
            if (index >= 0 && index < Model.Variants.Count)
            {
                // Remove default from all variants
                foreach (var variant in Model.Variants)
                {
                    variant.IsDefault = false;
                }

                // Set the selected variant as default
                Model.Variants[index].IsDefault = true;

                // Immediate UI update
                StateHasChanged();
            }
        }

        private void EnsureDefaultVariant()
        {
            if (Model.Variants.Any())
            {
                var defaultVariants = Model.Variants.Where(v => v.IsDefault).ToList();

                if (defaultVariants.Count == 0)
                {
                    Model.Variants.First().IsDefault = true;
                }
                else if (defaultVariants.Count > 1)
                {
                    // Keep only the first default
                    for (int i = 1; i < defaultVariants.Count; i++)
                    {
                        defaultVariants[i].IsDefault = false;
                    }
                }
            }
        }

        private CreateProductVariantDto CreateDefaultVariant()
        {
            return new CreateProductVariantDto
            {
                Title = "Default Title",
                Price = 0,
                Quantity = 0,
                TrackQuantity = true,
                ContinueSellingWhenOutOfStock = false,
                RequiresShipping = Model.RequiresShipping,
                IsTaxable = true,
                Weight = 0,
                WeightUnit = "kg",
                Position = Model.Variants.Count,
                IsDefault = !Model.Variants.Any() // First variant is default
            };
        }

        // Image picker helper methods with immediate UI updates
        private async Task OnProductImagesChanged(List<object> images)
        {
            productImageObjects = images;
            Model.Images = images.Cast<CreateProductImageDto>().ToList();

            // Update positions to ensure proper ordering
            for (int i = 0; i < Model.Images.Count; i++)
            {
                Model.Images[i].Position = i;
            }

            // Immediate UI update
            StateHasChanged();
            await Task.CompletedTask;
        }

        private string GetProductImageUrl(object image)
        {
            if (image is CreateProductImageDto productImage)
            {
                return FileService.GetThumbnailUrl(productImage.FileId);
            }
            return string.Empty;
        }

        private string? GetProductImageAlt(object image)
        {
            if (image is CreateProductImageDto productImage)
            {
                return productImage.Alt;
            }
            return null;
        }

        private bool GetProductImageIsFeatured(object image)
        {
            if (image is CreateProductImageDto productImage)
            {
                return productImage.IsFeatured;
            }
            return false;
        }

        private object CreateProductImageFromFile(FileDto file)
        {
            var newImage = new CreateProductImageDto
            {
                FileId = file.Id,
                Position = Model.Images.Count,
                IsFeatured = !Model.Images.Any() // First image is featured by default
            };

            // Trigger immediate update
            StateHasChanged();

            return newImage;
        }

        private void UpdateProductImage(object image, string? alt, string? caption, bool isFeatured)
        {
            if (image is CreateProductImageDto productImage)
            {
                productImage.Alt = alt;
                productImage.Caption = caption;
                productImage.IsFeatured = isFeatured;

                // Immediate UI update
                StateHasChanged();
            }
        }

        // Variant field change handlers for immediate updates
        private void OnVariantFieldChanged(int index, Action updateAction)
        {
            if (index >= 0 && index < Model.Variants.Count)
            {
                updateAction();
                StateHasChanged();
            }
        }

        private void UpdateVariantTitle(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                Model.Variants[index].Title = e.Value?.ToString() ?? string.Empty;
            });
        }

        private void UpdateVariantPrice(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                if (decimal.TryParse(e.Value?.ToString(), out var price))
                {
                    Model.Variants[index].Price = price;
                }
            });
        }

        private void UpdateVariantQuantity(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                if (int.TryParse(e.Value?.ToString(), out var quantity))
                {
                    Model.Variants[index].Quantity = quantity;
                }
            });
        }

        private void UpdateVariantCompareAtPrice(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                if (decimal.TryParse(e.Value?.ToString(), out var price))
                {
                    Model.Variants[index].CompareAtPrice = price;
                }
                else
                {
                    Model.Variants[index].CompareAtPrice = null;
                }
            });
        }

        private void UpdateVariantBarcode(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                Model.Variants[index].Barcode = e.Value?.ToString();
            });
        }

        private void UpdateVariantWeight(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                if (decimal.TryParse(e.Value?.ToString(), out var weight))
                {
                    Model.Variants[index].Weight = weight;
                }
            });
        }

        private void UpdateVariantWeightUnit(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                Model.Variants[index].WeightUnit = e.Value?.ToString();
            });
        }

        private void UpdateVariantOption1(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                Model.Variants[index].Option1 = e.Value?.ToString();
            });
        }

        private void UpdateVariantOption2(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                Model.Variants[index].Option2 = e.Value?.ToString();
            });
        }

        private void UpdateVariantOption3(int index, ChangeEventArgs e)
        {
            OnVariantFieldChanged(index, () =>
            {
                Model.Variants[index].Option3 = e.Value?.ToString();
            });
        }

        private void ToggleVariantCheckbox(int index, string property, bool value)
        {
            OnVariantFieldChanged(index, () =>
            {
                var variant = Model.Variants[index];
                switch (property)
                {
                    case "TrackQuantity":
                        variant.TrackQuantity = value;
                        break;
                    case "ContinueSellingWhenOutOfStock":
                        variant.ContinueSellingWhenOutOfStock = value;
                        break;
                    case "RequiresShipping":
                        variant.RequiresShipping = value;
                        break;
                    case "IsTaxable":
                        variant.IsTaxable = value;
                        break;
                }
            });
        }

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
        }
    }
}