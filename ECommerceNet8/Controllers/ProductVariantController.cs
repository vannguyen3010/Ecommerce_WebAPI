﻿using ECommerceNet8.DTOs.ProductVariantDtos.CustomModels;
using ECommerceNet8.DTOs.ProductVariantDtos.Request;
using ECommerceNet8.DTOs.ProductVariantDtos.Response;
using ECommerceNet8.Repositories.ProductVariantRepository;
using ECommerceNet8.Repositories.ValidationsRepository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceNet8.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductVariantController : ControllerBase
    {
        private readonly IProductVariantRepository _productVariantRepository;
        private readonly IValidationRepository _validationRepository;

        public ProductVariantController(IProductVariantRepository productVariantRepository,
            IValidationRepository validationRepository)
        {
            _productVariantRepository = productVariantRepository;
            _validationRepository = validationRepository;
        }

        [HttpGet]
        [Route("GetAllProductVariantsByBaseProductId/{baseProductId}")]
        public async Task<ActionResult<Response_ProductVariantWithObj>>
            GetAllVariantsByBaseProductId([FromRoute] int baseProductId)
        {
            var productVariantResponse = await _productVariantRepository
                .GetAllVariantsByBaseProductId(baseProductId);
            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }

            return Ok(productVariantResponse);
        }

        [HttpGet]
        [Route("GetVariantById/{productVariantId}")]
        [ActionName("GetVariantById")]
        public async Task<ActionResult<Response_ProductVariantWithObj>> GetVariantById
            ([FromRoute] int productVariantId)
        {
            var productVariantResponse = await _productVariantRepository
                .GetProductVariantById(productVariantId);

            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }
            return Ok(productVariantResponse);
        }

        [HttpPost]
        [Route("AddProductVariant")]
        public async Task<IActionResult> AddProductVariant([FromBody]
        Model_ProductVariantRequest productVariant)
        {
            var response = await CheckProductVariantExist(productVariant.BaseProductId,
                productVariant.ProductColorId, productVariant.ProductSizeId);
            if (response == true)
            {
                return BadRequest("Ürün varyantı halihazırda mevcut");
            }
            //OTHER VALIDATIONS

            var baseProductCheck = await CheckProductBaseExist(productVariant.BaseProductId);
            if (baseProductCheck == false)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün bulunamadı");
            }
            var colorCheck = await CheckProductColorExist(productVariant.ProductColorId);
            if (colorCheck == false)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün rengi bulunamadı");
            }
            var sizeCheck = await CheckProductSizeExist(productVariant.ProductSizeId);
            if (sizeCheck == false)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün bedeni bulunamadı");
            }


            var productVariantResponse = await _productVariantRepository
                .AddProductVariant(productVariant);

            return CreatedAtAction(nameof(GetVariantById),
                new { productVariantId = productVariantResponse.ProductVariantWithoutObj[0].Id },
                productVariantResponse.ProductVariantWithoutObj[0]);
        }

        [HttpPut]
        [Route("UpdateProductVariant/{productVariantId}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>>
            UpdateProductVariant([FromRoute] int productVariantId,
            [FromBody] Model_ProductVariantRequest productVariant)
        {
            var response = await CheckProductVariantExist(
                productVariant.BaseProductId, productVariant.ProductColorId,
                productVariant.ProductSizeId);
            if (response == true)
            {
                return BadRequest("Ürün varyantı halihazırda mevcut");
            }

            var productVariantResponse = await _productVariantRepository
                .UpdateProductVariant(productVariantId, productVariant);

            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }
            return Ok(productVariantResponse);
        }

        [HttpPut]
        [Route("UpdateProductVariantBaseProduct/{productVariantId}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>>
            UpdateProductVariantBase([FromRoute] int productVariantId,
            [FromBody] Request_ProductVariantUpdateBase productVariantUpdateBase)
        {
            var existingProductVariant = await _productVariantRepository
                .GetVariantForValidations(productVariantId);
            if (existingProductVariant == null)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün varyantı bulunamadı");
            }

            var response = await CheckProductVariantExist(productVariantUpdateBase.BaseProductId,
                existingProductVariant.ProductColorId, existingProductVariant.ProductSizeId);
            if (response == true)
            {
                return BadRequest("Ürün varyantı halihazırda mevcut");
            }

            var baseProductExist = await
                CheckProductBaseExist(productVariantUpdateBase.BaseProductId);

            if (baseProductExist == false)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün bulunamadı");
            }

            var productVariantResponse = await _productVariantRepository
                .UpdateProductVariantBaseProduct(productVariantId, productVariantUpdateBase);

            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }

            return Ok(productVariantResponse);
        }

        [HttpPut]
        [Route("UpdateProductVariantColor/{productVariantId}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>>
            UpdateProductVariantColor([FromRoute] int productVariantId,
            [FromBody] Request_ProductVariantUpdateColor productVariantColor)
        {
            var existingProductVariant = await _productVariantRepository
                .GetVariantForValidations(productVariantId);
            if (existingProductVariant == null)
            {
                return BadRequest("Belirtilen id ile eşleşen Ürün varyantı bulunamadı");
            }

            var checkResponse = await CheckProductVariantExist(existingProductVariant.BaseProductId,
                productVariantColor.ColorId, existingProductVariant.ProductSizeId);
            if (checkResponse == true)
            {
                return BadRequest("Ürün varyantı halihazırda mevcut");
            }
            var productColorResponse = await CheckProductColorExist(productVariantColor.ColorId);
            if (productColorResponse == false)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün rengi bulunamadı");
            }

            var productVariantResponse = await _productVariantRepository
                .UpdateProductVariantColor(productVariantId, productVariantColor);

            if (productVariantResponse.isSuccess == false)
            {
                return BadRequest(productVariantResponse);
            }

            return Ok(productVariantResponse);
        }

        [HttpPut]
        [Route("UpdateProductVariantSize/{productVariantId}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>>
            UpdateProductVariantSize([FromRoute] int productVariantId,
            [FromBody] Request_ProductVariantUpdateSize productVariantSize)
        {
            var existingProductVariant = await _productVariantRepository
                .GetVariantForValidations(productVariantId);
            if (existingProductVariant == null)
            {
                return BadRequest("Belirtilen id ile eşleşen ürün varyantı bulunamadı");
            }

            var checkResponse = await CheckProductVariantExist(
                existingProductVariant.BaseProductId,
                existingProductVariant.ProductColorId,
                productVariantSize.SizeId);
            if (checkResponse == true)
            {
                return BadRequest("Ürün varyantı halihazırda mevcut");
            }

            var productSizeExist = await CheckProductSizeExist(productVariantSize.SizeId);
            if (productSizeExist == false)
            {
                return NotFound("Belirtilen id ile eşleşen ürün bedeni bulunamadı");
            }

            var productVariantResponse = await _productVariantRepository
                .UpdateProductVariantSize(productVariantId, productVariantSize);
            if (productVariantResponse.isSuccess == false)
            {
                return BadRequest(productVariantResponse);
            }

            return Ok(productVariantResponse);
        }

        [HttpDelete]
        [Route("DeleteProductVariant/{productVariantId}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>>
            DeleteProductVariant([FromRoute] int productVariantId)
        {
            var productVariantResponse = await _productVariantRepository
                .DeleteProductVariant(productVariantId);

            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }
            return Ok(productVariantResponse);
        }

        [HttpPut]
        [Route("AddQuantity/{productVariantId}/{quantity}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>> AddQuantity
            ([FromRoute] int productVariantId, [FromRoute] int quantity)
        {
            var productVariantResponse = await _productVariantRepository
                .AddQuantity(productVariantId, quantity);

            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }

            return Ok(productVariantResponse);
        }
        [HttpPut]
        [Route("RemoveQuantity/{productVariantId}/{quantity}")]
        public async Task<ActionResult<Response_ProductVariantWithoutObj>> RemoveQuantity
            ([FromRoute] int productVariantId, [FromRoute] int quantity)
        {

            var productVariantResponse = await _productVariantRepository
                .RemoveQuantity(productVariantId, quantity);

            if (productVariantResponse.isSuccess == false)
            {
                return NotFound(productVariantResponse);
            }
            return Ok(productVariantResponse);
        }

        [HttpPost]
        [Route("HasEnoughItems")]
        public async Task<ActionResult<Response_ProductVariantCheckQty>> HasEnoughItems
            ([FromBody] Request_ProductVariantCheck productVariantToCheck)
        {
            var response = await _productVariantRepository
                .HasEnoughItems(productVariantToCheck);

            return Ok(response);
        }

        [HttpGet]
        [Route("GetProductSizes/{productBaseId}/{productColorId}")]
        public async Task<ActionResult<IEnumerable<Response_ProductVariantSizes>>>
            GetProductVariantSizes([FromRoute] int productBaseId, [FromRoute] int productColorId)
        {
            var productVariantSizes = await _productVariantRepository
                .GetProductVariantSizes(productBaseId, productColorId);

            if (productVariantSizes == null)
            {
                return NotFound("Beden bulunamadı");
            }

            return Ok(productVariantSizes);
        }



        #region HelperFunctions
        private async Task<bool> CheckProductVariantExist(int productBaseId,
            int productColorId, int productSizeId)
        {
            var response = await _validationRepository.ValidateProductVariant
                (productBaseId, productColorId, productSizeId);

            return response;
        }
        private async Task<bool> CheckProductBaseExist(int productBaseId)
        {
            var response = await _validationRepository.ValidateBaseProductId(productBaseId);

            return response;
        }

        private async Task<bool> CheckProductColorExist(int colorId)
        {
            var response = await _validationRepository.ValidateColorId(colorId);
            return response;
        }
        private async Task<bool> CheckProductSizeExist(int sizeId)
        {
            var response = await _validationRepository.ValidateSizeId(sizeId);

            return response;
        }
        #endregion
    }
}
