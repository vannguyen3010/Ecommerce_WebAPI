using ECommerceNet8.Data;
using ECommerceNet8.DTOConvertions;
using ECommerceNet8.DTOs.RequestExchangeDtos.Request;
using ECommerceNet8.DTOs.RequestExchangeDtos.Response;
using ECommerceNet8.Models.OrderModels;
using ECommerceNet8.Models.ReturnExchangeModels;
using ECommerceNet8.Repositories.ReturnExchangeRequestRepository;
using ECommerceNet8.Templates;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Globalization;

namespace ECommerceNet8.Repositories.ReturnExchangeRequestRepository
{
    public class ReturnExchangeRequestRepository : IReturnExchangeRequestRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly ISendGridClient _sendGridClient;

        public ReturnExchangeRequestRepository(ApplicationDbContext db,
             IWebHostEnvironment webHostEnvironment,
             IConfiguration configuration,
             ISendGridClient sendGridClient)
        {
            _db = db;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            _sendGridClient = sendGridClient;
        }

        //kullanıcılar için olan kısım
        public async Task<ICollection<ExchangeRequestFromUser>> GetExchangeRequestFromUsers()
        {
            var existingExchangeRequest = await _db.exchangeRequestsFromUsers
                .ToListAsync();

            return existingExchangeRequest;
        }
        public async Task<ExchangeRequestFromUser> GetExchangeRequestByExchangeUniqueId(string exchangeUniqueIdentifier)
        {
            var existingExchangeRequest = await _db.exchangeRequestsFromUsers
                .FirstOrDefaultAsync(er => er.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            return existingExchangeRequest;
        }
        public async Task<ICollection<ExchangeRequestFromUser>> GetExchangeRequestByOrderUniqueIdentifier(string orderUniqueIdentifier)
        {
            var existingExchangeRequest = await _db.exchangeRequestsFromUsers
                .Where(er => er.OrderUniqueIdentifier == orderUniqueIdentifier).ToListAsync();

            return existingExchangeRequest;
        }
        public async Task<Response_ExchangeRequest> AddExchangeRequest(Request_ExchangeRequest exchangeRequest, string UserId)
        {
            var existingOrder = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderUniqueIdentifier == exchangeRequest.OrderUniqueIdentifier);

            if (existingOrder == null)
            {
                return new Response_ExchangeRequest()
                {
                    isSuccess = false,
                    Message = "Sipariş bulunamadı"
                };
            }

            var exchangeRequestFromUser = new ExchangeRequestFromUser()
            {
                OrderUniqueIdentifier = exchangeRequest.OrderUniqueIdentifier,
                ExchangeUniqueIdentifier = await GenerateUniqueExchangeIdentifierForExchange(),
                ExchangeRequestTime = DateTime.UtcNow,
                UserId = UserId,
                Email = exchangeRequest.Email,
                PhoneNumber = exchangeRequest.PhoneNumber,
                ApartmentNumber = exchangeRequest.ApartmentNumber,
                HouseNumber = exchangeRequest.HouseNumber,
                Street = exchangeRequest.Street,
                City = exchangeRequest.City,
                Region = exchangeRequest.Region,
                Country = exchangeRequest.Country,
                PostalCode = exchangeRequest.PostalCode,
                Message = exchangeRequest.Message
            };

            await _db.exchangeRequestsFromUsers.AddAsync(exchangeRequestFromUser);
            await _db.SaveChangesAsync();

            return new Response_ExchangeRequest()
            {
                isSuccess = true,
                Message = "Değişim talebi başarıyla eklendi"
            };
        }


        public async Task<Response_IsSuccess> AddItemToReturn(int itemAtCustomerId, int quantity)
        {
            //kullanıcıda o öğe var mı diye kontrol edilecek
            var itemAtCustomer = await _db.ItemAtCustomers
                .FirstOrDefaultAsync(ic => ic.Id == itemAtCustomerId);
            if (itemAtCustomer == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Kullanıcıda verilen id ile eşleşen ürün bulunamadı"
                };
            }
            //iade edilmek istenen ürün miktarı kullanıcıda var mı kontrolü 
            if (itemAtCustomer.Quantity < quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = $"Kullanıcıda iade etmek istediği miktarda öğe yok. kullanıcıdaki öğe sayısı: {itemAtCustomer.Quantity}" +
                    $"istenen miktar:{quantity}"
                };
            }
            

            var existingReturnItem = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == itemAtCustomer.OrderId
                && ri.ProductVariantId == itemAtCustomer.ProductVariantId);

            if (existingReturnItem == null)
            {
                var returnedItemFromCustomer = new ReturnedItemsFromCustomer()
                {
                    OrderId = itemAtCustomer.OrderId,
                    BaseProductId = itemAtCustomer.BaseProductId,
                    BaseProductName = itemAtCustomer.BaseProductName,
                    ProductVariantId = itemAtCustomer.ProductVariantId,
                    ProductVariantColor = itemAtCustomer.ProductVariantColor,
                    ProductVariantSize = itemAtCustomer.ProductVariantSize,
                    PricePerItem = itemAtCustomer.PricePaidPerItem,
                    Quantity = quantity
                };

                await _db.returnedItemsFromCustomers.AddAsync(returnedItemFromCustomer);
                await _db.SaveChangesAsync();
            }
            else
            {
                existingReturnItem.Quantity = existingReturnItem.Quantity + quantity;
                await _db.SaveChangesAsync();
            }
            
            itemAtCustomer.Quantity = itemAtCustomer.Quantity - quantity;
            if (itemAtCustomer.Quantity == 0)
            {
                _db.ItemAtCustomers.Remove(itemAtCustomer);
            }
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "Öğe Başarıyla İade Edilen Öğelere Taşındı"
            };
        }

        public async Task<Response_IsSuccess> RemoveItemFromReturn(int returnedItemId, int quantity)
        {
            //ürün var mı kontrolü yapılacak
            var existingReturnedItem = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.Id == returnedItemId);
            if (existingReturnedItem == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "İade edilen ürünlerde belirtilen id ile eşleşen ürün bulunamadı"
                };
            }
            //İade edilen ürünlerden kaldırılacak yeterli öğenin olup olmadığını kontrol edilecek
            if (existingReturnedItem.Quantity < quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Yeterli öğe yok"
                };
            }
            //tam emin olmamakla birlikte sanırım ItemsAtCustomer'a öğeleri ekleyeceğiz sonrasında da zaten varsa miktarı-quantitiy'i arttıracağız
            var existingItemAtCustomer = await _db.ItemAtCustomers
                .FirstOrDefaultAsync(ic => ic.OrderId == existingReturnedItem.OrderId
                && ic.ProductVariantId == existingReturnedItem.ProductVariantId);

            if (existingItemAtCustomer == null)
            {
                var itemAtCustomer = new ItemAtCustomer()
                {
                    OrderId = existingReturnedItem.OrderId,
                    BaseProductId = existingReturnedItem.BaseProductId,
                    BaseProductName = existingReturnedItem.BaseProductName,
                    ProductVariantId = existingReturnedItem.ProductVariantId,
                    ProductVariantColor = existingReturnedItem.ProductVariantColor,
                    ProductVariantSize = existingReturnedItem.ProductVariantSize,
                    PricePaidPerItem = existingReturnedItem.PricePerItem,
                    Quantity = quantity
                };

                await _db.ItemAtCustomers.AddAsync(itemAtCustomer);
                await _db.SaveChangesAsync();
            }
            else
            {
                existingItemAtCustomer.Quantity = existingItemAtCustomer.Quantity + quantity;
                await _db.SaveChangesAsync();
            }
            //Miktar-Quantity <= 0 ise, iade edilen Öğeden(returnedItem) Miktarı kaldırıyoruz, sonra öğeyi siliyoruz
            //TODO Remove Quantity from returnedItem if qty <= 0, remove item
            existingReturnedItem.Quantity = existingReturnedItem.Quantity - quantity;
            if (existingReturnedItem.Quantity == 0)
            {
                _db.returnedItemsFromCustomers.Remove(existingReturnedItem);
            }
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "öğe müşteri öğelerine gönderildi"
            };


        }

        public async Task<Response_ReturnedItems> GetAllReturnedItems(int orderId)
        {
            var existingOrder = await _db.Orders
                .Include(o => o.ReturnedItemsFromCustomers)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (existingOrder == null)
            {
                return new Response_ReturnedItems()
                {
                    IsSuccess = false,
                    Message = "verilen orderid ile eşleşen order bulunamadı"
                };
            }

            return new Response_ReturnedItems()
            {
                IsSuccess = true,
                Message = "Müşterinin tüm iade edilen ürünleri",
                returnedItemsFromCustomers = existingOrder.ReturnedItemsFromCustomers.ToList()

            };
        }

        public async Task<Response_Exchange> CreateExchangeRequest(Request_Exchange exchangeRequest)
        {
            int apartmentNumber;
            if (exchangeRequest.ApartmentNumber == null
                || exchangeRequest.ApartmentNumber == 0)
            {
                apartmentNumber = 0;
            }
            else
            {
                apartmentNumber = (int)exchangeRequest.ApartmentNumber;
            }

            var orderInDb = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderUniqueIdentifier == exchangeRequest.OrderUniqueIdentfier);

            if (orderInDb == null)
            {
                return new Response_Exchange()
                {
                    isSuccess = false,
                    Message = "Verilen kimliğe uygun sipariş bulunamadı"
                };
            }
            var itemExchangeRequest = new ItemExchangeRequest()
            {
                OrderId = orderInDb.OrderId,
                OrderUniqueIdentifier = exchangeRequest.OrderUniqueIdentfier,
                ExchangeUniqueIdentifier = exchangeRequest.ExchangeUniqueIdentfier,
                AdminId = exchangeRequest.AdminId,
                AdminName = exchangeRequest.AdminFullName,
                UserEmail = exchangeRequest.UserEmail,
                UserPhone = exchangeRequest.UserPhone,
                UserFirstName = exchangeRequest.UserFirstName,
                UserLastName = exchangeRequest.UserLastName,
                ApartmentNumber = apartmentNumber,
                HouseNumber = exchangeRequest.HouseNumber,
                Street = exchangeRequest.Street,
                City = exchangeRequest.City,
                Region = exchangeRequest.Region,
                Country = exchangeRequest.Country,
                PostalCode = exchangeRequest.PostalCode,
                RequestClosed = false
            };

            await _db.ItemExchangeRequests.AddAsync(itemExchangeRequest);
            await _db.SaveChangesAsync();

            return new Response_Exchange()
            {
                isSuccess = true,
                ExchangeRequestId = itemExchangeRequest.Id,
                OrderUniqueIdentifier = exchangeRequest.OrderUniqueIdentfier,
                ExchangeUniqueIdentfier = exchangeRequest.ExchangeUniqueIdentfier,
                Message = "Değişim talebi oluşturuldu"
            };

        }

        public async Task<Response_ExchangeFullInfo> GetExchangeRequest(string exchangeUniqueIdentifier)
        {
            var exchangeRequest = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeOrderItems)
                .Include(ie => ie.exchangeItemsCanceled)
                .Include(ie => ie.exchangeItemsPending)
                .Include(ie => ie.exchangeConfirmedPdfInfo)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (exchangeRequest == null)
            {
                return new Response_ExchangeFullInfo()
                {
                    IsSuccess = false,
                    Message = "Verilen id ile eşleşen değişim talebi bulunamadı"
                };
            }

            return new Response_ExchangeFullInfo()
            {
                IsSuccess = true,
                Message = "Değişim alındı",
                OrderId = exchangeRequest.OrderId,
                OrderUniqueIdentifier = exchangeRequest.OrderUniqueIdentifier,
                ExchangeUniqueIdentfier = exchangeRequest.ExchangeUniqueIdentifier,
                AdminId = exchangeRequest.AdminId,
                AdminFullName = exchangeRequest.AdminName,
                UserFirstName = exchangeRequest.UserFirstName,
                UserLastName = exchangeRequest.UserLastName,
                UserEmail = exchangeRequest.UserEmail,
                UserPhone = exchangeRequest.UserPhone,
                ApartmentNumber = exchangeRequest.ApartmentNumber,
                HouseNumber = exchangeRequest.HouseNumber,
                Street = exchangeRequest.Street,
                City = exchangeRequest.City,
                Region = exchangeRequest.Region,
                Country = exchangeRequest.Country,
                PostalCode = exchangeRequest.PostalCode,
                ExchangeOrderItems = exchangeRequest.exchangeOrderItems.ToList(),
                ExchangeItemsCanceled = exchangeRequest.exchangeItemsCanceled.ToList(),
                exchangeItemsPending = exchangeRequest.exchangeItemsPending.ToList(),
                RequestClosed = exchangeRequest.RequestClosed,
                exchangeConfirmedPdfInfoId = exchangeRequest.ExchangeConfirmedPdfInfoId,
                ExchangeConfirmedPdfInfo = exchangeRequest.exchangeConfirmedPdfInfo
            };
        }

        public async Task<Response_IsSuccess> MarkExchangeOrderAsDone(string exchangeUniqueIdentifier)
        {
            //Değişim var mı kontrolü
            var existingExchangeOrder = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeOrderItems)
                .Include(ie => ie.exchangeItemsPending)
                .Include(ie => ie.exchangeItemsCanceled)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (existingExchangeOrder == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "verilen id ile eşleşen değişim bulunamadı"
                };
            }
            
            if (existingExchangeOrder.exchangeItemsPending.Count > 0)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Sipariş içerisinde bekleyen (henüz onaylanmamış) öğeler var"
                };
            }
            //Değişim talebi hala var mı diye kontrol 
            if (existingExchangeOrder.RequestClosed == true)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Değişim talebi kapatılmış"
                };
            }
            //pdf var mı kontrolü
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderId == existingExchangeOrder.OrderId);
            var pdfExist = await _db.ExchangeConfirmedPdfInfos
                .FirstOrDefaultAsync(pdf => pdf.ItemExchangeRequestId == existingExchangeOrder.Id);

            if (pdfExist != null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "PDF zaten var. id'yi değiştirmek için var olan pdf'i siliniz"
                };
            }
            //sanırım ürün müşteride var mı diye bakıp duruma göre ürünü ekliyor yahut adetini arttırıyoruz
            // yine de kodları kendiniz kontrol ediniz
            foreach (var exchangeItem in existingExchangeOrder.exchangeOrderItems)
            {
                var itemAtCustomer = await _db.ItemAtCustomers
                    .FirstOrDefaultAsync(ic => ic.OrderId == existingExchangeOrder.OrderId
                    && ic.ProductVariantId == exchangeItem.ExchangedProductVariantId);
                if (itemAtCustomer == null)
                {
                    ItemAtCustomer newItemAtCustomer = new ItemAtCustomer()
                    {
                        OrderId = existingExchangeOrder.OrderId,
                        BaseProductId = exchangeItem.BaseProductId,
                        BaseProductName = exchangeItem.BaseProductName,
                        ProductVariantId = exchangeItem.ExchangedProductVariantId,
                        ProductVariantColor = exchangeItem.ExchangedProductVariantColor,
                        ProductVariantSize = exchangeItem.ExchangedProductVariantSize,
                        PricePaidPerItem = exchangeItem.PricePerItemPaid,
                        Quantity = exchangeItem.Quantity,
                    };

                    await _db.ItemAtCustomers.AddAsync(newItemAtCustomer);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    itemAtCustomer.Quantity = itemAtCustomer.Quantity + exchangeItem.Quantity;
                    await _db.SaveChangesAsync();
                }
            }
            //PDF oluşturmaca
            ExchangeConfirmedPdfInfo pdfInfo = new ExchangeConfirmedPdfInfo();
            pdfInfo.ItemExchangeRequestId = existingExchangeOrder.Id;
            pdfInfo.Name = existingExchangeOrder.ExchangeUniqueIdentifier + "için PDF bilgilendirme dosyası ";
            pdfInfo.Added = DateTime.UtcNow;
            pdfInfo.Path = await CreateExchangePdf(existingExchangeOrder, order.UserId);

            await _db.ExchangeConfirmedPdfInfos.AddAsync(pdfInfo);
            await _db.SaveChangesAsync();

            existingExchangeOrder.ExchangeConfirmedPdfInfoId = pdfInfo.Id;
            existingExchangeOrder.RequestClosed = true;
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "Değişim siparişi onaylandı, pdf oluşturuldu, müşterideki ürünler eklendi"
            };


        }
        public async Task<Response_IsSuccess> MarkExchangeOrderAsNotDone(string exchangeUniqueIdentifier)
        {
            //DEğişim var mı diye kontrol ediyoruz
            var existingExchangeOrder = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeOrderItems)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (existingExchangeOrder == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Değişim buluanamdı verilen id ile eşleşen" // usta yoda gibi oldu biraz
                };
            }
            //değişim talebi hala var mı?
            if (existingExchangeOrder.RequestClosed == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Değişim talebi henüz kapatılmamış"
                };
            }

            //sanırım burada müşterideki ürün miktarı ile değişimi istenen ürün miktarı kıyaslanıyor.
            // sonrasında ise duruma göre bir şeyler yapılıyor
            bool allItemsExistInCustomerItems = true;
            bool allHaveEnoughQty = true;

            foreach (var exchangeItem in existingExchangeOrder.exchangeOrderItems)
            {
                var itemAtCustomer = await _db.ItemAtCustomers
                    .FirstOrDefaultAsync(ic => ic.OrderId == existingExchangeOrder.OrderId
                    && ic.ProductVariantId == exchangeItem.ExchangedProductVariantId);
                if (itemAtCustomer == null)
                {
                    allItemsExistInCustomerItems = false;
                }
                else
                {
                    if (itemAtCustomer.Quantity < exchangeItem.Quantity)
                    {
                        allHaveEnoughQty = false;
                    }
                }
            }

            if (allItemsExistInCustomerItems == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Bazı ürün veya ürünler müşteride yok"
                };
            }
            if (allHaveEnoughQty == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "müşteride Bazı ürünlerden iade etmeye yetecek adette yok"
                };
            }

            //ürünleri müşteriden siliyoruz.eğer adet-quantity == 0 olursa tabii
            foreach (var exchangeItem in existingExchangeOrder.exchangeOrderItems)
            {
                var itemAtCustomer = await _db.ItemAtCustomers
                    .FirstOrDefaultAsync(ic => ic.OrderId == existingExchangeOrder.OrderId
                    && ic.ProductVariantId == exchangeItem.ExchangedProductVariantId);

                itemAtCustomer.Quantity = itemAtCustomer.Quantity - exchangeItem.Quantity;
                if (itemAtCustomer.Quantity == 0)
                {
                    _db.ItemAtCustomers.Remove(itemAtCustomer);
                }
                await _db.SaveChangesAsync();
            }

            //PDF'i uçuruyoruz yer kaplamasın diye.
            //bence ne olur ne olmaz bir yerde tutmakta fayda var
            var existingPdfInfo = await _db.ExchangeConfirmedPdfInfos
            .FirstOrDefaultAsync(pdf => pdf.ItemExchangeRequestId == existingExchangeOrder.Id);

            if (existingPdfInfo != null)
            {
                if (File.Exists(existingPdfInfo.Path))
                {
                    File.Delete(existingPdfInfo.Path);
                }

                _db.ExchangeConfirmedPdfInfos.Remove(existingPdfInfo);
                await _db.SaveChangesAsync();
            }

            existingExchangeOrder.RequestClosed = false;
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = false,
                Message = "Değişim talebi betal edildi"
            };
        }

        public async Task<Response_Exchange> SendEmailWithPendingInfo(string exchangeUniqueIdentifier)
        {
            var existingExchangeOrder = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeOrderItems)
                .Include(ie => ie.exchangeItemsPending)
                .Include(ie => ie.exchangeItemsCanceled)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (existingExchangeOrder == null)
            {
                return new Response_Exchange()
                {
                    isSuccess = false,
                    Message = "verilen id ile eşleşen değişim talebi bulunamadı"
                };
            }

            string fromEmail = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromEmail");
            string fromName = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromName");

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(fromEmail, fromName),
                Subject = "Ürün İade Ayrıntıları",
                HtmlContent = EmailTemplates.ExchangePendingTemplate(existingExchangeOrder)
            };

            var email = existingExchangeOrder.UserEmail; //Test için kullanırken sıkıntı çıkabilir. temkinli davranınız
            msg.AddTo("irfnms@gmail.com");

            var response = await _sendGridClient.SendEmailAsync(msg);

            string message = response.IsSuccessStatusCode ? "Email başarıyla gönderildi" :
                "Email Gönderilirken hata oluştu";

            bool messageSuccess = response.IsSuccessStatusCode;

            return new Response_Exchange()
            {
                isSuccess = messageSuccess,
                Message = message
            };

        }
        public async Task<Response_Exchange> SendEmailWithCompletedPdf(string exchangeUniqueIdentifier)
        {
            //Değişim var mı kontrolü 
            var existingExchangeOrder = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeConfirmedPdfInfo)
                .Include(ie => ie.exchangeOrderItems)
                .Include(ie => ie.exchangeItemsCanceled)
                .Include(ie => ie.exchangeItemsPending)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (existingExchangeOrder == null)
            {
                return new Response_Exchange()
                {
                    isSuccess = false,
                    Message = "No existing exchange order found with given Unique Id"
                };
            }
            //talep kapalı mı diye bak
            if (existingExchangeOrder.RequestClosed == false)
            {
                return new Response_Exchange()
                {
                    isSuccess = false,
                    Message = "Değişim talebi hala açık, önce talebi kapatınız."
                };
            }
            //PDF eklentili email göndermece
            string fromEmail = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromEmail");
            string fromName = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromName");

            var msg = new SendGridMessage()
            {
                From = new EmailAddress(fromEmail, fromName),
                Subject = "Ürün değişim detayları",
                HtmlContent = EmailTemplates.ExchangePendingTemplate(existingExchangeOrder)
            };

            var email = existingExchangeOrder.UserEmail;//diğer email işlemleri gibi bunda da test aşamasında sorun sıkıntı yaşanabilir. metanetinizi koruyunuz

            msg.AddTo("irfnms@gmail.com");
            var bytes = File.ReadAllBytes(existingExchangeOrder.exchangeConfirmedPdfInfo.Path);
            var file = Convert.ToBase64String(bytes);
            msg.AddAttachment("Invoice.pdf", file);

            var response = await _sendGridClient.SendEmailAsync(msg);
            string message = response.IsSuccessStatusCode ? "Email başarıyla gönderildi" :
                "Email gönderilirken bir hata oluştu";

            bool messageSuccess = response.IsSuccessStatusCode;

            return new Response_Exchange()
            {
                isSuccess = messageSuccess,
                Message = message
            };
        }

        public Task<Response_Exchange> CreateExchangeRequestByAdmin(Request_ExchangeByAdmin exchangeRequest)
        {
            throw new NotImplementedException();
        }



        public async Task<Response_AllExchangedGoodItems> GetAllExchangeGoodItems(string exchangeUniqueIdentifier)
        {
            var existingExchangeOrder = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeOrderItems)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (existingExchangeOrder == null)
            {
                return new Response_AllExchangedGoodItems()
                {
                    IsSuccess = false,
                    Message = "verilen id ile eşleşen değişim talebi bulunamadı"
                };
            }

            var allExchangeGoodItemList = existingExchangeOrder.ConvertToDtoGoodItems();

            allExchangeGoodItemList.IsSuccess = true;
            allExchangeGoodItemList.Message = "ürünler değişime uygundur";

            return allExchangeGoodItemList;

        }

        public async Task<Response_IsSuccess> AddExchangeGoodItem(Request_AddExchangeGoodItem exchangeGoodItem)
        {
            //değişim talebi mevcudiyeti kontrolü sağlanmalıdır
            var existingExchangeOrder = await _db.ItemExchangeRequests
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeGoodItem.ExchangeUniqueIdentifier);
            if (existingExchangeOrder == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "verilen id ile eşleşen değişim talebi bulunmamaktadır"
                };
            }
            //değişim talebi kapalı mı kontrolü
            if (existingExchangeOrder.RequestClosed == true)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "talep kapalıdır. üzerinde değişiklik yapmaya çalışmak sql'e hakarettir. bazı şeyler zorlanmamalıdır."
                };
            }
            //talep var mı kontrolü
            var existingOrder = await _db.Orders
                .Include(o => o.ReturnedItemsFromCustomers)
                .FirstOrDefaultAsync(o => o.OrderUniqueIdentifier
                == exchangeGoodItem.OrderUniqueIdentifier);
            if (existingOrder == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "talep bulunamadı verilen id ile eşleşen" //devrik cümle kurunca yılmaz erdoğan yazmış gibi oluyor LoL
                };
            }
            //iade edilmek istenen ürün var mı ve miktarı yeterli mi diye kontrol sağlanmalıdır
            bool itemAtCustomerExist = false;
            bool itemAtCustomerHasEnoughQty = false;
            foreach (var item in existingOrder.ReturnedItemsFromCustomers)
            {
                if (item.ProductVariantId == exchangeGoodItem.ReturnedProductVariantId)
                {
                    itemAtCustomerExist = true;
                    if (item.Quantity >= exchangeGoodItem.Quantity)
                    {
                        itemAtCustomerHasEnoughQty = true;
                    }
                }
            }

            if (itemAtCustomerExist == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "iade edilmek istenen ürün müşteride yok gibi görünüyor" //bu hatayı kolay kolay kimse almaz diye düşünüyorum
                };
            }
            if (itemAtCustomerHasEnoughQty == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Müşteride yeterli ürün bulunmamakta"
                };
            }
            //ürün varyantı(spesifik olarak bir renk ve beden) yeterli miktarda var mı kontrolü
            var exchangeProductVariant = await _db.ProductVariants
                .Include(pv => pv.baseProduct)
                .Include(pv => pv.productColor)
                .Include(pv => pv.productSize)
                .FirstOrDefaultAsync(pv => pv.Id == exchangeGoodItem.ExchangeProductVariantId);
            if (exchangeProductVariant.Quantity < exchangeGoodItem.Quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "stokta değişim yapılacak miktarda ürün yok"
                };
            }
            // müşterinin 'iade edilen ürünler' kısmından ürün adetini yahut bizzat ürünü silmemiz gerekiyor.
            var itemReturned = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(r => r.OrderId == existingExchangeOrder.OrderId
                && r.ProductVariantId == exchangeGoodItem.ReturnedProductVariantId);


            //öğeler var mı
            if (itemReturned == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "No item at returned items with given product variant id"
                };
            }
            var pricePerItem = itemReturned.PricePerItem;
            //adeti azalt eğer 0 olursa sil

            itemReturned.Quantity = itemReturned.Quantity - exchangeGoodItem.Quantity;
            if (itemReturned.Quantity < 0)
            {
                itemReturned.Quantity = itemReturned.Quantity + exchangeGoodItem.Quantity;

                await _db.SaveChangesAsync();

                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "iade edilen ürünlerde yeterli öğe yok. adet bilgisini kontrol ediniz"
                };
            }
            if (itemReturned.Quantity == 0)
            {
                _db.returnedItemsFromCustomers.Remove(itemReturned);
            }
            await _db.SaveChangesAsync();
            //ProductVariant içeriisnde adet bilgisini sil (ExchangeProductVariantId)

            await RemoveItemQuantity(exchangeGoodItem.ExchangeProductVariantId,
                exchangeGoodItem.Quantity);

            //değişim talebi var mı diye kontrol edip eğer varsa adeti arttırıyor eğer yoksa da yenisini ekliyoruz galiba

            var returnedProductVariantInfo = await _db.ProductVariants
                .Include(pv => pv.productSize)
                .Include(pv => pv.productColor)
                .FirstOrDefaultAsync(pv => pv.Id == exchangeGoodItem.ReturnedProductVariantId);

            var existingExchangeItem = await _db.ExchangeOrderItems
                .FirstOrDefaultAsync(eo => eo.ItemExchangeRequestId == existingExchangeOrder.Id
                && eo.ReturnedProductVariantId == returnedProductVariantInfo.Id
                && eo.ExchangedProductVariantId == exchangeProductVariant.Id);
            if (existingExchangeItem != null)
            {
                existingExchangeItem.Quantity = existingExchangeItem.Quantity +
                    exchangeGoodItem.Quantity;

                await _db.SaveChangesAsync();
            }
            else
            {
                var exchangeOrderItem = new ExchangeOrderItem();
                exchangeOrderItem.ItemExchangeRequestId = existingExchangeOrder.Id;
                exchangeOrderItem.BaseProductId = exchangeProductVariant.BaseProductId;
                exchangeOrderItem.BaseProductName = exchangeProductVariant.baseProduct.Name;
                exchangeOrderItem.ReturnedProductVariantId = returnedProductVariantInfo.Id;
                exchangeOrderItem.ReturnedProductVariantColor =
                    returnedProductVariantInfo.productColor.Name;
                exchangeOrderItem.ReturnedProductVariantSize =
                    returnedProductVariantInfo.productSize.Name;
                exchangeOrderItem.ExchangedProductVariantId = exchangeProductVariant.Id;
                exchangeOrderItem.ExchangedProductVariantColor =
                    exchangeProductVariant.productColor.Name;
                exchangeOrderItem.ExchangedProductVariantSize =
                    exchangeProductVariant.productSize.Name;
                exchangeOrderItem.PricePerItemPaid = pricePerItem;
                exchangeOrderItem.Quantity = exchangeGoodItem.Quantity;
                exchangeOrderItem.Message = "Değişime uygundur";

                await _db.ExchangeOrderItems.AddAsync(exchangeOrderItem);
                await _db.SaveChangesAsync();

            }

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "Ürün değişimi başarıyla eklendi"
            };

        }

        public async Task<Response_IsSuccess> RemoveExchangeGoodItem(Request_RemoveExchangeGoodItem exchangeGoodItem)
        {
            //değişilecek ürünü buluyoruz
            var exchangeGoodItemFromDb = await _db.ExchangeOrderItems
                .FirstOrDefaultAsync(eo => eo.Id == exchangeGoodItem.ExchangeItemId);

            if (exchangeGoodItemFromDb == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "verilen id ile eşleşen öğe bulunamadı"
                };
            }
            
            var exchangeOrder = await _db.ItemExchangeRequests
                .FirstOrDefaultAsync(ie => ie.Id == exchangeGoodItemFromDb.ItemExchangeRequestId);
            if (exchangeOrder.RequestClosed == true)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişim talebi kapatıldı. üzerinde değişiklik yapılamaz"
                };
            }


            if (exchangeGoodItemFromDb.Quantity < exchangeGoodItem.Quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişim talebi içerisinde yeterli öğe yok"
                };
            }


            var pricePerItem = exchangeGoodItemFromDb.PricePerItemPaid;

            var returnItemAtCustomer = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == exchangeOrder.OrderId
                && ri.ProductVariantId == exchangeGoodItemFromDb.ReturnedProductVariantId);

            if (returnItemAtCustomer == null)
            {
                var returnedItemFromCustomer = new ReturnedItemsFromCustomer();

                returnedItemFromCustomer.OrderId = exchangeOrder.OrderId;

                returnedItemFromCustomer.BaseProductId =
                    exchangeGoodItemFromDb.BaseProductId;

                returnedItemFromCustomer.BaseProductName =
                    exchangeGoodItemFromDb.BaseProductName;

                returnedItemFromCustomer.ProductVariantId =
                    exchangeGoodItemFromDb.ReturnedProductVariantId;

                returnedItemFromCustomer.ProductVariantColor =
                    exchangeGoodItemFromDb.ReturnedProductVariantColor;

                returnedItemFromCustomer.ProductVariantSize =
                    exchangeGoodItemFromDb.ReturnedProductVariantSize;

                returnedItemFromCustomer.PricePerItem = pricePerItem;

                returnedItemFromCustomer.Quantity = exchangeGoodItem.Quantity;

                await _db.returnedItemsFromCustomers.AddAsync(returnedItemFromCustomer);
                await _db.SaveChangesAsync();
            }
            else
            {
                returnItemAtCustomer.Quantity = returnItemAtCustomer.Quantity
                    + exchangeGoodItem.Quantity;
            }


            await AddItemQuantity(exchangeGoodItemFromDb.ExchangedProductVariantId,
                exchangeGoodItemFromDb.Quantity);
            //değişilen ürünü kaldırıyoruz
            _db.ExchangeOrderItems.Remove(exchangeGoodItemFromDb);
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "değişilen ürünler silindi"
            };

        }

        public async Task<Response_AllExchangePendingItems> GetAllExchangePendingItems(string exchangeUniqueIdentifier)
        {
            var existingExchange = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeItemsPending)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);

            if (existingExchange == null)
            {
                return new Response_AllExchangePendingItems()
                {
                    isSuccess = false,
                    Message = "verilen id ile eşleşen değişim talebi bulunamadı"
                };
            }

            var exchangePendingItemsResponse = existingExchange.ConverToDtoPendingItems();
            exchangePendingItemsResponse.isSuccess = true;
            exchangePendingItemsResponse.Message = "ürünler başarıyla listelendi";

            return exchangePendingItemsResponse;
        }

        public async Task<Response_IsSuccess> AddExchangePendingItem(Request_AddExchangePendingItem exchangePendingItem)
        {
            //değişim talebi var mı kontrolü
            var existingExchange = await _db.ItemExchangeRequests
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier ==
                exchangePendingItem.ExchangeUniqueIdentifier);

            if (existingExchange == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişim talebi bulunamadı"
                };
            }
            //talep var mı kontrolü
            var existingOrder = await _db.Orders
                .Include(o => o.ReturnedItemsFromCustomers)
                .FirstOrDefaultAsync(o => o.OrderId == existingExchange.OrderId);

            if (existingOrder == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "talep bulunamadı"
                };
            }
            //öğe müşterinin iade edilenler listesinde var mı kontrolü yapılıyor galiba
            //değişim için yeterli miktar var mı kontrolü
            bool itemExist = false;
            bool enoughQty = false;

            foreach (var item in existingOrder.ReturnedItemsFromCustomers)
            {
                if (item.ProductVariantId == exchangePendingItem.ReturnedProductVariantId)
                {
                    itemExist = true;
                    if (item.Quantity >= exchangePendingItem.Quantity)
                    {
                        enoughQty = true;
                    }
                }
            }

            if (itemExist == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "No item exist at returned items from customer"
                };
            }
            if (enoughQty == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "Not enough items at returned items to exchange"
                };
            }
            //iade edilen ürünleri getiriyoruz
            var itemAtReturns = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == existingOrder.OrderId
                && ri.ProductVariantId == exchangePendingItem.ReturnedProductVariantId);
            
            var existingPendingItem = await _db.ExchangeItemsPending
                .FirstOrDefaultAsync(ei => ei.ItemExchangeRequestId == existingExchange.Id
                && ei.ReturnedProductVariantId == itemAtReturns.ProductVariantId);

            if (existingPendingItem == null)
            {
                ExchangeItemPending exchangeItemPending = new ExchangeItemPending()
                {
                    ItemExchangeRequestId = existingExchange.Id,
                    BaseProductId = itemAtReturns.BaseProductId,
                    BaseProductName = itemAtReturns.BaseProductName,
                    PricePerItemPaid = itemAtReturns.PricePerItem,
                    ReturnedProductVariantId = itemAtReturns.ProductVariantId,
                    ReturnedProductVariantColor = itemAtReturns.ProductVariantColor,
                    ReturnedProductVariantSize = itemAtReturns.ProductVariantSize,
                    Quantity = exchangePendingItem.Quantity,
                    Message = exchangePendingItem.Message
                };

                await _db.ExchangeItemsPending.AddAsync(exchangeItemPending);
                await _db.SaveChangesAsync();
            }
            else
            {
                exchangePendingItem.Quantity = exchangePendingItem.Quantity +
                    exchangePendingItem.Quantity;
                exchangePendingItem.Message = exchangePendingItem.Message;
                await _db.SaveChangesAsync();
            }
            //ürünü yada miktarını iade edilenlerden kaldırıyoruz
            itemAtReturns.Quantity = itemAtReturns.Quantity - exchangePendingItem.Quantity;
            if (itemAtReturns.Quantity == 0)
            {
                _db.returnedItemsFromCustomers.Remove(itemAtReturns);

            }
            await _db.SaveChangesAsync();
            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "ürün bekleme listesine alındı"
            };

        }

        public async Task<Response_IsSuccess> RemoveExchangePendingItem(Request_RemoveExchangePendingItem exchangePendingItem)
        {
            // ürün beklemede mi kontrolü
            var pendingItem = await _db.ExchangeItemsPending
                .FirstOrDefaultAsync(ei => ei.Id == exchangePendingItem.ExchangeItemId);
            if (pendingItem == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "bekleyen ürün bulunamadı"
                };
            }
            // ürünü iade edilenlere alma
            var existingExchange = await _db.ItemExchangeRequests
                .FirstOrDefaultAsync(ie => ie.Id == pendingItem.ItemExchangeRequestId);

            var existingItemAtReturns = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == existingExchange.OrderId
                && ri.ProductVariantId == pendingItem.ReturnedProductVariantId);
            if (existingItemAtReturns == null)
            {
                var itemAtReturns = new ReturnedItemsFromCustomer()
                {
                    OrderId = existingExchange.OrderId,
                    BaseProductId = pendingItem.BaseProductId,
                    BaseProductName = pendingItem.BaseProductName,
                    ProductVariantId = pendingItem.ReturnedProductVariantId,
                    ProductVariantColor = pendingItem.ReturnedProductVariantColor,
                    ProductVariantSize = pendingItem.ReturnedProductVariantSize,
                    PricePerItem = pendingItem.PricePerItemPaid,
                    Quantity = exchangePendingItem.Quantity
                };

                await _db.returnedItemsFromCustomers.AddAsync(itemAtReturns);
                await _db.SaveChangesAsync();
            }
            else
            {
                existingItemAtReturns.Quantity = existingItemAtReturns.Quantity
                    + exchangePendingItem.Quantity;

                await _db.SaveChangesAsync();
            }
            // ürünü bekleyenlerden kaldırıyoruz eğer miktarı sıfırsa 
            pendingItem.Quantity = pendingItem.Quantity - exchangePendingItem.Quantity;
            if (pendingItem.Quantity == 0)
            {
                _db.ExchangeItemsPending.Remove(pendingItem);
            }
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "bekleyen ürün silindi"
            };

        }

        public async Task<Response_IsSuccess> MovePendingItemToGood(Request_MovePendingToGood movePendingToGood)
        {
            //bekleyen ürün kontrolü
            var pendingItem = await _db.ExchangeItemsPending
                .FirstOrDefaultAsync(ei => ei.Id == movePendingToGood.PendingItemId);
            if (pendingItem == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "No pending item exist with given pending item Id"
                };
            }
            //ürün varyantı(spesifik bir renk ve beden) var mı kontrolü
            var productVariant = await _db.ProductVariants
                .Include(pv => pv.productSize)
                .Include(pv => pv.productColor)
                .FirstOrDefaultAsync(pv => pv.Id == movePendingToGood.ExchangeProductVariantId);
            if (productVariant == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "ürün varyantı bulunamadı"
                };
            }

            if (pendingItem.Quantity < movePendingToGood.Quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "bekleyen ürünlerde değişime yetecek miktar yok"
                };
            }

            if (productVariant.Quantity < movePendingToGood.Quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "stokta değişime yetecek miktar yok"
                };
            }
            
            if (productVariant.BaseProductId != pendingItem.BaseProductId)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişilecek ürün ile bekleyen ürün aynı ürünün varyantları değiller"
                };
            }
            //bekleyen ürün adetini ya da kendisini silme
            pendingItem.Quantity = pendingItem.Quantity - movePendingToGood.Quantity;
            if (pendingItem.Quantity == 0)
            {
                _db.ExchangeItemsPending.Remove(pendingItem);
            }
            await _db.SaveChangesAsync();
            //ürün varyantından adet azaltmaca
            await RemoveItemQuantity(productVariant.Id, movePendingToGood.Quantity);
            //değişime uygun ürün kontrolü yapıp varsa adet arttır yoksa oluşturuyoruz galiba
            var existingExchangeOrderItem = await _db.ExchangeOrderItems
                .FirstOrDefaultAsync(eo => eo.ItemExchangeRequestId ==
                pendingItem.ItemExchangeRequestId
                && eo.ReturnedProductVariantId == pendingItem.ReturnedProductVariantId
                && eo.ExchangedProductVariantId == productVariant.Id);
            if (existingExchangeOrderItem == null)
            {
                var exchangeOrderItem = new ExchangeOrderItem();
                exchangeOrderItem.ItemExchangeRequestId = pendingItem.ItemExchangeRequestId;
                exchangeOrderItem.BaseProductId = pendingItem.BaseProductId;
                exchangeOrderItem.BaseProductName = pendingItem.BaseProductName;

                exchangeOrderItem.ReturnedProductVariantId =
                    pendingItem.ReturnedProductVariantId;

                exchangeOrderItem.ReturnedProductVariantColor =
                    pendingItem.ReturnedProductVariantColor;

                exchangeOrderItem.ReturnedProductVariantSize =
                    pendingItem.ReturnedProductVariantSize;

                exchangeOrderItem.ExchangedProductVariantId = productVariant.Id;

                exchangeOrderItem.ExchangedProductVariantColor =
                    productVariant.productColor.Name;

                exchangeOrderItem.ExchangedProductVariantSize =
                    productVariant.productSize.Name;

                exchangeOrderItem.PricePerItemPaid = pendingItem.PricePerItemPaid;
                exchangeOrderItem.Quantity = movePendingToGood.Quantity;
                exchangeOrderItem.Message = "ürünler değişime uygundur";

                await _db.ExchangeOrderItems.AddAsync(exchangeOrderItem);
                await _db.SaveChangesAsync();
            }
            else
            {
                existingExchangeOrderItem.Quantity = existingExchangeOrderItem.Quantity
                    + movePendingToGood.Quantity;

                await _db.SaveChangesAsync();
            }


            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "ürün değişilen ürünlere gönderildi"
            };

        }

        public async Task<Response_IsSuccess> MovePendingItemToBad(Request_MovePendingToBad movePendingToBad)
        {
            //değişilen ürünler var mı kontrolü
            var pendingItem = await _db.ExchangeItemsPending
                .FirstOrDefaultAsync(ei => ei.Id == movePendingToBad.PendingItemId);
            if (pendingItem == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "bekleyen ürün buluanamdı"
                };
            }
            //bekleyen ürünün miktarının yeterliliği kontrolü
            if (pendingItem.Quantity < movePendingToBad.Quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "bekleyen üründen yeterli miktarda bulunmamaktadır"
                };
            }
            
            var existingCanceledItem = await _db.ExchangeItemsCanceled
                .FirstOrDefaultAsync(ei => ei.ItemExchangeRequestId == pendingItem.ItemExchangeRequestId
                && ei.ReturnedProductVariantId == pendingItem.ReturnedProductVariantId);
            if (existingCanceledItem == null)
            {
                var exchangeItemCanceled = new ExchangeItemCanceled();
                exchangeItemCanceled.ItemExchangeRequestId = pendingItem.ItemExchangeRequestId;
                exchangeItemCanceled.BaseProductId = pendingItem.BaseProductId;
                exchangeItemCanceled.BaseProductName = pendingItem.BaseProductName;
                exchangeItemCanceled.PricePerItemPaid = pendingItem.PricePerItemPaid;

                exchangeItemCanceled.ReturnedProductVariantId =
                    pendingItem.ReturnedProductVariantId;

                exchangeItemCanceled.ReturnedProductVariantSize =
                    pendingItem.ReturnedProductVariantSize;

                exchangeItemCanceled.ReturnedProductVariantColor =
                    pendingItem.ReturnedProductVariantColor;

                exchangeItemCanceled.Quantity = movePendingToBad.Quantity;
                exchangeItemCanceled.CancelationReason = movePendingToBad.Message;

                await _db.ExchangeItemsCanceled.AddAsync(exchangeItemCanceled);

                await _db.SaveChangesAsync();
            }
            else
            {
                existingCanceledItem.Quantity = existingCanceledItem.Quantity +
                    movePendingToBad.Quantity;
                existingCanceledItem.CancelationReason = movePendingToBad.Message;

                await _db.SaveChangesAsync();
            }
            

            pendingItem.Quantity = pendingItem.Quantity - movePendingToBad.Quantity;
            if (pendingItem.Quantity == 0)
            {
                _db.ExchangeItemsPending.Remove(pendingItem);
            }
            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "öğeler iptal edilenlere gönderildi"
            };
        }

        public async Task<Response_AllExchangeBadItems> GetAllExchangeBadItems(string exchangeUniqueIdentifier)
        {
            
            var exchangeOrder = await _db.ItemExchangeRequests
                .Include(ie => ie.exchangeItemsCanceled)
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier == exchangeUniqueIdentifier);
            //değişim var mı kontrolü
            if (exchangeOrder == null)
            {
                return new Response_AllExchangeBadItems()
                {
                    isSuccess = false,
                    Message = "değişim talebi bulunamadı"
                };
            }
            
            //DTO'ya dönüştürmece eklenmelidir
            var allExchangeBadItemsResponse = exchangeOrder.ConvertToDtoBadItems();
            allExchangeBadItemsResponse.isSuccess = true;
            allExchangeBadItemsResponse.Message = "tüm öğeler değişime uygun değil";

            return allExchangeBadItemsResponse;

        }

        public async Task<Response_IsSuccess> AddExchangeBadItem(Request_AddExchangeBadItem addExchangeBadItem)
        {
            //değişim talebi var mı kontrolü
            var existingExchange = await _db.ItemExchangeRequests
                .FirstOrDefaultAsync(ie => ie.ExchangeUniqueIdentifier ==
                addExchangeBadItem.ExchangeUniqueIdentifier);
            if (existingExchange == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişim talebi bulunamadı"
                };
            }
            //değişim talebi kapalı mı kontrolü
            if (existingExchange.RequestClosed == true)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişim talebi kapalıdır. üzerinde değişiklik yapılamaz"
                };
            }
            //talep var mı kontrolü
            var existingOrder = await _db.Orders
                .Include(o => o.ReturnedItemsFromCustomers)
                .FirstOrDefaultAsync(o => o.OrderId == existingExchange.OrderId);

            if (existingOrder == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "talep bulunamadı"
                };
            }

            //müşteride yeterince var mı kontrolü
            bool itemExistInReturns = false;
            bool enoughQty = false;
            int existingItemAtReturnsId = 0;

            foreach (var item in existingOrder.ReturnedItemsFromCustomers)
            {
                if (item.ProductVariantId == addExchangeBadItem.ReturnedProductVariantId)
                {
                    itemExistInReturns = true;
                    existingItemAtReturnsId = item.Id;
                    if (item.Quantity >= addExchangeBadItem.Quantity)
                    {
                        enoughQty = true;
                    }
                }
            }

            if (itemExistInReturns == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "iadeler içerisinde id'si verilen ürün bulunamadı"
                };
            }
            if (enoughQty == false)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "İade edilen ürünlerde değiştirilecek yeterli ürün yok"
                };
            }

            //badItems'te ProductVariant'ın zaten mevcut olup olmadığını kontrol edip ona göre eklememiz gerekiyor
            var existingBadItemAtCustomer = await _db.ExchangeItemsCanceled
                .FirstOrDefaultAsync(ei => ei.ItemExchangeRequestId == existingExchange.Id
                && ei.ReturnedProductVariantId == addExchangeBadItem.ReturnedProductVariantId);
            if (existingBadItemAtCustomer == null)
            {
                var existingItemAtReturns = await _db.returnedItemsFromCustomers
                    .FirstOrDefaultAsync(ri => ri.OrderId == existingExchange.OrderId
                    && ri.ProductVariantId == addExchangeBadItem.ReturnedProductVariantId);

                var canceledItem = new ExchangeItemCanceled()
                {
                    ItemExchangeRequestId = existingExchange.Id,
                    BaseProductId = existingItemAtReturns.BaseProductId,
                    BaseProductName = existingItemAtReturns.BaseProductName,
                    ReturnedProductVariantId = existingItemAtReturns.ProductVariantId,
                    ReturnedProductVariantColor = existingItemAtReturns.ProductVariantColor,
                    ReturnedProductVariantSize = existingItemAtReturns.ProductVariantSize,
                    PricePerItemPaid = existingItemAtReturns.PricePerItem,
                    Quantity = addExchangeBadItem.Quantity,
                    CancelationReason = addExchangeBadItem.Message
                };

                await _db.ExchangeItemsCanceled.AddAsync(canceledItem);
                await _db.SaveChangesAsync();
            }
            else
            {
                existingBadItemAtCustomer.Quantity = existingBadItemAtCustomer.Quantity
                    + addExchangeBadItem.Quantity;

                await _db.SaveChangesAsync();
            }

            //iade edilen ürünlerde adet bilgisini değiştirmece
            var itemInReturns = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.Id == existingItemAtReturnsId);

            itemInReturns.Quantity = itemInReturns.Quantity - addExchangeBadItem.Quantity;
            if (itemInReturns.Quantity == 0)
            {
                _db.returnedItemsFromCustomers.Remove(itemInReturns);
            }

            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "ürün iade edilemeyenlere eklendi"
            };

        }

        public async Task<Response_IsSuccess> RemoveExchangeBadItem(Request_RemoveExchangeBadItem removeExchangeBadItem)
        {
            
            var existingCanceledItem = await _db.ExchangeItemsCanceled
                .FirstOrDefaultAsync(ei => ei.Id == removeExchangeBadItem.ExchangeItemId);
            if (existingCanceledItem == null)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "iptal edilenlerde bulunamadı"
                };
            }
            
            if (existingCanceledItem.Quantity < removeExchangeBadItem.Quantity)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "iptal edilenlerde yeterli ürün yok"
                };
            }

            var existingExchangeOrder = await _db.ItemExchangeRequests
                .FirstOrDefaultAsync(ie => ie.Id == existingCanceledItem.ItemExchangeRequestId);

            if (existingExchangeOrder.RequestClosed == true)
            {
                return new Response_IsSuccess()
                {
                    isSuccess = false,
                    Message = "değişim taalebi kapalı. üzerinde değişiklik yapılamaz"
                };
            }
            
            var existingItemInReturns = await _db.returnedItemsFromCustomers
                .FirstOrDefaultAsync(ri => ri.OrderId == existingExchangeOrder.OrderId
                && ri.ProductVariantId == existingCanceledItem.ReturnedProductVariantId);
            if (existingItemInReturns == null)
            {
                var returnedItemFromCustomer = new ReturnedItemsFromCustomer()
                {
                    OrderId = existingExchangeOrder.OrderId,
                    BaseProductId = existingCanceledItem.BaseProductId,
                    BaseProductName = existingCanceledItem.BaseProductName,
                    ProductVariantId = existingCanceledItem.ReturnedProductVariantId,
                    ProductVariantColor = existingCanceledItem.ReturnedProductVariantColor,
                    ProductVariantSize = existingCanceledItem.ReturnedProductVariantSize,
                    PricePerItem = existingCanceledItem.PricePerItemPaid,
                    Quantity = removeExchangeBadItem.Quantity
                };

                await _db.returnedItemsFromCustomers.AddAsync(returnedItemFromCustomer);
                await _db.SaveChangesAsync();

            }
            else
            {
                existingItemInReturns.Quantity = existingItemInReturns.Quantity
                    + removeExchangeBadItem.Quantity;

                await _db.SaveChangesAsync();
            }


            existingCanceledItem.Quantity = existingCanceledItem.Quantity
                - removeExchangeBadItem.Quantity;
            if (existingCanceledItem.Quantity == 0)
            {
                _db.ExchangeItemsCanceled.Remove(existingCanceledItem);
            }

            await _db.SaveChangesAsync();

            return new Response_IsSuccess()
            {
                isSuccess = true,
                Message = "ürün iade edilemeyecekler listesinden çıkarıldı"
            };

        }


        #region HelperFunctions
        private async Task<string> GenerateUniqueExchangeIdentifierForExchange()
        {
            char[] letters = "ABCÇDEFGĞHIİJKLMNOÖPQRSŞTUÜVWXYZ".ToCharArray();
            Random random = new Random();
            string ramdomLetters = "";

            for (int i = 0; i < 3; i++)
            {
                ramdomLetters += letters[random.Next(letters.Length)];
            }

            int randomNumber = random.Next(100000000, 999999999);

            string ExchangeUniqueIdentifier = ramdomLetters + randomNumber.ToString();

            var existingIdentifierInReturns = await _db.exchangeRequestsFromUsers
                .FirstOrDefaultAsync(er => er.ExchangeUniqueIdentifier == ExchangeUniqueIdentifier);
            if (existingIdentifierInReturns != null)
            {
                GenerateUniqueExchangeIdentifierForExchange();
            }

            return ExchangeUniqueIdentifier;
        }

        private async Task<bool> RemoveItemQuantity(int productVariantId, int quantity)
        {
            var existingProductVariant = await _db.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == productVariantId);
            existingProductVariant.Quantity = existingProductVariant.Quantity - quantity;
            await _db.SaveChangesAsync();

            return true;
        }
        private async Task<bool> AddItemQuantity(int productVariantId, int quanity)
        {
            var existingProductVariant = await _db.ProductVariants
                .FirstOrDefaultAsync(pv => pv.Id == productVariantId);
            existingProductVariant.Quantity = existingProductVariant.Quantity + quanity;

            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<string> CreateExchangePdf(ItemExchangeRequest itemExchangeRequest, string userId)
        {
            var date = DateTime.Now.ToShortDateString().ToString();
            var dateNormilized = date.Replace("/", "_");
            string fileName = "PDF_" + dateNormilized + "_" + DateTime.UtcNow.Millisecond + ".pdf";

            string folderPath = _webHostEnvironment.WebRootPath + $"\\PDF\\{userId}\\{itemExchangeRequest.OrderUniqueIdentifier}";
            string path = System.IO.Path.Combine(folderPath, fileName);

            decimal TotalDiscountOfAllItems = 0;
            decimal TotalPriceOfAllItems = 0;

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            PdfDocument pdfDoc = new PdfDocument(new PdfWriter(path));
            iText.Layout.Document doc = new iText.Layout.Document(pdfDoc, PageSize.A4, true);
            doc.SetMargins(25, 25, 25, 25);

            var font = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.TIMES_ROMAN);
            var aligmentLeft = iText.Layout.Properties.TextAlignment.LEFT;
            var aligmentCenter = iText.Layout.Properties.TextAlignment.CENTER;
            string imagePath = _webHostEnvironment.WebRootPath + "\\ImageForPDF\\Logo.png";
            iText.Layout.Element.Image image = new iText.Layout.Element.Image
                (ImageDataFactory.Create(imagePath));
            image.SetFixedPosition(400, 700);
            image.ScaleToFit(90, 90);
            doc.Add(image);

            //müşteri bilgisi

            Paragraph name = new Paragraph(itemExchangeRequest.UserFirstName + " " + itemExchangeRequest.UserLastName)
                .SetFont(font)
                .SetFontSize(12)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(name);

            Paragraph email = new Paragraph(itemExchangeRequest.UserEmail)
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(email);

            Paragraph street = new Paragraph(itemExchangeRequest.Street)
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(street);

            if (itemExchangeRequest.ApartmentNumber == null || itemExchangeRequest.ApartmentNumber == 0)
            {
                Paragraph addressHouseNum = new Paragraph(itemExchangeRequest.HouseNumber.ToString())
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
                doc.Add(addressHouseNum);
            }
            else
            {
                Paragraph addressHouseApp = new Paragraph(itemExchangeRequest.HouseNumber.ToString() + "-" + itemExchangeRequest.ApartmentNumber.ToString())
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
                doc.Add(addressHouseApp);
            }

            Paragraph addressCity = new Paragraph(itemExchangeRequest.City)
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(addressCity);

            Paragraph addressRegion = new Paragraph(itemExchangeRequest.Region)
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(addressRegion);

            Paragraph addressCountry = new Paragraph(itemExchangeRequest.Country)
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(addressCountry);

            Paragraph postalCode = new Paragraph(itemExchangeRequest.PostalCode)
                .SetFont(font)
                .SetFontSize(14)
                .SetTextAlignment(aligmentLeft)
                .SetMarginBottom(0);
            doc.Add(postalCode);

            Paragraph busineesName = new Paragraph("MESESOFT")
               .SetFont(font)
               .SetFontSize(12)
               .SetTextAlignment(aligmentCenter)
               .SetPaddingTop(20);
            doc.Add(busineesName);

            Paragraph orderId = new Paragraph("Sipariş faturası: " + itemExchangeRequest.OrderUniqueIdentifier)
               .SetFont(font)
               .SetFontSize(12)
               .SetTextAlignment(aligmentCenter)
               .SetPaddingTop(5);
            doc.Add(orderId);

            if (itemExchangeRequest.exchangeOrderItems.Count > 0)
            {
                Table ExchangeGoodTable = new Table(6);
                ExchangeGoodTable.SetMarginTop(20);
                ExchangeGoodTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);
                ExchangeGoodTable.SetWidth(500);

                //iText.Kernel.Colors
                Color textColorTableHeadings = new DeviceRgb(255, 255, 255);
                Color bgColorTableHeadings = new DeviceRgb(1, 1, 1);

                Cell cell1 = new Cell().Add(new Paragraph("ÜRÜN ADI")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell1.SetBackgroundColor(bgColorTableHeadings);
                cell1.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeGoodTable.AddCell(cell1);

                Cell cell2 = new Cell().Add(new Paragraph("İADE EDİLEN ÜRÜN RENGİ")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell2.SetBackgroundColor(bgColorTableHeadings);
                cell2.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeGoodTable.AddCell(cell2);

                Cell cell3 = new Cell().Add(new Paragraph("İADE EDİLEN ÜRÜN BEDENİ")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell3.SetBackgroundColor(bgColorTableHeadings);
                cell3.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeGoodTable.AddCell(cell3);

                Cell cell4 = new Cell().Add(new Paragraph("DEĞİŞTİRİLEN ÜRÜN RENGİ")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell4.SetBackgroundColor(bgColorTableHeadings);
                cell4.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeGoodTable.AddCell(cell4);

                Cell cell5 = new Cell().Add(new Paragraph("DEĞİŞTİRİLEN ÜRÜN BEDENİ")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell5.SetBackgroundColor(bgColorTableHeadings);
                cell5.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeGoodTable.AddCell(cell5);

                Cell cell6 = new Cell().Add(new Paragraph("ADET")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell6.SetBackgroundColor(bgColorTableHeadings);
                cell6.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeGoodTable.AddCell(cell6);

                foreach (var item in itemExchangeRequest.exchangeOrderItems)
                {
                    Cell CellProductName = new Cell().Add(new Paragraph(item.BaseProductName)
                    .SetTextAlignment(aligmentCenter)
                    .SetFontSize(8));
                    CellProductName.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeGoodTable.AddCell(CellProductName);

                    Cell ReturnedProductColor = new Cell().Add(new Paragraph(item.ReturnedProductVariantColor)
                    .SetTextAlignment(aligmentCenter)
                    .SetFontSize(8));
                    ReturnedProductColor.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeGoodTable.AddCell(ReturnedProductColor);

                    Cell ReturnedProductSize = new Cell().Add(new Paragraph(item.ReturnedProductVariantSize)
                   .SetTextAlignment(aligmentCenter)
                   .SetFontSize(8));
                    ReturnedProductSize.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeGoodTable.AddCell(ReturnedProductSize);

                    Cell ExchangedProductColor = new Cell().Add(new Paragraph(item.ExchangedProductVariantColor)
                    .SetTextAlignment(aligmentCenter)
                    .SetFontSize(8));
                    ExchangedProductColor.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeGoodTable.AddCell(ExchangedProductColor);

                    Cell ExchangedProductSize = new Cell().Add(new Paragraph(item.ExchangedProductVariantSize)
                   .SetTextAlignment(aligmentCenter)
                   .SetFontSize(8));
                    ExchangedProductSize.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeGoodTable.AddCell(ExchangedProductSize);

                    Cell Quantity = new Cell().Add(new Paragraph(item.Quantity.ToString())
                   .SetTextAlignment(aligmentCenter)
                   .SetFontSize(8));
                    Quantity.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeGoodTable.AddCell(Quantity);
                }
                doc.Add(ExchangeGoodTable);
            }

            

            if (itemExchangeRequest.exchangeItemsCanceled.Count > 0)
            {
                Table ExchangeBadTable = new Table(5);
                ExchangeBadTable.SetMarginTop(20);
                ExchangeBadTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);
                ExchangeBadTable.SetWidth(500);

                //iText.Kernel.Colors
                Color textColorTableHeadings = new DeviceRgb(255, 255, 255);
                Color bgColorTableHeadings = new DeviceRgb(1, 1, 1);

                Cell cell1 = new Cell().Add(new Paragraph("ÜRÜN ADI")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell1.SetBackgroundColor(bgColorTableHeadings);
                cell1.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeBadTable.AddCell(cell1);

                Cell cell2 = new Cell().Add(new Paragraph("İADE EDİLEN ÜRÜN RENGİ")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell2.SetBackgroundColor(bgColorTableHeadings);
                cell2.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeBadTable.AddCell(cell2);

                Cell cell3 = new Cell().Add(new Paragraph("İADE EDİLEN ÜRÜN BEDENİ")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell3.SetBackgroundColor(bgColorTableHeadings);
                cell3.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeBadTable.AddCell(cell3);

                Cell cell4 = new Cell().Add(new Paragraph("ADET")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell4.SetBackgroundColor(bgColorTableHeadings);
                cell4.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeBadTable.AddCell(cell4);

                Cell cell5 = new Cell().Add(new Paragraph("DEĞİŞİM NEDENİ YOK")
                .SetFontColor(textColorTableHeadings)
                .SetFontSize(8)
                .SetTextAlignment(aligmentCenter));
                cell5.SetBackgroundColor(bgColorTableHeadings);
                cell5.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                ExchangeBadTable.AddCell(cell5);

                foreach (var item in itemExchangeRequest.exchangeItemsCanceled)
                {
                    Cell CellProductName = new Cell().Add(new Paragraph(item.BaseProductName)
                    .SetTextAlignment(aligmentCenter)
                    .SetFontSize(8));
                    CellProductName.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeBadTable.AddCell(CellProductName);

                    Cell ReturnedProductColor = new Cell().Add(new Paragraph(item.ReturnedProductVariantColor)
                    .SetTextAlignment(aligmentCenter)
                    .SetFontSize(8));
                    ReturnedProductColor.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeBadTable.AddCell(ReturnedProductColor);

                    Cell ReturnedProductSize = new Cell().Add(new Paragraph(item.ReturnedProductVariantSize)
                   .SetTextAlignment(aligmentCenter)
                   .SetFontSize(8));
                    ReturnedProductSize.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeBadTable.AddCell(ReturnedProductSize);

                    Cell Quantity = new Cell().Add(new Paragraph(item.Quantity.ToString())
                   .SetTextAlignment(aligmentCenter)
                   .SetFontSize(8));
                    Quantity.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeBadTable.AddCell(Quantity);

                    Cell ReasonNoExchange = new Cell().Add(new Paragraph(item.CancelationReason)
                    .SetTextAlignment(aligmentCenter)
                    .SetFontSize(8));
                    ReasonNoExchange.SetBorder(new SolidBorder(ColorConstants.GRAY, 2));
                    ExchangeBadTable.AddCell(ReasonNoExchange);
                }

                doc.Add(ExchangeBadTable);
            }

            doc.Close();

            return path;
        }
        #endregion


    }
}