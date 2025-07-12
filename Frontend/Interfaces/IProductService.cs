using Backend.CMS.Application.DTOs;
using System.Threading.Tasks;

namespace Frontend.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<ProductListDto>> GetProductsAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null, ProductStatus? status = null, ProductType? type = null, List<int>? categoryIds = null, bool? hasVariants = null, bool? isAvailable = null, string? vendor = null);
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<ProductDto?> CreateProductAsync(CreateProductDto createProductDto);
        Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto updateProductDto);
        Task<bool> DeleteProductAsync(int id);
        Task<bool> ValidateSlugAsync(string slug, int? excludeId = null);
    }
}