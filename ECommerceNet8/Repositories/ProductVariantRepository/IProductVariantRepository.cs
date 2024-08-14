using ECommerceNet8.DTOs.ProductVariantDtos.CustomModels;
using ECommerceNet8.DTOs.ProductVariantDtos.Request;
using ECommerceNet8.DTOs.ProductVariantDtos.Response;
using ECommerceNet8.Models.ProductModels;

namespace ECommerceNet8.Repositories.ProductVariantRepository
{
    public interface IProductVariantRepository
    {
        //GET Metodları
        Task<Response_ProductVariantWithObj> GetAllVariantsByBaseProductId(int baseProductId);
        Task<Response_ProductVariantWithObj?> GetProductVariantById(int productVariantId);
        Task<ProductVariant> GetVariantForValidations(int productVariantId);

        //ADD Metodları
        Task<Response_ProductVariantWithoutObj> AddProductVariant(Model_ProductVariantRequest productVariantAdd);

        //Update Metodları
        Task<Response_ProductVariantWithoutObj> UpdateProductVariant(int productVariantId,
           Model_ProductVariantRequest productVariantUpdate);
        Task<Response_ProductVariantWithoutObj> UpdateProductVariantBaseProduct
            (int productVariantId, Request_ProductVariantUpdateBase productVariantUpdateBase);
        Task<Response_ProductVariantWithoutObj> UpdateProductVariantColor(
            int productVariantId, Request_ProductVariantUpdateColor productVariantUpdateColor);
        Task<Response_ProductVariantWithoutObj> UpdateProductVariantSize(
            int productVariantId, Request_ProductVariantUpdateSize productVariantUpdateSize);

        //DELETE Metodları
        Task<Response_ProductVariantWithoutObj> DeleteProductVariant(int productVariantId);


        //KAtegorize edemediğim Metodlar
        Task<Response_ProductVariantWithoutObj> AddQuantity(int productVariantId, int quantity);
        Task<Response_ProductVariantWithoutObj> RemoveQuantity(int productVariantId, int quantity);
        Task<IEnumerable<Response_ProductVariantCheckQty>> HasEnoughItems(
            Request_ProductVariantCheck productVariantToCheck);
        Task<IEnumerable<Response_ProductVariantSizes>> GetProductVariantSizes(
            int productBaseId, int colorId);
        Task<Response_ProductVariantWithObj> GetProductVariantSelection(
            int productBaseId, int colorId, int sizeId);
    }
}
