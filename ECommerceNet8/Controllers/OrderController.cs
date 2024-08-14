﻿using ECommerceNet8.DTOs.OrderDtos.Request;
using ECommerceNet8.DTOs.OrderDtos.Response;
using ECommerceNet8.DTOs.RefundRequestDtos.Request;
using ECommerceNet8.DTOs.RefundRequestDtos.Response;
using ECommerceNet8.DTOs.RequestExchangeDtos.Request;
using ECommerceNet8.DTOs.RequestExchangeDtos.Response;
using ECommerceNet8.Models.OrderModels;
using ECommerceNet8.Repositories.OrderRepository;
using ECommerceNet8.Repositories.RefundRepository;
using ECommerceNet8.Repositories.ReturnExchangeRequestRepository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ECommerceNet8.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IReturnExchangeRequestRepository _returnExchangeRequestRepository;
        private readonly IRefundRepository _refundRepository;

        public OrderController(IOrderRepository orderRepository,
           IReturnExchangeRequestRepository returnExchangeRequestRepository,
           IRefundRepository refundRepository)
        {
            _orderRepository = orderRepository;
            _returnExchangeRequestRepository = returnExchangeRequestRepository;
            _refundRepository = refundRepository;
        }


        [HttpGet]
        [Route("GetAllOrders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
        {
            var orders = await _orderRepository.GetAllOrders();

            return Ok(orders);
        }

        [HttpGet]
        [Route("GetNotSentOrders")]
        public async Task<ActionResult<IEnumerable<Order>>> GetNotSentOrders()
        {
            var orders = await _orderRepository.GetNotSentOrders();

            return Ok(orders);
        }

        [HttpPost]
        [Route("MarkOrderAsSent/{orderId}")]
        public async Task<ActionResult<Response_Order>> MarkOrderAsSent(
            [FromRoute] int orderId)
        {
            var orderResponse = await _orderRepository.MarkOrderAsSent(orderId);
            if (orderResponse.isSuccess == false)
            {
                return BadRequest(orderResponse);
            }

            return Ok(orderResponse);
        }

        [HttpPost]
        [Route("MarkOrderAsNotSent/{orderId}")]
        public async Task<ActionResult<Response_Order>> MarkOrderAsNotSent(
            [FromRoute] int orderId)
        {
            var orderResponse = await _orderRepository.MarkOrderAsNotSent(orderId);
            if (orderResponse.isSuccess == false)
            {
                return BadRequest(orderResponse);
            }
            return Ok(orderResponse);
        }

        [HttpGet]
        [Route("GetOrder/{uniqueOrderIdentifier}")]
        public async Task<ActionResult<Order>> GetOrder(
            [FromRoute] string uniqueOrderIdentifier)
        {
            var order = await _orderRepository.GetOrder(uniqueOrderIdentifier);
            if (order == null)
            {
                return NotFound("Belirtilen id ile eşleşen order bulunamadı");
            }

            return Ok(order);
        }

        [HttpPost]
        [Route("GetOrdersByDate")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByDate(
            [FromBody] Request_OrderDate orderDate)
        {
            var orders = await _orderRepository.GetAllOrderByDate(orderDate);


            if (orders.Count() <= 0)
            {
                return BadRequest("Sipariş bulunamadı");
            }

            return Ok(orders);
        }

        [HttpGet]
        [Route("GetItemsAtCustomer/{orderUniqueIdentifier}")]
        public async Task<ActionResult<Response_ItemsAtCustomer>> GetItemsAtCustomer(
            [FromRoute] string orderUniqueIdentifier)
        {
            var itemsAtCustomerResponse = await _orderRepository
                .GetItemsAtCustomer(orderUniqueIdentifier);
            if (itemsAtCustomerResponse.isSuccess == false)
            {
                return NotFound(itemsAtCustomerResponse);
            }

            return Ok(itemsAtCustomerResponse);
        }

        [HttpGet]
        [Route("GetPdf/{OrderUniqueIdentifier}")]
        public async Task<IActionResult> GetPdf([FromRoute] string OrderUniqueIdentifier)
        {
            var order = await _orderRepository.GetOrderForPdf(OrderUniqueIdentifier);
            if (order == null)
            {
                return NotFound("Id'yi kontrol edin");
            }

            var date = order.OrderTime.ToString();
            var dateNormalized = date.Replace("/", "_");
            string fileName = "PDF_" + dateNormalized + ".pdf";

            var provider = new FileExtensionContentTypeProvider();
            string filePath = order.OriginalOrderFromCustomer.pdfInfo.Path;
            string contentType = "application/octet-stream";
            byte[] fileBytes;

            fileBytes = System.IO.File.ReadAllBytes(filePath);

            return File(fileBytes, contentType, fileName);

        }

        [HttpPost]
        [Route("CreateOrder/{userId}/{userAddressId}/{shippingTypeId}")]
        public async Task<ActionResult<Response_Order>> CreateOrder(
            [FromRoute] string userId, [FromRoute] int userAddressId,
            [FromRoute] int shippingTypeId)
        {
            var orderResponse = await _orderRepository
                .GenerateOrder(userId, userAddressId, shippingTypeId);
            if (orderResponse.isSuccess == false)
            {
                return BadRequest(orderResponse);
            }

            return Ok(orderResponse);
        }

        //İade Yönetimi
        [HttpPost]
        [Route("AddItemToReturn/{itemAtCustomerId}/{quantity}")]
        public async Task<ActionResult<Response_IsSuccess>> AddItemToReturn(
            [FromRoute] int itemAtCustomerId, [FromRoute] int quantity)
        {

            var response = await _returnExchangeRequestRepository
                .AddItemToReturn(itemAtCustomerId, quantity);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);

        }
        [HttpPost]
        [Route("RemoveItemFromReturn/{returnedItemId}/{quantity}")]
        public async Task<ActionResult<Response_IsSuccess>> RemoveItemFromReturn(
            [FromRoute] int returnedItemId, [FromRoute] int quantity)
        {
            var response = await _returnExchangeRequestRepository
                .RemoveItemFromReturn(returnedItemId, quantity);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet]
        [Route("GetAllReturnedItems/{orderId}")]
        public async Task<ActionResult<Response_ReturnedItems>> GetAllReturnedItems
            ([FromRoute] int orderId)
        {
            var response = await _returnExchangeRequestRepository
                .GetAllReturnedItems(orderId);
            if (response.IsSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        //Değişim Endpointleri (envere verilecek url'ler)

        [HttpPost]
        [Route("CreateExchangeRequest")]
        public async Task<ActionResult<Response_Exchange>> CreateExchangeRequest
            ([FromBody] Request_Exchange exchangeRequest)
        {
            var response = await _returnExchangeRequestRepository
                .CreateExchangeRequest(exchangeRequest);

            return Ok(response);
        }
        [HttpGet]
        [Route("GetExchangeRequest/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_ExchangeFullInfo>> GetExchangeRequest
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .GetExchangeRequest(exchangeUniqueIdentifier);
            if (response.IsSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost]
        [Route("MarkExchangeOrderAsDone/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_IsSuccess>> MarkExchangeOrderAsDone
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .MarkExchangeOrderAsDone(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("MarkExchangeOrderAsNotDone/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_IsSuccess>> MarkExchangeOrderAsNotDone
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .MarkExchangeOrderAsNotDone(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("SendEmailWithPendingInfo/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_IsSuccess>> SendEmailWithPendingInfo
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .SendEmailWithPendingInfo(exchangeUniqueIdentifier);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("SendEmailWithCompletePdf/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_IsSuccess>> SendEmailWithCompletePdf
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .SendEmailWithCompletedPdf(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet]
        [Route("GetAllGoodExchangeItems/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_AllExchangedGoodItems>>
            GetAllGoodExchangeItems([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .GetAllExchangeGoodItems(exchangeUniqueIdentifier);

            if (response.IsSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("AddGoodExchangeItem")]
        public async Task<ActionResult<Response_IsSuccess>> AddGoodExchangeItem
            ([FromBody] Request_AddExchangeGoodItem addExchangeGoodItem)
        {
            var response = await _returnExchangeRequestRepository
                .AddExchangeGoodItem(addExchangeGoodItem);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("RemoveGoodExchangeItem")]
        public async Task<ActionResult<Response_IsSuccess>> RemoveGoodExchangeItem
            ([FromBody] Request_RemoveExchangeGoodItem removeExchangeGoodItem)
        {
            var response = await _returnExchangeRequestRepository
                .RemoveExchangeGoodItem(removeExchangeGoodItem);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpGet]
        [Route("GetAllPendingExchangeItems/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_AllExchangePendingItems>>
            GetAllPendingExcahngeItems(
            [FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .GetAllExchangePendingItems(exchangeUniqueIdentifier);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("AddPendingExchangeItem")]
        public async Task<ActionResult<Response_IsSuccess>> AddPendingExchangeItem
        ([FromBody] Request_AddExchangePendingItem addExchangePendingItem)
        {
            var response = await _returnExchangeRequestRepository
                .AddExchangePendingItem(addExchangePendingItem);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost]
        [Route("RemovePendingExchangeItem")]
        public async Task<ActionResult<Response_IsSuccess>> RemovePendingExchangeItem
            ([FromBody] Request_RemoveExchangePendingItem removeExchangePendingItem)
        {
            var response = await _returnExchangeRequestRepository
                .RemoveExchangePendingItem(removeExchangePendingItem);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("MovePendingToGood")]
        public async Task<ActionResult<Response_IsSuccess>> MovePendingToGood
            ([FromBody] Request_MovePendingToGood movePendingToGood)
        {
            var response = await _returnExchangeRequestRepository
                .MovePendingItemToGood(movePendingToGood);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("MovePendingToBad")]
        public async Task<ActionResult<Response_IsSuccess>> MovePendingToBad(
            [FromBody] Request_MovePendingToBad movePendingToBad)
        {
            var response = await _returnExchangeRequestRepository
                .MovePendingItemToBad(movePendingToBad);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }


        [HttpGet]
        [Route("GetAllBadExchangeItems/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_AllExchangeBadItems>> GetAllExchangeBadItems
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _returnExchangeRequestRepository
                .GetAllExchangeBadItems(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("AddBadExchangeItem")]
        public async Task<ActionResult<Response_IsSuccess>> AddBadExchangeItem
            ([FromBody] Request_AddExchangeBadItem addExchangeBadItem)
        {
            var response = await _returnExchangeRequestRepository
                .AddExchangeBadItem(addExchangeBadItem);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("RemoveBadExchangeItem")]
        public async Task<ActionResult<Response_IsSuccess>> RemoveBadExchangeItem
            ([FromBody] Request_RemoveExchangeBadItem removeExchangeBadItem)
        {
            var response = await _returnExchangeRequestRepository
                .RemoveExchangeBadItem(removeExchangeBadItem);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("CreateRefundOrder")]
        public async Task<ActionResult<Response_Refund>> CreateRefundOrder
          ([FromBody] Request_Refund requestRefund)
        {
            var refundResponse = await _refundRepository.CreateRefundOrder(requestRefund);
            if (refundResponse.isSuccess == false)
            {
                return BadRequest(refundResponse);
            }

            return Ok(refundResponse);
        }

        [HttpGet]
        [Route("GetRefundRequest/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_RefundFullInfo>> GetRefundRequest(
            [FromRoute] string exchangeUniqueIdentifier)
        {
            var refundRequest = await _refundRepository.GetRefundRequest(exchangeUniqueIdentifier);

            if (refundRequest.isSuccess == false)
            {
                return BadRequest(refundRequest);
            }

            return Ok(refundRequest);
        }

        [HttpPost]
        [Route("AddReturnedGoodItem")]
        public async Task<ActionResult<Response_RefundIsSuccess>> AddReturnedGoodItem
            ([FromBody] Request_AddGoodRefundItem addGoodRefundItem)
        {
            var returnedItemResponse = await _refundRepository
                .AddReturnedGoodItem(addGoodRefundItem);
            if (returnedItemResponse.isSuccess == false)
            {
                return BadRequest(returnedItemResponse);
            }

            return Ok(returnedItemResponse);
        }

        [HttpPost]
        [Route("CancelGoodReturnItem")]
        public async Task<ActionResult<Response_RefundIsSuccess>> CancelGoodReturnItem
            ([FromBody] Request_CancelRefundItem cancelGoodRefundItem)
        {
            var returnResponse = await _refundRepository
                .CancelGoodReturnedItem(cancelGoodRefundItem);
            if (returnResponse.isSuccess == false)
            {
                return BadRequest(returnResponse);
            }

            return Ok(returnResponse);
        }

        [HttpPost]
        [Route("AddReturnedBadItem")]
        public async Task<ActionResult<Response_RefundIsSuccess>> AddReturnedBadItem
            ([FromBody] Request_AddBadRefundItem addBadRefundItem)
        {
            var returnResponse = await _refundRepository
                .AddReturnedBadItem(addBadRefundItem);

            if (returnResponse.isSuccess == false)
            {
                return BadRequest(returnResponse);
            }

            return Ok(returnResponse);
        }

        [HttpPost]
        [Route("CancelBadReturnItem")]
        public async Task<ActionResult<Response_RefundIsSuccess>> CancelBadReturnItem
            ([FromBody] Request_CancelRefundItem cancelRefundItem)
        {
            var returnResponse = await _refundRepository.
                CancelBadReturnItem(cancelRefundItem);

            if (returnResponse.isSuccess == false)
            {
                return BadRequest(returnResponse);
            }
            return Ok(returnResponse);
        }

        [HttpPost]
        [Route("SetOrderAsRefunded/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_RefundIsSuccess>> SetOrderAsRefunded
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository
                .SetOrderAsRefunded(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost]
        [Route("CancelOrderAsRefunded/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_RefundIsSuccess>> CancelOrderAsRefunded
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository
                .CancelOrderAsRefunded(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost]
        [Route("SetOrderAsDone/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_RefundIsSuccess>> SetOrderAsDone
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository.SetOrderAsDone(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost]
        [Route("CancelOrderAsDone/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_RefundIsSuccess>> CancelOrderAsDone
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository.CancelOrderAsDone(exchangeUniqueIdentifier);

            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet]
        [Route("GetAllGoodRefundItems/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_GoodRefundItems>> GetAllGoodRefundItems
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository
                .GetAllGoodRefundItems(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet]
        [Route("GetAllBadRefundItems/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_BadRefundItems>> GetAllBadRefundItems
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository
                .GetAllBadRefundItems(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost]
        [Route("AllItemsCheckedSendEmail/{exchangeUniqueIdentifier}")]
        public async Task<ActionResult<Response_RefundIsSuccess>> AllItemsCheckedSendEmail
            ([FromRoute] string exchangeUniqueIdentifier)
        {
            var response = await _refundRepository
                .AllItemsCheckedSendEmail(exchangeUniqueIdentifier);
            if (response.isSuccess == false)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
    }
}