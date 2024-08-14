﻿using ECommerceNet8.DTOs.BaseProductDtos.CustomModels;
using ECommerceNet8.DTOs.BaseProductDtos.Request;
using ECommerceNet8.DTOs.BaseProductDtos.Response;
using ECommerceNet8.Models.ProductModels;
using ECommerceNet8.Repositories.BaseProductRepository;
using ECommerceNet8.Repositories.ValidationsRepository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceNet8.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaseProductController : ControllerBase
    {
        private readonly IBaseProductRepository _baseProductRepository;
        private readonly IValidationRepository _validationRepository;

        public BaseProductController(IBaseProductRepository baseProductRepository, IValidationRepository validationRepository)
        {
            _baseProductRepository = baseProductRepository;
            _validationRepository = validationRepository;
        }

        [HttpGet]
        [Route("GetAllAsync")]
        public async Task<ActionResult<IEnumerable<BaseProduct>>> GetAllAsync()
        {
            var baseProducts = await _baseProductRepository.GetAllAsync();

            return Ok(baseProducts);
        }

        [HttpGet]
        [Route("GetAllWithFullInfoAsync")]
        public async Task<ActionResult<IEnumerable<Model_BaseProductCustom>>> GetAllWithFullInfoAsync()
        {
            var baseProducts = await _baseProductRepository.GetAllWithFullInfoAsync();

            return Ok(baseProducts);
        }

        [HttpGet]
        [Route("GetAllPages/{pageNumber}/{pageSize}")]
        public async Task<ActionResult<Response_BaseProductWithPaging>> GetAllPaged([FromRoute]int pageNumber, [FromRoute]int pageSize)
        {
            var baseProductPaged = await _baseProductRepository.GetAllWithFullInfoByPages(pageNumber, pageSize);

            return Ok(baseProductPaged);
        }

        [HttpGet]
        [Route("GetByIdWithNoInfo/{baseProductId}")]
        [ActionName("GetByIdNoInfo")]
        public async Task<ActionResult<Response_BaseProduct>> GetByIdNoInfo([FromRoute]int baseProductId)
        {
            var baseProductResponse = await _baseProductRepository.GetByIdWithNoInfo(baseProductId);
            if(baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);
        }

        [HttpGet]
        [Route("GetByIdFullInfo/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProductWithFullInfo>> GetByIdFullInfo([FromRoute]int baseProductId)
        {
            var baseProductResponse = await _baseProductRepository.GetByIdWithFullInfo(baseProductId);
            if( baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);
        }

        [HttpPost]
        [Route("AddBaseProduct")]
        public async Task<IActionResult> AddBaseProduct([FromBody]Request_BaseProduct baseProduct)
        {
            var checkMaterial = await _validationRepository.ValidateMaterialId(baseProduct.MaterialId);
            if(checkMaterial == false)
            {
                return BadRequest("belirtilen id ile eşleşen materyal bulunamadı");
            }

            var checkMainCategory = await _validationRepository.ValidateMainCategoryId(baseProduct.MainCategoryId);
            if( checkMainCategory == false)
            {
                return BadRequest("belirtilen id ile eşleşen kategori bulunamadı");
            }
            var baseProductResponse = await _baseProductRepository.AddBaseProduct(baseProduct);

            return CreatedAtAction(nameof(GetByIdNoInfo),
                new { baseProductId = baseProductResponse.baseProducts[0].Id },
                baseProductResponse.baseProducts[0]);
        }

        [HttpPut]
        [Route("UpdateBaseProduct/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProduct>> UpdateBaseProduct([FromRoute]int baseProductId, [FromBody]Request_BaseProduct baseProduct)
        {
            var checkMaterial = await _validationRepository.ValidateMaterialId(baseProduct.MaterialId);
            if (checkMaterial == false)
            {
                return BadRequest("belirtilen id ile eşleşen materyal bulunamadı");
            }

            var checkMainCategory = await _validationRepository.ValidateMainCategoryId(baseProduct.MainCategoryId);
            if (checkMainCategory == false)
            {
                return BadRequest("belirtilen id ile eşleşen kategori bulunamadı");
            }

            var baseProductResponse = await _baseProductRepository.UpdateBaseProduct(baseProductId, baseProduct);

            if(baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);
        }

        [HttpPut]
        [Route("UpdateBaseProductPrice/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProduct>> UpdateBaseProductPrice([FromRoute]int baseProductId, [FromBody]Request_BaseProductPrice productPrice)
        {
            var baseProductResponse = await _baseProductRepository.UpdateBaseProductPrice(baseProductId, productPrice);

            if(baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);
        }

        [HttpPut]
        [Route("UpdateBaseProductDiscount/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProduct>> UpdateBaseProductDiscount([FromRoute]int baseProductId, [FromBody]Request_BaseProductDiscount productDiscount)
        {
            var baseProductResponse = await _baseProductRepository.UpdateBaseProductDiscount(baseProductId, productDiscount);

            if(baseProductResponse.isSuccess == false) { return NotFound(baseProductResponse); }
            return Ok(baseProductResponse);
        }

        [HttpPut]
        [Route("UpdateBaseProductMainCategory/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProduct>> UpdateBaseProductMainCategory([FromRoute]int baseProductId, [FromBody]Request_BaseProductMainCategory mainCategory)
        {
            var checkMainCategory = await _validationRepository.ValidateMainCategoryId(mainCategory.MainCategoryId);
            if (checkMainCategory == false)
            {
                return BadRequest("belirtilen id ile eşleşen kategori bulunamadı");
            }

            var baseProductResponse = await _baseProductRepository.UpdateBaseProductMainCategory(baseProductId, mainCategory);

            if(baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);

        }

        [HttpPut]
        [Route("UpdateBaseProductMaterial/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProduct>> UpdateBaseProductMaterial
           ([FromRoute] int baseProductId, [FromBody] Request_BaseProductMaterial material)
        {

            var checkMaterial = await _validationRepository
                .ValidateMaterialId(material.MaterialId);
            if (checkMaterial == false)
            {
                return BadRequest("Belirtilen id ile eşeleşen materyal bulunamadı");
            }

            var baseProductResponse = await _baseProductRepository
                .UpdateBaseProductMaterial(baseProductId, material);
            if (baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);
        }

        [HttpDelete]
        [Route("RemoveBaseProduct/{baseProductId}")]
        public async Task<ActionResult<Response_BaseProduct>> RemoveBaseProduct
            ([FromRoute] int baseProductId)
        {
            var baseProductResponse = await _baseProductRepository
                .RemoveBaseProduct(baseProductId);
            if (baseProductResponse.isSuccess == false)
            {
                return NotFound(baseProductResponse);
            }
            return Ok(baseProductResponse);
        }

        [HttpGet]
        [Route("GetProductSearchSuggestions/{searchText}")]
        public async Task<ActionResult<List<string>>> GetProductSearchSuggestions([FromRoute] string searchText)
        {
            var searchResponse = await _baseProductRepository.GetProductSearchSuggestions(searchText);

            return Ok(searchResponse);
        }

        [HttpGet]
        [Route("GetproductSearch/{searchText}")]
        public async Task<ActionResult<IEnumerable<Model_BaseProductCustom>>> GetProductSearch([FromRoute]string searchText)
        {
            var productResponse = await _baseProductRepository.GetProductSearch(searchText);

            return Ok(productResponse);
        }

        [HttpGet]
        [Route("GetProductSearchWithPaging/{searchText}/{pageNumber}/{pageSize}")]
        public async Task<ActionResult<Response_BaseProductWithPaging>> GetProductSearchWithPaging([FromRoute]string searchText, [FromRoute]int pageNumber, [FromRoute]int pageSize)
        {
            var BaseProductResponse = await _baseProductRepository.GetProductSearchWithPaging(searchText, pageNumber, pageSize);

            if(BaseProductResponse == null)
            {
                return NotFound(BaseProductResponse);
            }

            return Ok(BaseProductResponse);
        }

        [HttpGet]
        [Route("SearchProducts")]
        public async Task<ActionResult<IEnumerable<Model_BaseImageCustom>>> SearchProducts([FromQuery] int[]MaterialIds, [FromQuery] int[]MainCategoriesIds, [FromQuery] int[]ProductColorIds, [FromQuery] int[]ProductSizeIds)
        {
            int[] MaterialIntIds = MaterialIds;
            int[] MainCategoryIntIds = MainCategoriesIds;
            int[] ProductColorIntIds = ProductColorIds;
            int[] ProductSizeIntIds = ProductSizeIds;

            var searchResult = await _baseProductRepository.SearchProducts(MaterialIntIds, MainCategoryIntIds, ProductColorIntIds, ProductSizeIntIds);

            if(searchResult == null)
            {
                return NotFound("Verilen parametrelere uygun ürün bulunamadı");
            }

            return Ok(searchResult);
        }

    }
}
