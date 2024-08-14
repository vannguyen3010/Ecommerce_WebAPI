using ECommerceNet8.Data;
using ECommerceNet8.DTOConvertions;
using ECommerceNet8.DTOs.BaseProductDtos.CustomModels;
using ECommerceNet8.DTOs.BaseProductDtos.Request;
using ECommerceNet8.DTOs.BaseProductDtos.Response;
using ECommerceNet8.Models.ProductModels;
using iText.Kernel.Geom;
using Microsoft.EntityFrameworkCore;
using System;

namespace ECommerceNet8.Repositories.BaseProductRepository
{
    public class BaseProductRepository : IBaseProductRepository
    {
        private readonly ApplicationDbContext _db;

        public BaseProductRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<BaseProduct>> GetAllAsync()
        {
            var baseProducts = await _db.BaseProducts
                .Include(bp => bp.MainCategory)
                .Include(bp => bp.Material)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
                .Include(bp => bp.ImageBases)
                .ToListAsync();

            return baseProducts;
        }

        public async Task<IEnumerable<Model_BaseProductCustom>> GetAllWithFullInfoAsync()
        {
            var baseProducts = await _db.BaseProducts
               .Include(bp => bp.MainCategory)
               .Include(bp => bp.Material)
               .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
               .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
               .Include(bp => bp.ImageBases)
               .ToListAsync();

            //DTO'ya dönüştürmece
            //Ana Dizinde DtoConvertions klasöründe bulunan bu converter BaseProduct kümesinin özelliklerini alıyor ve DTO tipine dönüştürüyor.
            //daha fazla DTO'ya dönüştürme görmek için aşağı satırlara inebilirsiniz.
            var CustomBaseProduct = baseProducts.ConvertToDtoListCustomProduct();

            return CustomBaseProduct;
        }

        public async Task<Response_BaseProductWithPaging> GetAllWithFullInfoByPages(int pageNumber, int pageSize)
        {
            Response_BaseProductWithPaging baseProductWithPaging = new Response_BaseProductWithPaging();

            float numberpp = (float)pageSize;
            var totalPages = Math.Ceiling((await GetAllAsync()).Count() / numberpp);
            int totPages = (int)totalPages;

            var baseProducts = await _db.BaseProducts
               .Include(bp => bp.MainCategory)
               .Include(bp => bp.Material)
               .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
               .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
               .Include(bp => bp.ImageBases)
               .Skip((pageNumber - 1)* pageSize)
               .Take(pageSize)
               .ToListAsync();

            //DTO'ya dönüştürmece
            var CustomBaseProducts = baseProducts.ConvertToDtoListCustomProduct();

            baseProductWithPaging.baseProducts = CustomBaseProducts.ToList();
            baseProductWithPaging.TotalPages = totPages;

            return baseProductWithPaging;

        }

        public async Task<Response_BaseProductWithFullInfo> GetByIdWithFullInfo(int baseProductId)
        {
            var existingBaseProduct = await _db.BaseProducts
                .Include(bp => bp.MainCategory)
               .Include(bp => bp.Material)
               .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
               .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
               .Include(bp => bp.ImageBases)
               .FirstOrDefaultAsync(bp=>bp.Id == baseProductId);

            if(existingBaseProduct== null)
            {
                return new Response_BaseProductWithFullInfo()
                {
                    isSuccess = false,
                    Message = "Belirtilen id ile işleşen ürün bulunamadı"
                };
            }
            //DTO'ya dönüştürmece

            var baseProductCustom = existingBaseProduct.ConvertToDtoCustomProduct();

            return new Response_BaseProductWithFullInfo()
            {
                isSuccess = true,
                Message = "Ürün listelendi",
                baseProductCustom = baseProductCustom
            };
        }

        public async Task<Response_BaseProduct> GetByIdWithNoInfo(int baseProductId)
        {
            Response_BaseProduct baseProductResponse = new Response_BaseProduct();

            var existingBaseProduct = await _db.BaseProducts
                .FirstOrDefaultAsync(bp=>bp.Id==baseProductId);

            if(existingBaseProduct== null)
            {
                baseProductResponse.isSuccess = false;
                baseProductResponse.Message = "Belirtilen id ile eşleşen ürün bulunamadı";

                return baseProductResponse;
            }

            //DTO'ya dönüştürmece
            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            baseProductResponse.isSuccess = true;
            baseProductResponse.Message = "Ürün Bulundu";
            baseProductResponse.baseProducts.Add(baseProductWithNoInfo);

            return baseProductResponse;
        }
        public async Task<Response_BaseProduct> AddBaseProduct(Request_BaseProduct baseProduct)
        {
            var baseProductDB = baseProduct.ConvertToBaseProduct();

            await _db.BaseProducts.AddAsync(baseProductDB);
            await _db.SaveChangesAsync();

            //Bil bakalım burada ne oluyor?
            //evet bildin, DTO'ya dönüştürmece
            var baseProductWithNoInfo = baseProductDB.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "Ürün başarıyla eklendi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>()
                {
                    baseProductWithNoInfo
                }
            };
        }

        public async Task<Response_BaseProduct> UpdateBaseProduct(int baseProductId, Request_BaseProduct baseProduct)
        {
            var existingBaseProduct = await _db.BaseProducts.FirstOrDefaultAsync(bp=>bp.Id == baseProductId);
            if(existingBaseProduct != null)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "Belirtilen id ile eşleşen ürün bulunamadı"
                };
            }

            existingBaseProduct.Name = baseProduct.Name;
            existingBaseProduct.Description = baseProduct.Description;
            existingBaseProduct.MainCategoryId = baseProduct.MainCategoryId;
            existingBaseProduct.MaterialId = baseProduct.MaterialId;
            existingBaseProduct.Price = baseProduct.Price;
            existingBaseProduct.Discount = baseProduct.Discount;

            //Bil bakalım burada ne oluyor
            //Bilemedin DTO'ya dönüştürmece değil
            //doğru cevap: Total price hesaplamaca olacaktı
            var totalPrice = (existingBaseProduct.Price - (existingBaseProduct.Price *  existingBaseProduct.Discount/100));
            var totalPriceDecimal = decimal.Round(totalPrice, 2);
            existingBaseProduct.TotalPrice = totalPriceDecimal;

            await _db.SaveChangesAsync();

            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "Ürün güncellendi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>()
                {
                    baseProductWithNoInfo
                }
            };
        }

        public async Task<Response_BaseProduct> UpdateBaseProductDiscount(int baseProductId, Request_BaseProductDiscount baseProductDiscount)
        {
            var existingBaseProduct = await _db.BaseProducts.FirstOrDefaultAsync(bp=>bp.Id == baseProductId);
            if(existingBaseProduct == null)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen ürün bulunamadı"
                };
            }

            if(baseProductDiscount.Discount > 99 || baseProductDiscount.Discount < 0)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "İndirim yüzdesi miktarı %(1-99) arasında olmak zorundadır. Lütfen Kontrol ediniz"
                };
            }

            existingBaseProduct.Discount = baseProductDiscount.Discount;

            //Evet burada da Dto'ya çevirmiyoruz. 
            //total price hesabı yapıyoruz
            // Yeni yorum satırları için sayfayı aşağı kaydırınız...
            decimal totalPrice;
            decimal totalPriceDecimal;

            if(baseProductDiscount.Discount == 0)
            {
                totalPrice = existingBaseProduct.Price;
                totalPriceDecimal = decimal.Round(totalPrice, 2);
            }
            else
            {
                totalPrice = existingBaseProduct.Price - (existingBaseProduct.Price * baseProductDiscount.Discount / 100);
                totalPriceDecimal = decimal.Round(totalPrice, 2);
            }

            existingBaseProduct.TotalPrice = totalPriceDecimal;
            await _db.SaveChangesAsync();

            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "Ürün indirimi başarıyla güncellendi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>()
                {
                    baseProductWithNoInfo
                }
            };
        }

        public async Task<Response_BaseProduct> UpdateBaseProductPrice(int baseProductId, Request_BaseProductPrice baseProductPrice)
        {
            var existingBaseProduct = await _db.BaseProducts.FirstOrDefaultAsync(bp=>bp.Id== baseProductId);
            if(existingBaseProduct == null)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen ürün bulunamadı"
                };
            }

            existingBaseProduct.Price = baseProductPrice.Price;

            decimal totalPriceCalculated;
            //Merhaba sevgili okuyucu
            //burada totalprice yeniden hesaplama işlemi yapıyoruz
            if(existingBaseProduct.Discount == 0)
            {
                var totalPrice = baseProductPrice.Price;
                totalPriceCalculated = decimal.Round(totalPrice, 2);
            }
            else
            {
                var totalPrice = baseProductPrice.Price - (baseProductPrice.Price * existingBaseProduct.Discount / 100);
                totalPriceCalculated = decimal.Round(totalPrice, 2);
            }

            existingBaseProduct.TotalPrice = totalPriceCalculated;
            await _db.SaveChangesAsync();

            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "ürün fiyatı güncellendi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>()
                {
                    baseProductWithNoInfo
                }
            }; ;
        }

        public async Task<Response_BaseProduct> UpdateBaseProductMainCategory(int baseProductId, Request_BaseProductMainCategory baseProductMainCategory)
        {
            var existingBaseProduct = await _db.BaseProducts.FirstOrDefaultAsync(bp=>bp.Id==baseProductId);

            if(existingBaseProduct == null)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen ürün bulunamadı"
                };
            }

            existingBaseProduct.MainCategoryId = baseProductMainCategory.MainCategoryId;
            await _db.SaveChangesAsync();

            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "ürün kategorisi güncellendi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>
                {
                    baseProductWithNoInfo
                }
            };
        }

        public async Task<Response_BaseProduct> UpdateBaseProductMaterial(int baseProductId, Request_BaseProductMaterial baseProductMaterial)
        {
            var existingBaseProduct = await _db.BaseProducts
                .FirstOrDefaultAsync(bp => bp.Id == baseProductId);
            if (existingBaseProduct == null)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "Belirtilen id ile eşleşen ürün bulunamadı"
                };
            }

            existingBaseProduct.MaterialId = baseProductMaterial.MaterialId;
            await _db.SaveChangesAsync();

            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "Materyal Id Başarıyla güncellendi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>()
                {
                    baseProductWithNoInfo
                }
            };
        }

        public async Task<Response_BaseProduct> RemoveBaseProduct(int baseProductId)
        {
            var existingBaseProduct = await _db.BaseProducts.FirstOrDefaultAsync(bp=> bp.Id == baseProductId);
            if(existingBaseProduct == null)
            {
                return new Response_BaseProduct()
                {
                    isSuccess = false,
                    Message = "belirtilen id ile eşleşen ürün bulunamadı"
                };
            }

            _db.BaseProducts.Remove(existingBaseProduct);
            await _db.SaveChangesAsync();

            var baseProductWithNoInfo = existingBaseProduct.ConvertToDtoProductNoInfo();

            return new Response_BaseProduct()
            {
                isSuccess = true,
                Message = "Ürün başarıyla silindi",
                baseProducts = new List<Model_BaseProductWithNoExtraInfo>()
                {
                    baseProductWithNoInfo
                }
            };
        }

        public async Task<IEnumerable<string>> GetProductSearchSuggestions(string searchText)
        {
            var products = await FindProductBySearchText(searchText);
            List<string> searchResult = new List<string>();

            foreach(var product in products)
            {
                if(product.Name.Contains(searchText,StringComparison.OrdinalIgnoreCase))
                {
                    searchResult.Add(product.Name);
                }

                //Merhaba kod dünyasının gezgini,
                //bizim seninle yeşil yorum satırlarında kesişen yollarımız gibi aşağıdaki satırlarda da searchtext'in description ile kesiştiği yerlere değindim
                 //Bu satırları yazan ben, sana sadece bir yazılım parçası bırakmıyorum.Aynı zamanda bir deneyim ve düşünsel bir serüven bırakıyorum.
                 //Önceki yazılımcının yaklaşımı ilham verici olabilir, ancak senin bakış açınla daha da geliştirilebilir.
                //Bana katıl, kodun sadece bir mekanizma değil, aynı zamanda bir konuşma aracı olduğunu düşün.
                //Yorum satırları, bu diyalogun en önemli parçalarıdır. Belki de bu kodu yazarken benimle konuştuğunu hissedeceksin.
                //İşte tam olarak bu yüzden bu satırları yazıyorum.
                //Kodun her bir parçasını dikkatlice incele, çünkü burada sadece bir çözüm değil, aynı zamanda bir düşünce süreci bulacaksın.
                //Belki de benim düşündüğüm gibi bir çözüm yoluna gitmeye karar vereceksin.Belki de bu kodu, kendi projelerin için bir temel olarak kullanacaksın.
                //Ve şimdi, bu kodun ötesinde bir diyaloğa başla.Bu yorum satırları, geçmişteki bir yazılımcının sana seslenişidir.
                //Belki de senin yorum satırların, bir sonraki yazılımcıya ışık tutacak.

                if(product.Description != null)
                {
                    var punctutation = product.Description.Where(char.IsPunctuation).Distinct().ToArray();

                    var words = product.Description.Split().Select(w => w.Trim(punctutation));

                    foreach(var word in words)
                    {
                        if(word.Contains(searchText,StringComparison.OrdinalIgnoreCase)&& !searchResult.Contains(word))
                        {
                            searchResult.Add(word);
                        }
                    }
                }
            }
            return searchResult;
        }

        public async Task<IEnumerable<Model_BaseProductCustom>> GetProductSearch(string searchText)
        {
            var baseProducts = await _db.BaseProducts.Where(bp => bp.Name.ToLower().Contains(searchText.ToLower()) || bp.Description.Contains(searchText.ToLower()))
                .Include(bp => bp.MainCategory)
                .Include(bp => bp.Material)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
                .Include(bp => bp.ImageBases)
                .ToListAsync();

            if(baseProducts == null)
            {
                return null;
            }

            var baseProductCustomReturn = baseProducts.ConvertToDtoListCustomProduct();

            return baseProductCustomReturn;
        }



        public async Task<Response_BaseProductWithPaging> GetProductSearchWithPaging(string searchText, int pageNumber, int pageSize)
        {
            float numberpp = (float)pageSize;
            float currPage = (float)pageNumber;
            var totPages = Math.Ceiling((await GetProductSearch(searchText)).Count() / numberpp);
            var totalPages = (int)totPages;

            var baseProducts = await _db.BaseProducts
                .Where(bp => bp.Name.ToLower().Contains(searchText.ToLower()))
                .Include(bp => bp.MainCategory)
                .Include(bp => bp.Material)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
                .Skip(((int)pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if(baseProducts == null)
            {
                return null;
            }

            var baseProductsDTO = baseProducts.ConvertToDtoListCustomProduct();

            return new Response_BaseProductWithPaging()
            {
                TotalPages = totalPages,
                baseProducts = baseProductsDTO.ToList()
            };

        }



        public async Task<IEnumerable<Model_BaseProductCustom>> SearchProducts(int[] MaterialsIds, int[] mainCategoryIds, int[] productColorIds, int[] productSizeIds)
        {
            IQueryable<BaseProduct> queryBaseProducts = _db.BaseProducts
                .Include(bp => bp.MainCategory)
                .Include(bp => bp.Material)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productColor)
                .Include(bp => bp.productVariants).ThenInclude(pv => pv.productSize)
                .Include(bp => bp.ImageBases);

            if(MaterialsIds.Length > 0)
            {
                queryBaseProducts = queryBaseProducts.Where(bp => MaterialsIds.Contains(bp.MaterialId));
            }
            if(mainCategoryIds.Length >0)
            {
                queryBaseProducts = queryBaseProducts.Where(bp => mainCategoryIds.Contains(bp.MainCategoryId));
            }
            if(productColorIds.Length > 0)
            {
                queryBaseProducts = queryBaseProducts.Where(bp => bp.productVariants.ToList().Any(pv => productColorIds.Contains(pv.ProductColorId)));
            }
            if(productSizeIds.Length > 0)
            {
                queryBaseProducts = queryBaseProducts.Where(bp => bp.productVariants.ToList().Any(pv => productSizeIds.Contains(pv.ProductSizeId)));
            }

            List<BaseProduct> result = await queryBaseProducts.ToListAsync();
            var baseProductCustom = result.ConvertToDtoListCustomProduct();

            return baseProductCustom;
        }

        //PRİVATE FUNCTİONLAR
        private async Task<IEnumerable<BaseProduct>> FindProductBySearchText(string searchText)
        {
            return await _db.BaseProducts.Where(bp=>bp.Name.ToLower().Contains(searchText.ToLower())||bp.Description.ToLower().Contains(searchText.ToLower())).ToListAsync();
        }

 
    }
}
