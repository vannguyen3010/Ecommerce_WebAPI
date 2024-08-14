using ECommerceNet8.Data;
using ECommerceNet8.DTOs.RefundRequestDtos.Request;
using ECommerceNet8.DTOs.RefundRequestDtos.Response;
using ECommerceNet8.Models.OrderModels;
using ECommerceNet8.Templates;
using Microsoft.EntityFrameworkCore;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ECommerceNet8.Repositories.RefundRepository
{
    public class RefundRepository : IRefundRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ISendGridClient _sendGridClient;

        public RefundRepository(ApplicationDbContext db,
            IConfiguration configuration,
            ISendGridClient sendGridClient)
        {
            _db = db;
            _configuration = configuration;
            _sendGridClient = sendGridClient;
        }


        public async Task<Response_Refund> CreateRefundOrder(Request_Refund refundRequest)
        {
            var existingRefundOrder = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == refundRequest.ExchangeUniqueIdentifier);

            if (existingRefundOrder != null)
            {
                return new Response_Refund
                {
                    isSuccess = false,
                    OrderUniqueIdentifier = refundRequest.OrderUniqueIdentifer,
                    ExchangeUniqueIdentifier = refundRequest.ExchangeUniqueIdentifier,
                    Message = "İade zaten var"
                };
            }

            var existingOrder = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderUniqueIdentifier
                == refundRequest.OrderUniqueIdentifer);

            if (existingOrder == null)
            {
                return new Response_Refund()
                {
                    isSuccess = false,
                    OrderUniqueIdentifier = refundRequest.OrderUniqueIdentifer,
                    ExchangeUniqueIdentifier = refundRequest.ExchangeUniqueIdentifier,
                    Message = "verilen id ile eşleşen iade bulunamadı"
                };
            }

            var itemReturnRequest = new ItemReturnRequest()
            {
                RequestClosed = false,
                RequestRefunded = false,
                ExchangeRequestTime = refundRequest.ExchangeRequestTime,
                OrderId = existingOrder.OrderId,
                OrderUniqueIdentifier = existingOrder.OrderUniqueIdentifier,
                ExchangeUniqueIdentifier = refundRequest.ExchangeUniqueIdentifier,
                AdminId = refundRequest.AdminId,
                AdminFullName = refundRequest.AdminName,
                UserEmail = refundRequest.Email,
                UserPhone = refundRequest.PhoneNumber,
                UserBankName = refundRequest.BankName,
                UserBankAccount = refundRequest.AccountNumber,
                totalAmountNotRefunded = 0,
                totalAmountRefunded = 0,
                totalRequestForRefund = 0,
            };

            await _db.ItemReturnRequests.AddAsync(itemReturnRequest);
            await _db.SaveChangesAsync();

            return new Response_Refund()
            {
                isSuccess = true,
                OrderUniqueIdentifier = itemReturnRequest.OrderUniqueIdentifier,
                ExchangeUniqueIdentifier = itemReturnRequest.ExchangeUniqueIdentifier,
                ReturnRequestId = itemReturnRequest.Id,
                Message = "İade talebi başarıyla oluşturuldu"
            };

        }

        public async Task<Response_RefundFullInfo> GetRefundRequest(string exchangeUniqueIdentifier)
        {
            var refundRequest = await _db.ItemReturnRequests
                 .Include(ir => ir.itemsGoodForRefund)
                 .Include(ir => ir.itemsBadForRefund)
                 .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                 == exchangeUniqueIdentifier);

            if (refundRequest == null)
            {
                return new Response_RefundFullInfo()
                {
                    isSuccess = false,
                    Message = "iade bulunamadı"
                };
            }


            return new Response_RefundFullInfo()
            {
                isSuccess = true,
                OrderUniqueIdentifier = refundRequest.OrderUniqueIdentifier,
                ExchangeUniqueIdentifier = refundRequest.ExchangeUniqueIdentifier,
                Id = refundRequest.Id,
                OrderId = refundRequest.OrderId,
                AdminId = refundRequest.AdminId,
                AdminFullName = refundRequest.AdminFullName,
                UserEmail = refundRequest.UserEmail,
                UserPhone = refundRequest.UserPhone,
                UserBankName = refundRequest.UserBankName,
                UserBankAccount = refundRequest.UserBankAccount,
                ExchangeRequestTime = refundRequest.ExchangeRequestTime,
                ItemsGoodForRefund = refundRequest.itemsGoodForRefund,
                ItemsBadForRefund = refundRequest.itemsBadForRefund,
                TotalRequestForRefund = refundRequest.totalRequestForRefund,
                TotalAmountRefunded = refundRequest.totalAmountRefunded,
                TotalAmountNotRefunded = refundRequest.totalAmountNotRefunded,

                RequestRefunded = refundRequest.RequestRefunded,
                RequestClosed = refundRequest.RequestClosed

            };

        }
        public async Task<Response_RefundIsSuccess> AddReturnedGoodItem(Request_AddGoodRefundItem addGoodRefundItem)
        {
            
            var existingReturnRequest = await _db.ItemReturnRequests
                .Include(ir => ir.itemsGoodForRefund)
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == addGoodRefundItem.ExchangeUniqueIdentifier);

            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }
            
            var existingOrder = await _db.Orders
                .Include(o => o.ItemsAtCustomer)
                .FirstOrDefaultAsync(o => o.OrderUniqueIdentifier
                == existingReturnRequest.OrderUniqueIdentifier);

            if (existingOrder == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebinde ürün yok"
                };
            }

            
            var existingProductVariant = await _db.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == addGoodRefundItem.ProductVariantId);

            if (existingProductVariant == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "ürün varyantı bulunamadı"
                };
            }
            
            var existingReturnItem = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == existingOrder.OrderId
                && ri.ProductVariantId == existingProductVariant.Id);

            if (existingReturnItem == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade içerisinde ürün yok"
                };
            }

            var itemAtCustomerPricePaid = existingReturnItem.PricePerItem;

            if (existingReturnItem.Quantity < addGoodRefundItem.Quantity)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = $"Müşteri {existingReturnItem.Quantity.ToString()}" +
                    $" adet ürün iade etti, ama {addGoodRefundItem.Quantity.ToString()} kadar ürünü değiştirmek istiyor"
                };
            }

           


            var itemExistInGoodItemsForRefund = false;
            int goodReturnItemId = 0;

            foreach (var item in existingReturnRequest.itemsGoodForRefund)
            {
                if (item.ProductVariantId == existingProductVariant.Id)
                {
                    itemExistInGoodItemsForRefund = true;
                    goodReturnItemId = item.Id;
                }
            }

            if (itemExistInGoodItemsForRefund == true)
            {
                var existingGoodReturnItem = await _db.ItemsGoodForRefund
                    .FirstOrDefaultAsync(ig => ig.Id == goodReturnItemId);

                existingGoodReturnItem.Quantity = existingGoodReturnItem.Quantity
                    + addGoodRefundItem.Quantity;

                await _db.SaveChangesAsync();
            }
            else
            {
                var itemGoodForRefund = new ItemGoodForRefund()
                {
                    ItemReturnRequestId = existingReturnRequest.Id,
                    BaseProductId = existingReturnItem.BaseProductId,
                    BaseProductName = existingReturnItem.BaseProductName,
                    ProductVariantId = existingReturnItem.ProductVariantId,
                    ProductColor = existingReturnItem.ProductVariantColor,
                    ProductSize = existingReturnItem.ProductVariantSize,
                    PricePaidPerItem = itemAtCustomerPricePaid,
                    Quantity = addGoodRefundItem.Quantity
                };

                await _db.ItemsGoodForRefund.AddAsync(itemGoodForRefund);
                await _db.SaveChangesAsync();
            }



            existingReturnItem.Quantity = existingReturnItem.Quantity
                - addGoodRefundItem.Quantity;
            if (existingReturnItem.Quantity == 0)
            {
                _db.returnedItemsFromCustomers.Remove(existingReturnItem);
            }

            await _db.SaveChangesAsync();



            existingReturnRequest.totalRequestForRefund =
                existingReturnRequest.totalRequestForRefund +
                (itemAtCustomerPricePaid * addGoodRefundItem.Quantity);

            existingReturnRequest.totalAmountRefunded +=
                (itemAtCustomerPricePaid * addGoodRefundItem.Quantity);

            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "öğe iadeye uygun listesine eklendi"
            };

        }


        public async Task<Response_RefundIsSuccess> CancelGoodReturnedItem(Request_CancelRefundItem cancelRefundItem)
        {

            var existingReturnedGoodItem = await _db.ItemsGoodForRefund
                .FirstOrDefaultAsync(ig => ig.Id == cancelRefundItem.ReturnItemId);

            if (existingReturnedGoodItem == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iadeye uygun ürün bulunamadı"
                };
            }

            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.Id
                == existingReturnedGoodItem.ItemReturnRequestId);

            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            var existingOrder = await _db.Orders
                .Include(o => o.ReturnedItemsFromCustomers)
                .FirstOrDefaultAsync(o => o.OrderId == existingReturnRequest.OrderId);

            if (existingOrder == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "talep bulunamadı"
                };
            }


            bool itemIsInReturnedItems = false;
            foreach (var item in existingOrder.ReturnedItemsFromCustomers)
            {
                if (item.ProductVariantId == existingReturnedGoodItem.ProductVariantId)
                {
                    itemIsInReturnedItems = true;
                }
            }

            if (itemIsInReturnedItems == true)
            {
                foreach (var item in existingOrder.ReturnedItemsFromCustomers)
                {
                    if (item.ProductVariantId == existingReturnedGoodItem.ProductVariantId)
                    {
                        var returnedItemAtCustomer = await _db.returnedItemsFromCustomers
                            .FirstOrDefaultAsync(ri => ri.Id == item.Id);

                        returnedItemAtCustomer.Quantity =
                            returnedItemAtCustomer.Quantity + cancelRefundItem.Quantity;

                        await _db.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var returnItemFromCustomer = new ReturnedItemsFromCustomer()
                {
                    OrderId = existingOrder.OrderId,
                    BaseProductId = existingReturnedGoodItem.BaseProductId,
                    BaseProductName = existingReturnedGoodItem.BaseProductName,
                    ProductVariantId = existingReturnedGoodItem.ProductVariantId,
                    ProductVariantColor = existingReturnedGoodItem.ProductColor,
                    ProductVariantSize = existingReturnedGoodItem.ProductSize,
                    PricePerItem = existingReturnedGoodItem.PricePaidPerItem,
                    Quantity = cancelRefundItem.Quantity
                };

                await _db.returnedItemsFromCustomers.AddAsync(returnItemFromCustomer);
                await _db.SaveChangesAsync();
            }

            existingReturnedGoodItem.Quantity = existingReturnedGoodItem.Quantity
                - cancelRefundItem.Quantity;
            if (existingReturnedGoodItem.Quantity == 0)
            {
                _db.ItemsGoodForRefund.Remove(existingReturnedGoodItem);
            }

            await _db.SaveChangesAsync();

            existingReturnRequest.totalRequestForRefund =
                existingReturnRequest.totalRequestForRefund -
                (existingReturnedGoodItem.PricePaidPerItem * cancelRefundItem.Quantity);

            existingReturnRequest.totalAmountRefunded -=
                (existingReturnedGoodItem.PricePaidPerItem * cancelRefundItem.Quantity);

            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = false,
                Message = "ürün iadeden başarıyla kaldırıldı"
            };
        }
        public async Task<Response_RefundIsSuccess> AddReturnedBadItem(Request_AddBadRefundItem addBadRefundItem)
        {

            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == addBadRefundItem.ExchangeUniqueIdentifier);
            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            var existingProductVariant = await _db.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == addBadRefundItem.ProductVariantId);
            if (existingProductVariant == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "ürün varyantı bulunamadı"
                };
            }

            var existingReturnItem = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.ProductVariantId
                == addBadRefundItem.ProductVariantId);
            if (existingReturnItem == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "ürürnün iadesi bulunamadı"
                };
            }
            if (existingReturnItem.Quantity < addBadRefundItem.Quantity)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "müşteride yeterli miktarda ürün yok"
                };
            }

            var existingRefundRequest = await _db.ItemReturnRequests
                .Include(ir => ir.itemsBadForRefund)
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == addBadRefundItem.ExchangeUniqueIdentifier);
            if (existingRefundRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            bool itemExsitInBadForRefund = false;
            int itemBadForRefundId = 0;

            foreach (var item in existingRefundRequest.itemsBadForRefund)
            {
                if (item.ProductVariantId == addBadRefundItem.ProductVariantId)
                {
                    itemExsitInBadForRefund = true;
                    itemBadForRefundId = item.Id;
                }
            }

            if (itemExsitInBadForRefund == true)
            {
                var badItem = await _db.ItemsBadForRefund
                    .FirstOrDefaultAsync(ib => ib.Id == itemBadForRefundId);
                badItem.Quantity = badItem.Quantity + addBadRefundItem.Quantity;
                await _db.SaveChangesAsync();
            }
            else
            {
                var itemBadForRefund = new ItemBadForRefund()
                {
                    ItemReturnRequestId = existingReturnRequest.Id,
                    BaseProductId = existingReturnItem.BaseProductId,
                    BaseProductName = existingReturnItem.BaseProductName,
                    ProductVariantId = existingReturnItem.ProductVariantId,
                    ProductColor = existingReturnItem.ProductVariantColor,
                    ProductSize = existingReturnItem.ProductVariantSize,
                    PricePaidPerItem = existingReturnItem.PricePerItem,
                    Quantity = addBadRefundItem.Quantity,
                    ReasonForNotRefunding = addBadRefundItem.ReasonMessage
                };

                await _db.ItemsBadForRefund.AddAsync(itemBadForRefund);
                await _db.SaveChangesAsync();
            }

            existingReturnItem.Quantity = existingReturnItem.Quantity
                - addBadRefundItem.Quantity;

            var pricePerItemPaid = existingReturnItem.PricePerItem;

            if (existingReturnItem.Quantity == 0)
            {
                _db.returnedItemsFromCustomers.Remove(existingReturnItem);
            }
            await _db.SaveChangesAsync();

            existingRefundRequest.totalRequestForRefund +=
                (pricePerItemPaid * addBadRefundItem.Quantity);

            existingRefundRequest.totalAmountNotRefunded +=
                (pricePerItemPaid * addBadRefundItem.Quantity);

            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "İade Edilen Ürün İade Edilmeyen Ürünlere Taşındı"
            };

        }

        public async Task<Response_RefundIsSuccess> CancelBadReturnItem(Request_CancelRefundItem cancelRefundItem)
        {

            var existingBadItem = await _db.ItemsBadForRefund
                .FirstOrDefaultAsync(ib => ib.Id == cancelRefundItem.ReturnItemId);

            if (existingBadItem == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iadeye uygun ürün bulunamadı"
                };
            }

            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.Id == existingBadItem.ItemReturnRequestId);
            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            var returnedItemFromCustomer = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == existingReturnRequest.OrderId
                && ri.ProductVariantId == existingBadItem.ProductVariantId);
            if (returnedItemFromCustomer != null)
            {
                returnedItemFromCustomer.Quantity += cancelRefundItem.Quantity;
                await _db.SaveChangesAsync();
            }
            else
            {
                var newReturnedItemFromCustomer = new ReturnedItemsFromCustomer()
                {
                    OrderId = existingReturnRequest.OrderId,
                    BaseProductId = existingBadItem.BaseProductId,
                    BaseProductName = existingBadItem.BaseProductName,
                    ProductVariantId = existingBadItem.ProductVariantId,
                    ProductVariantColor = existingBadItem.ProductColor,
                    ProductVariantSize = existingBadItem.ProductSize,
                    PricePerItem = existingBadItem.PricePaidPerItem,
                    Quantity = cancelRefundItem.Quantity
                };
                await _db.returnedItemsFromCustomers.AddAsync(newReturnedItemFromCustomer);
                await _db.SaveChangesAsync();
            }

            var pricePaidPerItem = existingBadItem.PricePaidPerItem;


            existingBadItem.Quantity -= cancelRefundItem.Quantity;
            if (existingBadItem.Quantity == 0)
            {
                _db.ItemsBadForRefund.Remove(existingBadItem);
            }

            await _db.SaveChangesAsync();

            existingReturnRequest.totalRequestForRefund =
                existingReturnRequest.totalRequestForRefund -
                (pricePaidPerItem * cancelRefundItem.Quantity);

            existingReturnRequest.totalAmountNotRefunded =
                existingReturnRequest.totalAmountNotRefunded
                - (pricePaidPerItem * cancelRefundItem.Quantity);

            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "iadeye uygun olmayan ürün iadeye taşındı"
            };


        }

        public async Task<Response_RefundIsSuccess> SetOrderAsRefunded(string exchangeUniqueIdentifier)
        {
            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);
            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            existingReturnRequest.RequestRefunded = true;
            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "iade talebi iade edildi olarak işaretlendi"
            };
        }

        public async Task<Response_RefundIsSuccess> CancelOrderAsRefunded(string exchangeUniqueIdentifier)
        {
            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);

            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            existingReturnRequest.RequestRefunded = false;
            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "iade talebi iade edildi olarak işaretlendi"
            };
        }

        public async Task<Response_RefundIsSuccess> SetOrderAsDone(string exchangeUniqueIdentifier)
        {
            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);
            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }
            if (existingReturnRequest.RequestRefunded == false)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "Talep iade edilmedi, önce iade talebi yapınız"
                };
            }
            existingReturnRequest.RequestClosed = true;
            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "iade talebi kapalı"
            };

        }

        public async Task<Response_RefundIsSuccess> CancelOrderAsDone(string exchangeUniqueIdentifier)
        {
            var existingReturnRequest = await _db.ItemReturnRequests
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);
            if (existingReturnRequest == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }
            existingReturnRequest.RequestClosed = false;
            await _db.SaveChangesAsync();

            return new Response_RefundIsSuccess()
            {
                isSuccess = true,
                Message = "iade talebi kapatıldı"
            };
        }

        public async Task<Response_GoodRefundItems> GetAllGoodRefundItems(string exchangeUniqueIdentifier)
        {
            var existingReturnRequest = await _db.ItemReturnRequests
                .Include(ir => ir.itemsGoodForRefund)
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);

            if (existingReturnRequest == null)
            {
                return new Response_GoodRefundItems()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            return new Response_GoodRefundItems()
            {
                isSuccess = true,
                Message = "iyi durumdaki iadeler",
                OrderUniqueIdentifier = existingReturnRequest.OrderUniqueIdentifier,
                ExchangeUniqueIdentifier = existingReturnRequest.ExchangeUniqueIdentifier,
                itemsGoodForRefund = existingReturnRequest.itemsGoodForRefund.ToList()
            };
        }

        public async Task<Response_BadRefundItems> GetAllBadRefundItems(string exchangeUniqueIdentifier)
        {
            var existingReturnRequest = await _db.ItemReturnRequests
                .Include(ir => ir.itemsBadForRefund)
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);

            if (existingReturnRequest == null)
            {
                return new Response_BadRefundItems()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            return new Response_BadRefundItems()
            {
                isSuccess = true,
                Message = "kötü durumdaki iadeler",
                OrderUniqueIdentifier = existingReturnRequest.OrderUniqueIdentifier,
                ExchangeUniqueIdentifier = existingReturnRequest.ExchangeUniqueIdentifier,
                ItemsBadForRefund = existingReturnRequest.itemsBadForRefund.ToList()
            };
        }


        public async Task<Response_RefundIsSuccess> AllItemsCheckedSendEmail(string exchangeUniqueIdentifier)
        {
            var existingReturnOrder = await _db.ItemReturnRequests
                .Include(ir => ir.itemsGoodForRefund)
                .Include(ir => ir.itemsBadForRefund)
                .FirstOrDefaultAsync(ir => ir.ExchangeUniqueIdentifier
                == exchangeUniqueIdentifier);

            if (existingReturnOrder == null)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "iade talebi bulunamadı"
                };
            }

            if (existingReturnOrder.RequestClosed == false)
            {
                return new Response_RefundIsSuccess()
                {
                    isSuccess = false,
                    Message = "bilgi epostası göndermeden önce talebi kapatınız"
                };
            }

            string fromEmail = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromEmail");
            string fromName = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromName");

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(fromEmail, fromName),
                Subject = "Ürün İade Bilgilendirmesi",
                HtmlContent = EmailTemplates.RefundTemplate(existingReturnOrder)
            };

            var email = existingReturnOrder.UserEmail; //test aşamasında sıkıntılar yaşanabilir. bunlar hepimizin başına gelebilir.

            msg.AddTo("irfnms@gmail.com");
            var response = await _sendGridClient.SendEmailAsync(msg);

            string message = response.IsSuccessStatusCode ? "Email başarıyla gönderildi"
                : "Email gönderilemedi";
            bool messageSuccess = response.IsSuccessStatusCode;

            return new Response_RefundIsSuccess()
            {
                isSuccess = messageSuccess,
                Message = message
            };

        }
    }
}