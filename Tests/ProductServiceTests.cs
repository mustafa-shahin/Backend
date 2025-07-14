
using Xunit;
using Moq;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Services;
using Backend.CMS.Infrastructure.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Linq.Expressions;

public class ProductServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<ProductService>> _loggerMock;
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<ProductService>>();
        _productService = new ProductService(_unitOfWorkMock.Object, _mapperMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void IsAvailable_WhenStatusIsActiveAndVariantQuantityIsGreaterThanZero_ShouldBeTrue()
    {
        // Arrange
        var product = new Product
        {
            Status = ProductStatus.Active,
            Variants = new List<ProductVariant>
            {
                new ProductVariant { Quantity = 10 }
            }
        };

        // Act
        var isAvailable = product.IsAvailable;

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public void IsAvailable_WhenStatusIsActiveAndVariantQuantityIsZero_ShouldBeFalse()
    {
        // Arrange
        var product = new Product
        {
            Status = ProductStatus.Active,
            Variants = new List<ProductVariant>
            {
                new ProductVariant { Quantity = 0 }
            }
        };

        // Act
        var isAvailable = product.IsAvailable;

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public void IsAvailable_WhenStatusIsNotActive_ShouldBeFalse()
    {
        // Arrange
        var product = new Product
        {
            Status = ProductStatus.Draft,
            Variants = new List<ProductVariant>
            {
                new ProductVariant { Quantity = 10 }
            }
        };

        // Act
        var isAvailable = product.IsAvailable;

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task SearchProductsAsync_WhenIsAvailableIsTrue_ShouldReturnOnlyAvailableProducts()
    {
        // Arrange
        var searchDto = new ProductSearchDto { IsAvailable = true };
        var products = new List<Product>
        {
            new Product { Id = 1, Name = "Available Product", Status = ProductStatus.Active, Variants = new List<ProductVariant> { new ProductVariant { Quantity = 1 } } },
            new Product { Id = 2, Name = "Unavailable Product", Status = ProductStatus.Active, Variants = new List<ProductVariant> { new ProductVariant { Quantity = 0 } } },
            new Product { Id = 3, Name = "Draft Product", Status = ProductStatus.Draft, Variants = new List<ProductVariant> { new ProductVariant { Quantity = 1 } } }
        };
        var pagedResult = new PaginatedResult<Product>(products.Where(p => p.IsAvailable).ToList(), 1, 10, 1);

        _unitOfWorkMock.Setup(u => u.Products.SearchProductsPagedAsync(searchDto)).ReturnsAsync(pagedResult);
        _mapperMock.Setup(m => m.Map<List<ProductDto>>(It.IsAny<List<Product>>())).Returns(new List<ProductDto> { new ProductDto { Id = 1, Name = "Available Product" } });

        // Act
        var result = await _productService.SearchProductsAsync(searchDto);

        // Assert
        Assert.Single(result.Data);
        Assert.Equal(1, result.Data.First().Id);
    }

    [Fact]
    public async Task GetProductByIdAsync_WhenProductExists_ShouldReturnProduct()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Test Product" };
        _unitOfWorkMock.Setup(u => u.Products.GetWithDetailsAsync(1)).ReturnsAsync(product);
        _mapperMock.Setup(m => m.Map<ProductDto>(product)).Returns(new ProductDto { Id = 1, Name = "Test Product" });

        // Act
        var result = await _productService.GetProductByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetProductByIdAsync_WhenProductDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.Products.GetWithDetailsAsync(1)).ReturnsAsync((Product)null);

        // Act
        var result = await _productService.GetProductByIdAsync(1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProductAsync_WithValidData_ShouldCreateProduct()
    {
        // Arrange
        var createDto = new CreateProductDto { Name = "New Product", Slug = "new-product" };
        var product = new Product { Id = 1, Name = "New Product", Slug = "new-product" };

        _unitOfWorkMock.Setup(u => u.Products.SlugExistsAsync("new-product", null)).ReturnsAsync(false);
        _mapperMock.Setup(m => m.Map<Product>(createDto)).Returns(product);
        _unitOfWorkMock.Setup(u => u.Products.AddAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.Products.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.Products.GetWithDetailsAsync(1)).ReturnsAsync(product);
        _mapperMock.Setup(m => m.Map<ProductDto>(product)).Returns(new ProductDto { Id = 1, Name = "New Product" });

        // Act
        var result = await _productService.CreateProductAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task UpdateProductAsync_WithValidData_ShouldUpdateProduct()
    {
        // Arrange
        var updateDto = new UpdateProductDto { Name = "Updated Product", Slug = "updated-product", Images = new List<UpdateProductImageDto>() };
        var product = new Product { Id = 1, Name = "Old Product", Slug = "old-product" };
        var productImageRepositoryMock = new Mock<IRepository<ProductImage>>();

        _unitOfWorkMock.Setup(u => u.Products.GetWithDetailsAsync(1)).ReturnsAsync(product);
        _unitOfWorkMock.Setup(u => u.Products.SlugExistsAsync("updated-product", 1)).ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.Products.Update(product));
        _unitOfWorkMock.Setup(u => u.Products.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.Products.GetWithDetailsAsync(1)).ReturnsAsync(product);
        _mapperMock.Setup(m => m.Map<ProductDto>(product)).Returns(new ProductDto { Id = 1, Name = "Updated Product" });
        _unitOfWorkMock.Setup(u => u.GetRepository<ProductImage>()).Returns(productImageRepositoryMock.Object);
        productImageRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductImage, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductImage>());
        _unitOfWorkMock.Setup(u => u.Products.RemoveProductCategoriesAsync(product.Id)).Returns(Task.CompletedTask);

        // Act
        var result = await _productService.UpdateProductAsync(1, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Product", result.Name);
    }

    [Fact]
    public async Task DeleteProductAsync_WhenProductExists_ShouldDeleteProduct()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Test Product" };
        _unitOfWorkMock.Setup(u => u.Products.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _unitOfWorkMock.Setup(u => u.Products.SoftDeleteAsync(product, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _productService.DeleteProductAsync(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CreateProductAsync_WhenSlugExists_ShouldThrowArgumentException()
    {
        // Arrange
        var createDto = new CreateProductDto { Name = "New Product", Slug = "existing-slug" };
        _unitOfWorkMock.Setup(u => u.Products.SlugExistsAsync("existing-slug", null)).ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _productService.CreateProductAsync(createDto));
    }

    [Fact]
    public async Task UpdateProductAsync_WhenProductDoesNotExist_ShouldThrowArgumentException()
    {
        // Arrange
        var updateDto = new UpdateProductDto { Name = "Updated Product", Slug = "updated-product" };
        _unitOfWorkMock.Setup(u => u.Products.GetWithDetailsAsync(1)).ReturnsAsync((Product)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _productService.UpdateProductAsync(1, updateDto));
    }

    [Fact]
    public async Task DeleteProductAsync_WhenProductDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.Products.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Product)null);

        // Act
        var result = await _productService.DeleteProductAsync(1);

        // Assert
        Assert.False(result);
    }
}
