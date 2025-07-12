using Backend.CMS.Application.DTOs;
using System.Threading.Tasks;

namespace Frontend.Interfaces
{
    public interface IProductVariantService
    {
        Task<PagedResult<ProductVariantDto>> GetProductVariantsAsync(int pageNumber = 1, int pageSize = 10, int? productId = null, string? searchTerm = null);
        Task<ProductVariantDto?> GetProductVariantByIdAsync(int id);
        Task<ProductVariantDto?> CreateProductVariantAsync(CreateProductVariantDto createProductVariantDto);
        Task<ProductVariantDto?> UpdateProductVariantAsync(int id, UpdateProductVariantDto updateProductVariantDto);
        Task<bool> DeleteProductVariantAsync(int id);
    }
}