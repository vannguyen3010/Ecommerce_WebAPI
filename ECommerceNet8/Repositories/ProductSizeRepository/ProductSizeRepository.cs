using AutoMapper;
using ECommerceNet8.Data;
using ECommerceNet8.DTOs.ProductSizeDtos.Request;
using ECommerceNet8.DTOs.ProductSizeDtos.Response;
using ECommerceNet8.Models.ProductModels;
using Microsoft.EntityFrameworkCore;

namespace ECommerceNet8.Repositories.ProductSizeRepository
{
    public class ProductSizeRepository : IProductSizeRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IMapper _mapper;

        public ProductSizeRepository(ApplicationDbContext db,
            IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<Response_ProductSize> GetAllProductSizes()
        {
            var productSizes = await _db.ProductSizes.ToListAsync();
            if (productSizes == null)
            {
                return new Response_ProductSize()
                {
                    isSuccess = false,
                    Message = "Ürün bedeni bulunamadı"
                };
            }
            return new Response_ProductSize()
            {
                isSuccess = true,
                Message = "Tüm ürün bedenleri listelendi",
                ProductSizes = productSizes
            };
        }

        public async Task<Response_ProductSize> GetProductSizeById(int productSizeId)
        {
            var productSize = await _db.ProductSizes.FirstOrDefaultAsync(ps => ps.Id == productSizeId);
            if (productSize == null)
            {
                return new Response_ProductSize()
                {
                    isSuccess = false,
                    Message = "Belirtilen id ile eşleşen ürün bedeni bulunamadı"
                };
            }
            return new Response_ProductSize()
            {
                isSuccess = true,
                Message = "beden başarıyla listelendi",
                ProductSizes = new List<ProductSize>()
                {
                    productSize
                }
            };
        }
        public async Task<Response_ProductSize> AddProductSize(Request_ProductSize productSize)
        {
            var productSizeBase = _mapper.Map<ProductSize>(productSize);

            await _db.ProductSizes.AddAsync(productSizeBase);
            await _db.SaveChangesAsync();

            return new Response_ProductSize()
            {
                isSuccess = true,
                Message = "Ürün bedeni başarıyla eklendi",
                ProductSizes = new List<ProductSize>()
                {
                    productSizeBase
                }
            };
        }
        public async Task<Response_ProductSize> UpdateProductSize(int productSizeId, Request_ProductSize productSize)
        {
            var existingProductSize = await _db.ProductSizes.FirstOrDefaultAsync(ps => ps.Id == productSizeId);
            if (existingProductSize == null)
            {
                return new Response_ProductSize()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen ürün bedeni bulunamadı"
                };
            }
            existingProductSize.Name = productSize.Name;
            await _db.SaveChangesAsync();

            return new Response_ProductSize()
            {
                isSuccess = true,
                Message = "ürün bedeni güncellendi",
                ProductSizes = new List<ProductSize>()
                {
                    existingProductSize
                }
            };
        }

        public async Task<Response_ProductSize> DeleteProductSize(int productSizeId)
        {
            var existingProductSize = await _db.ProductSizes.FirstOrDefaultAsync(ps => ps.Id == productSizeId);
            if (existingProductSize == null)
            {
                return new Response_ProductSize()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen ürün bedeni bulunamadı"
                };
            }
            _db.ProductSizes.Remove(existingProductSize);
            await _db.SaveChangesAsync();

            return new Response_ProductSize()
            {
                isSuccess = true,
                Message = "Ürün bedeni başarıyla silindi",
                ProductSizes = new List<ProductSize>()
                {
                    existingProductSize
                }
            };
        }
    }
}
