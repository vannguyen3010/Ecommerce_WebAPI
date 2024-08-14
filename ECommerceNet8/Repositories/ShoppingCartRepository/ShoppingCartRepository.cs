using ECommerceNet8.Data;
using ECommerceNet8.DTOConvertions;
using ECommerceNet8.DTOs.ShoppingCartDtos.Models;
using ECommerceNet8.DTOs.ShoppingCartDtos.Request;
using ECommerceNet8.DTOs.ShoppingCartDtos.Response;
using ECommerceNet8.Models.ShoppingCartModels;
using Microsoft.EntityFrameworkCore;

namespace ECommerceNet8.Repositories.ShoppingCartRepository
{
    public class ShoppingCartRepository : IShoppingCartRepository
    {
        private readonly ApplicationDbContext _db;

        public ShoppingCartRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<Response_ShoppingCart> GetAllCartItems(string userId)
        {
            bool anyCantBeSold = false;
            decimal totalPrice = 0;

            var userCart = await _db.ShoppingCarts
                .Include(sc => sc.CartItems)
                .FirstOrDefaultAsync(sc => sc.ApiUserId == userId);
            if (userCart == null)
            {
                return new Response_ShoppingCart()
                {
                    CanBeSold = false,
                    TotalPrice = 0,
                    Message = "Kullanıcıya ait sepet bulunamadı"
                };
            }

            Response_ShoppingCart response_ShoppingCart = new Response_ShoppingCart();
            response_ShoppingCart.ShoppingCartId = userCart.Id;

            var userCartItems = userCart.CartItems.ToList();
            int shoppingCartId = userCart.Id;

            foreach (var item in userCartItems)
            {
                Model_CartItemReturn cartItemReturn = new Model_CartItemReturn();

                var existingProductVariant = await _db.ProductVariants
                    .Include(pv => pv.productSize)
                    .Include(pv => pv.productColor)
                    .FirstOrDefaultAsync(pv => pv.Id == item.ProductVariantId);

                if (existingProductVariant == null)
                {
                    cartItemReturn.CanBeSold = false;
                    cartItemReturn.ProductVariantId = item.ProductVariantId;
                    cartItemReturn.SelectedQuantity = item.Quantity;
                    cartItemReturn.Message = "Belirtilen id ile eşleşen öğe bulunamadı";
                    response_ShoppingCart.CartItemsCantBeSold.Add(cartItemReturn);

                    anyCantBeSold = true;
                }
                else
                {
                    var baseProduct = await _db.BaseProducts
                        .FirstOrDefaultAsync(bp => bp.Id == existingProductVariant.BaseProductId);

                    //CONVERT TO DTO
                    cartItemReturn = baseProduct.ConvertToDtoCartItem(existingProductVariant, item);

                    if (cartItemReturn.CanBeSold == false)
                    {
                        response_ShoppingCart.CartItemsCantBeSold.Add(cartItemReturn);
                    }
                    else
                    {
                        totalPrice += cartItemReturn.TotalPrice;
                        response_ShoppingCart.CartItemsCanBeSold.Add(cartItemReturn);
                    }

                }
            }

            if (anyCantBeSold == true)
            {
                response_ShoppingCart.CanBeSold = false;
                response_ShoppingCart.Message = "Bazı öğeler satılamaz";
                response_ShoppingCart.TotalPrice = 0;
            }
            else
            {
                response_ShoppingCart.CanBeSold = true;
                response_ShoppingCart.Message = "Tüm öğeler satışa uygun";
                response_ShoppingCart.TotalPrice = totalPrice;
            }

            return response_ShoppingCart;
        }

        public async Task<Response_ShoppingCartInfo> AddItem(string userId, Request_ShoppingCart shoppingCartItem)
        {
            var userCart = await _db.ShoppingCarts
                .Include(sc => sc.CartItems)
                .FirstOrDefaultAsync(sc => sc.ApiUserId == userId);
            if (userCart == null)
            {
                ShoppingCart shoppingCart = new ShoppingCart();
                shoppingCart.ApiUserId = userId;
                await _db.ShoppingCarts.AddAsync(shoppingCart);
                await _db.SaveChangesAsync();

                userCart = await _db.ShoppingCarts
                .Include(sc => sc.CartItems)
                .FirstOrDefaultAsync(sc => sc.ApiUserId == userId);
            }

            var cartItem = new CartItem();
            cartItem.ProductVariantId = shoppingCartItem.ProductVariantId;

            //Ürün varyantlarını getirip kontrol ediyoruz burada
            var productVariant = await _db.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == shoppingCartItem.ProductVariantId);
            if (productVariant == null)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = 0,
                    Message = "Belirtilen id ile eşleşen ürün çeşidi bulunamadı",
                    RequestQty = 0,
                    StoreQty = 0
                };
            }

            //öğenin sepette zaten olup olmadığını kontrol ediyoruz
            var cartItemInDb = await _db.CartItems
                .AnyAsync(ci => ci.ShoppingCartId == userCart.Id
                && ci.ProductVariantId == productVariant.Id);
            if (cartItemInDb == true)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = productVariant.Id,
                    Message = "Öğe halihazırda sepette mevcut",
                    RequestQty = 0,
                    StoreQty = 0
                };
            }
            //Kaç tane kaldığına bakıyoruz
            if (productVariant.Quantity < shoppingCartItem.Quantity)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = productVariant.Id,
                    Message = "Yeterli öğe bulunmamaktadır",
                    RequestQty = shoppingCartItem.Quantity,
                    StoreQty = productVariant.Quantity
                };
            }

            cartItem.Quantity = shoppingCartItem.Quantity;
            cartItem.ShoppingCartId = userCart.Id;

            await _db.CartItems.AddAsync(cartItem);
            await _db.SaveChangesAsync();

            return new Response_ShoppingCartInfo()
            {
                IsSuccess = true,
                ProductVariantId = productVariant.Id,
                Message = "Ürün sepete eklendi",
                RequestQty = shoppingCartItem.Quantity,
                StoreQty = productVariant.Quantity
            };


        }
        public async Task<Response_ShoppingCartInfo> UpdateQty(string userId, Request_ShoppingCart shoppingCartItem)
        {
            //Quantity kontrolü
            if (shoppingCartItem.Quantity <= 0)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = shoppingCartItem.ProductVariantId,
                    Message = "öğe sayısı negatif olamaz. icabında öğeyi kaldırınız.",
                    RequestQty = shoppingCartItem.Quantity,
                    StoreQty = 0
                };
            }
            //sepettekileri get yapıp kontrol ediyoruz
            var userCart = await _db.ShoppingCarts
                .Include(sc => sc.CartItems)
                .FirstOrDefaultAsync(sc => sc.ApiUserId == userId);

            var productVariant = await _db.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == shoppingCartItem.ProductVariantId);

            var cartItem = await _db.CartItems
                .Where(ci => ci.ShoppingCartId == userCart.Id
                && ci.ProductVariantId == shoppingCartItem.ProductVariantId)
                .FirstOrDefaultAsync();

            if (cartItem == null || productVariant == null)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = shoppingCartItem.ProductVariantId,
                    Message = "Belirtilen id ile eşleşen öğe bulunamadı",
                    RequestQty = shoppingCartItem.Quantity,
                    StoreQty = 0
                };
            }
            //sepetteki ürünlerin stoktakilerden fazla olmaması gerekiyor.
            //açıkçası bu kontrolü yapmak benim aklıma gelmezdi.
            if (shoppingCartItem.Quantity > productVariant.Quantity)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = productVariant.Id,
                    Message = "Stokta yeterli miktar yok.",
                    RequestQty = shoppingCartItem.Quantity,
                    StoreQty = productVariant.Quantity
                };
            }
            
            cartItem.Quantity = shoppingCartItem.Quantity;
            await _db.SaveChangesAsync();

            return new Response_ShoppingCartInfo()
            {
                IsSuccess = true,
                ProductVariantId = productVariant.Id,
                Message = "Adet bilgisi güncellendi",
                RequestQty = shoppingCartItem.Quantity,
                StoreQty = productVariant.Quantity
            };

        }
        public async Task<Response_ShoppingCartInfo> RemoveItem(string userId, Request_ShoppingCart shoppingCartItem)
        {
            var shoppingCart = await _db.ShoppingCarts
                .FirstOrDefaultAsync(sc => sc.ApiUserId == userId);

            var cartItem = await _db.CartItems
                .FirstOrDefaultAsync(ci => ci.ShoppingCartId == shoppingCart.Id
                && ci.ProductVariantId == shoppingCartItem.ProductVariantId);
            if (cartItem == null)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = 0,
                    Message = "sepette beliritlen id'ye sahip oğe bulunmamaktadır",
                    RequestQty = shoppingCartItem.Quantity,
                    StoreQty = 0
                };
            }

            _db.CartItems.Remove(cartItem);
            await _db.SaveChangesAsync();

            return new Response_ShoppingCartInfo()
            {
                IsSuccess = true,
                ProductVariantId = cartItem.ProductVariantId,
                Message = "öğe kaldırıldı",
                RequestQty = 0,
                StoreQty = 0,
            };
        }

        public async Task<Response_ShoppingCartInfo> ClearCart(string userId)
        {
            var shoppingCart = await _db.ShoppingCarts
                .Include(sc => sc.CartItems)
                .FirstOrDefaultAsync(sc => sc.ApiUserId == userId);

            if (shoppingCart == null)
            {
                return new Response_ShoppingCartInfo()
                {
                    IsSuccess = false,
                    ProductVariantId = 0,
                    Message = "Kullanıcının sepeti yoktur",
                    RequestQty = 0,
                    StoreQty = 0,
                };
            }


            foreach (var cartItem in shoppingCart.CartItems)
            {
                _db.CartItems.Remove(cartItem);
            }

            await _db.SaveChangesAsync();

            return new Response_ShoppingCartInfo()
            {
                IsSuccess = true,
                ProductVariantId = 0,
                Message = "Sepet boşaltıldı",
                RequestQty = 0,
                StoreQty = 0,
            };
        }
    }
}