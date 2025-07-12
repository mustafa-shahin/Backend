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

public class ProductVariantServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<ProductVariantService>> _loggerMock;
    private readonly ProductVariantService _productVariantService;

    public ProductVariantServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<ProductVariantService>>();
        _productVariantService = new ProductVariantService(_unitOfWorkMock.Object, _mapperMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetVariantByIdAsync_WhenVariantExists_ShouldReturnVariant()
    {
        // Arrange
        var variant = new ProductVariant { Id = 1, Title = "Test Variant" };
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(variant);
        _mapperMock.Setup(m => m.Map<ProductVariantDto>(variant)).Returns(new ProductVariantDto { Id = 1, Title = "Test Variant" });

        // Act
        var result = await _productVariantService.GetVariantByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetVariantByIdAsync_WhenVariantDoesNotExist_ShouldThrowException()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariant)null);

        // Act & Assert
        await Assert.ThrowsAsync<System.ArgumentException>(() => _productVariantService.GetVariantByIdAsync(1));
    }

    [Fact]
    public async Task CreateVariantAsync_WhenProductExists_ShouldCreateVariant()
    {
        // Arrange
        var createDto = new CreateProductVariantDto { Title = "New Variant", Images = new List<CreateProductVariantImageDto>() };
        var product = new Product { Id = 1, Name = "Test Product" };
        var variant = new ProductVariant { Id = 1, Title = "New Variant", ProductId = 1 };

        _unitOfWorkMock.Setup(u => u.Products.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _mapperMock.Setup(m => m.Map<ProductVariant>(createDto)).Returns(variant);
        _unitOfWorkMock.Setup(u => u.ProductVariants.AddAsync(variant, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.ProductVariants.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(variant);
        _mapperMock.Setup(m => m.Map<ProductVariantDto>(variant)).Returns(new ProductVariantDto { Id = 1, Title = "New Variant" });
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByProductIdAsync(1)).ReturnsAsync(new List<ProductVariant>());

        // Act
        var result = await _productVariantService.CreateVariantAsync(1, createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task UpdateVariantAsync_WhenVariantExists_ShouldUpdateVariant()
    {
        // Arrange
        var updateDto = new UpdateProductVariantDto { Title = "Updated Variant", Images = new List<UpdateProductVariantImageDto>() };
        var variant = new ProductVariant { Id = 1, Title = "Old Variant" };
        var variantImageRepositoryMock = new Mock<IRepository<ProductVariantImage>>();

        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(variant);
        _unitOfWorkMock.Setup(u => u.ProductVariants.Update(variant));
        _unitOfWorkMock.Setup(u => u.ProductVariants.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mapperMock.Setup(m => m.Map<ProductVariantDto>(variant)).Returns(new ProductVariantDto { Id = 1, Title = "Updated Variant" });
        _unitOfWorkMock.Setup(u => u.GetRepository<ProductVariantImage>()).Returns(variantImageRepositoryMock.Object);
        variantImageRepositoryMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ProductVariantImage, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductVariantImage>());

        // Act
        var result = await _productVariantService.UpdateVariantAsync(1, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Variant", result.Title);
    }

    [Fact]
    public async Task DeleteVariantAsync_WhenVariantExists_ShouldDeleteVariant()
    {
        // Arrange
        var product = new Product { Id = 1, Name = "Test Product" };
        var variant = new ProductVariant { Id = 1, Title = "Test Variant", ProductId = 1, Product = product };
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(variant);
        _unitOfWorkMock.Setup(u => u.ProductVariants.SoftDeleteAsync(variant, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.ProductVariants.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByProductIdAsync(1)).ReturnsAsync(new List<ProductVariant> { variant });
        _unitOfWorkMock.Setup(u => u.Products.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        var result = await _productVariantService.DeleteVariantAsync(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CreateVariantAsync_WhenProductDoesNotExist_ShouldThrowArgumentException()
    {
        // Arrange
        var createDto = new CreateProductVariantDto { Title = "New Variant" };
        _unitOfWorkMock.Setup(u => u.Products.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Product)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _productVariantService.CreateVariantAsync(1, createDto));
    }


    [Fact]
    public async Task UpdateVariantAsync_WhenVariantDoesNotExist_ShouldThrowArgumentException()
    {
        // Arrange
        var updateDto = new UpdateProductVariantDto { Title = "Updated Variant" };
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariant)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _productVariantService.UpdateVariantAsync(1, updateDto));
    }

    [Fact]
    public async Task DeleteVariantAsync_WhenVariantDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _unitOfWorkMock.Setup(u => u.ProductVariants.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariant)null);

        // Act
        var result = await _productVariantService.DeleteVariantAsync(1);

        // Assert
        Assert.False(result);
    }
}
