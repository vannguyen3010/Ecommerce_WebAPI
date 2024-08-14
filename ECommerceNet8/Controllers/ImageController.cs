﻿using ECommerceNet8.Data;
using ECommerceNet8.Models.ProductModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceNet8.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly Microsoft.AspNetCore.Hosting.IHostingEnvironment _hostingEnvironment;
        private readonly ApplicationDbContext _db;

        public ImageController(Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnvironment,
            ApplicationDbContext db)
        {
            _hostingEnvironment = hostingEnvironment;
            _db = db;
        }

        [HttpPost]
        [Route("UploadImage/{productBaseId}")]
        public async Task<IActionResult> UploadImage([FromRoute] int productBaseId)
        {
            try
            {
                var files = HttpContext.Request.Form.Files;

                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        FileInfo fi = new FileInfo(file.FileName);
                        var date = DateTime.UtcNow.ToShortDateString().ToString();
                        var dateNormalized = date.Replace("/", "_");

                        var newFileName = "Image_" + dateNormalized + "_"
                            + DateTime.UtcNow.TimeOfDay.Microseconds + fi.Extension;

                        var folderPath = Path.Combine("", _hostingEnvironment.ContentRootPath
                            + "\\" + "wwwroot\\" + "Images\\" + "ProductId_" + productBaseId);

                        var path = Path.Combine("", _hostingEnvironment.ContentRootPath
                            + "\\" + "wwwroot\\" + "Images\\" + "ProductId_" + productBaseId +
                            "\\" + newFileName);

                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        using (var stream = new FileStream(path, FileMode.Create))
                        {

                            file.CopyTo(stream);
                        }

                        string protocol = HttpContext.Request.IsHttps ? "https" : "http";
                        var domain = HttpContext.Request.Host.ToString();
                        var domainPath = protocol + "://" + domain;
                        var staticPath = Path.Combine(domainPath, "Images\\" + "ProductId_" + productBaseId
                            + "\\" + newFileName);

                        ImageBase imageBase = new ImageBase();
                        imageBase.ImagePath = path;
                        imageBase.AddedOn = DateTime.UtcNow;
                        imageBase.BaseProductId = productBaseId;
                        imageBase.StaticPath = staticPath;

                        await _db.ImageBases.AddAsync(imageBase);
                        await _db.SaveChangesAsync();

                    }

                    return Ok("Resimler başarıyla yüklendi");
                }
                else
                {
                    return BadRequest("Lütfen yüklenecek dosyaları seçiniz");
                }
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        [HttpDelete]
        [Route("DeleteImage/{baseImageId}")]
        public async Task<IActionResult> DeleteImage([FromRoute] int baseImageId)
        {
            var existingImage = await _db.ImageBases    
                .FirstOrDefaultAsync(ib => ib.Id == baseImageId);

            if (existingImage == null)
            {
                return NotFound("Belirtilen id ile eşleşen fotoğraf bulunamadı");
            }

            if (System.IO.File.Exists(existingImage.ImagePath))
            {
                System.IO.File.Delete(existingImage.ImagePath);
            }

            _db.ImageBases.Remove(existingImage);
            await _db.SaveChangesAsync();

            return Ok("Fotoğraf başarıyla silindi");
        }
    }
}