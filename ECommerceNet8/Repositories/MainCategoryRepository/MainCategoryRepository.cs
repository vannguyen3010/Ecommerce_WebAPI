using AutoMapper;
using ECommerceNet8.Data;
using ECommerceNet8.DTOs.MainCategoryDtos.Request;
using ECommerceNet8.DTOs.MainCategoryDtos.Response;
using ECommerceNet8.Models.ProductModels;
using Microsoft.EntityFrameworkCore;

namespace ECommerceNet8.Repositories.MainCategoryRepository
{
    public class MainCategoryRepository : IMainCategoryRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IMapper _mapper;

        public MainCategoryRepository(ApplicationDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }
        public async Task<Response_MainCategory> GetAllMainCategories()
        {
            var productMainCategories = await _db.MainCategories.ToListAsync();

            if(productMainCategories == null)
            {
                return new Response_MainCategory()
                {
                    isSuccess = false,
                    Message = "Kategori Bulunamadı"
                };
            }

            return new Response_MainCategory()
            {
                isSuccess = true,
                Message = "Tüm Kategoriler",
                mainCategories = productMainCategories
            };
        }

        public async Task<Response_MainCategory> GetMainCategoryById(int mainCategoryId)
        {
            var mainCategory = await _db.MainCategories
                .FirstOrDefaultAsync(mc => mc.Id == mainCategoryId);

            if(mainCategory == null)
            {
                return new Response_MainCategory()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen kategori bulunamadı"
                };
            }

            return new Response_MainCategory()
            {
                isSuccess = true,
                Message = "Kategori bulundu",
                mainCategories = new List<MainCategory>()
                {
                    mainCategory
                }
            };
        }
        public async Task<Response_MainCategory> AddMainCategory(Request_MainCategory mainCategory)
        {
            var MainCategoryBaseModel = _mapper.Map<MainCategory>(mainCategory);
            await _db.MainCategories.AddAsync(MainCategoryBaseModel);
            await _db.SaveChangesAsync();

            return new Response_MainCategory()
            {
                isSuccess = true,
                Message = "Kategori başarıyla eklendi",
                mainCategories = new List<MainCategory>()
                {
                    MainCategoryBaseModel
                }
            };
        }
        public async Task<Response_MainCategory> UpdateMainCategory(int mainCategoryId, Request_MainCategory mainCategory)
        {
            var existingMainCategory = await _db.MainCategories
                .FirstOrDefaultAsync(mc => mc.Id == mainCategoryId);

            if (existingMainCategory == null)
            {
                return new Response_MainCategory()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen kategori bulunamadı"
                };
            }

            existingMainCategory.Name = mainCategory.Name;
            await _db.SaveChangesAsync();

            return new Response_MainCategory()
            {
                isSuccess = true,
                Message = "Kategori başarıyla Güncellendi",
                mainCategories = new List<MainCategory>()
                {
                    existingMainCategory
                }
            };
        }

        public async Task<Response_MainCategory> DeleteMainCategory(int mainCategoryId)
        {
            var existingMainCategory = await _db.MainCategories
                .FirstOrDefaultAsync(mc => mc.Id == mainCategoryId);

            if(existingMainCategory == null)
            {
                return new Response_MainCategory()
                {
                    isSuccess = false,
                    Message = "Belirtilen id ile eşleşen kategori bulunamadı"
                };
            }

            _db.MainCategories.Remove(existingMainCategory);
            await _db.SaveChangesAsync();

            return new Response_MainCategory()
            {
                isSuccess = true,
                Message = "KAtegori başarıyla silindi",
                mainCategories = new List<MainCategory>()
                {
                    existingMainCategory
                }
            };
        }

    }
}
