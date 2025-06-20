﻿namespace Backend.CMS.Application.DTOs
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public int? ParentCategoryId { get; set; }
        public string? ParentCategoryName { get; set; }
        public List<CategoryDto> SubCategories { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public int ProductCount { get; set; }
        public List<CategoryImageDto> Images { get; set; } = new();
        public string? FeaturedImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public int? ParentCategoryId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public List<CreateCategoryImageDto> Images { get; set; } = new();
    }

    public class UpdateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public int? ParentCategoryId { get; set; }
        public bool IsActive { get; set; }
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public List<UpdateCategoryImageDto> Images { get; set; } = new();
    }

    public class CategoryTreeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
        public List<CategoryTreeDto> Children { get; set; } = new();
        public int ProductCount { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? FeaturedImageUrl { get; set; }
    }

    public class CategorySearchDto
    {
        public string? SearchTerm { get; set; }
        public int? ParentCategoryId { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsVisible { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "Name";
        public string SortDirection { get; set; } = "Asc";
    }

    public class CategoryImageDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
    }

    public class CreateCategoryImageDto
    {
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; } = 0;
        public bool IsFeatured { get; set; } = false;
    }

    public class UpdateCategoryImageDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
    }
}