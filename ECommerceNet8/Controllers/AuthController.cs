using ECommerceNet8.DTOs.ApiUserDtos.request;
using ECommerceNet8.DTOs.ApiUserDtos.response;
using ECommerceNet8.Models.AuthModels;
using ECommerceNet8.Repositories.AuthRepository;
using ECommerceNet8.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Security.Claims;
using System.Text;

namespace ECommerceNet8.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _authRepository;
        private readonly UserManager<ApiUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ISendGridClient _sendGridClient;

        public AuthController(IAuthRepository authRepository,
            UserManager<ApiUser> userManager,
            IConfiguration configuration,
            ISendGridClient sendGridClient
            )
        {
            _authRepository = authRepository;
            _userManager = userManager;
            _configuration = configuration;
            _sendGridClient = sendGridClient;
        }

        #region
        [HttpPost]
        [Route("register")]
        public async Task<ActionResult<Response_ApiUserRegisterDto>> Register
          ([FromBody] Request_ApiUserRegisterDto request_ApiUserRegisterDto)
        {
            // Đăng ký người dùng
            var userDto = await _authRepository.Register(request_ApiUserRegisterDto);

            //Kiểm tra kết quả đăng ký
            if (userDto.isSuccess == false)
            {
                return BadRequest(new Response_ApiUserRegisterDto()
                {
                    isSuccess = false,
                    Message = userDto.Message
                });
            }

            //Phần xác nhận người dùng
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(userDto.apiUser);

            var callbackUrl = Request.Scheme + "://" + Request.Host + Url.Action("ConfirmEmail", "Auth", new { userId = userDto.apiUser.Id, code = code });

            var body = EmailTemplates.EmailLinkTemplate(callbackUrl);

            //Gửi email
            //var result = await SendEmail(body, "vovannguyen30102001@gmail.com");
            var result = await SendEmail(body, userDto.apiUser.Email!);

            string EmailMessage = result ? "Đã gửi email" : "Có lỗi xảy ra trong khi gửi email";

            //Phản hồi thành công
            return Ok(new Response_ApiUserRegisterDto()
            {
                isSuccess = true,
                Message = new List<string>
                {
                    "User Registered",
                    EmailMessage
                }
            });
        }
        #endregion


        [HttpPost]
        [Route("registerAdmin/{secretKey}")]
        public async Task<ActionResult<Response_ApiUserRegisterDto>> RegisterAdmin(
            [FromRoute] int secretKey, [FromBody] Request_ApiUserRegisterDto request)
        {
            var userDto = await _authRepository.RegisterAdmin(request, secretKey);
            if (userDto.isSuccess == false)
            {
                return BadRequest(
                    new Response_ApiUserRegisterDto()
                    {
                        isSuccess = false,
                        Message = userDto.Message
                    });
            }

            return Ok(new Response_ApiUserRegisterDto()
            {
                isSuccess = true,
                Message = new List<string>
                {
                    "Người dùng quản trị được tạo thành công",
                    "Người dùng quản trị không cần xác minh email"
                }
            });
        }

        [HttpGet]
        [Route("ConfirmEmail")]
        public async Task<ActionResult<Response_ApiUserConfirmEmail>> ConfirmEmail(
            string userId, string code)
        {
            if (userId == null || code == null)
            {
                return BadRequest(new Response_ApiUserConfirmEmail()
                {
                    isSuccess = false,
                    Message = "Liên kết xác minh email không chính xác"
                });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new Response_ApiUserConfirmEmail()
                {
                    isSuccess = false,
                    Message = "ID người dùng được cung cấp không chính xác"
                });
            }

            var result = await _userManager.ConfirmEmailAsync(user, code);
            var status = result.Succeeded ? "Cám ơn đã xác nhận địa chỉ email của bạn." : "Địa chỉ email của bạn không thể được xác minh, vui lòng thử lại sau";

            return Ok(new Response_ApiUserConfirmEmail()
            {
                isSuccess = true,
                Message = status
            });
        }

        [HttpPost]
        [Route("login")]
        public async Task<ActionResult<Response_LoginDto>> Login(
            [FromBody] Request_LoginDto userLogin)
        {
            var authResponse = await _authRepository.Login(userLogin);
            if (authResponse.Result == false)
            {
                return BadRequest(authResponse);
            }

            return Ok(authResponse);
        }

        //private functionlar

        private async Task<bool> SendEmail(
            string body,
            string email,
            string subject = "Email Verification")
        {
            //Lấy thông tin người gửi từ cấu hình
            string fromEmail = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromEmail")!;
            string fromName = _configuration.GetSection("SendGridEmailSettings")
                .GetValue<string>("FromName")!;

            //Tạo đối tượng SendGridMessage
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(fromEmail, fromName),
                Subject = subject,
                HtmlContent = body
            };

            var emailToSend = email;

            msg.AddTo("vovannguyen30102001@gmail.com");

            //Gửi email qua SendGrid
            var response = await _sendGridClient.SendEmailAsync(msg);

            return response.IsSuccessStatusCode;
        }

        [HttpPost]
        [Route("GenerateTokens")]
        public async Task<ActionResult<Response_LoginDto>> GenerateTokens(
            [FromBody] Request_TokenDto request)
        {
            var authResponse = await _authRepository.VerifyAndGenerateTokens(request);
            if (authResponse.Result == false)
            {
                return BadRequest(authResponse);
            }

            return Ok(authResponse);
        }

        [Authorize]
        [HttpDelete("Logout")]
        public async Task<ActionResult> Logout()
        {
            string userId = HttpContext.User.FindFirstValue("uid")!;
            await _authRepository.LogoutDeleteRefreshToken(userId);

            return Ok("Đăng xuất hoàn tất thành công");
        }

        [HttpPost]
        [Route("ResendEmail/{email}")]
        public async Task<ActionResult<Response_ApiUserConfirmEmail>> ResendEmail(
            [FromRoute] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return BadRequest(new Response_ApiUserConfirmEmail()
                {
                    isSuccess = false,
                    Message = "Không tìm thấy người dùng nào có email"
                });
            }

            if (user.EmailConfirmed == true)
            {
                return BadRequest(new Response_ApiUserConfirmEmail()
                {
                    isSuccess = false,
                    Message = "Địa chỉ email đã được xác minh."
                });
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var callbackURL = Request.Scheme + "://" + Request.Host + Url.Action("ConfirmEmail", "Auth", new { userId = user.Id, code = code });

            var body = EmailTemplates.EmailLinkTemplate(callbackURL);

            //Email gönderme kısmı

            var result = await SendEmail(body, "test@email.com");
            string EmailMessage = result ? "Email gönderildi" : "Có lỗi xảy ra trong khi gửi email";

            return Ok(new Response_ApiUserConfirmEmail()
            {
                isSuccess = true,
                Message = EmailMessage
            });
        }

        [HttpGet]
        [Route("ResetPasswordSendEmail/{email}")]
        public async Task<IActionResult> ResetPasswordSendEmail(
            [FromRoute] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
            {
                return NotFound("Yanlış email adresi");
            }

            if (user.EmailConfirmed == false)
            {
                return BadRequest("Eposta Doğrulanmadı");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // sanırım özel karakter sorunu yaşamamak için token encoding yapıyoruz
            var encodedToken = Encoding.UTF8.GetBytes(token);
            var validToken = WebEncoders.Base64UrlEncode(encodedToken);

            var callbackURL = Request.Scheme + "://" + Request.Host + $"/ResetPassword?email={email}&token={validToken}";

            var body = EmailTemplates.EmailLinkTemplate(callbackURL);
            var result = await SendEmail(body, user.Email, "Password reset");

            if (result == false)
            {
                return BadRequest("không thể đặt lại mật khẩu");
            }

            return Ok("Email liên kết đặt lại mật khẩu đã được gửi.");
        }

        [HttpPost]
        [Route("ResetPassword")]
        public async Task<ActionResult<Response_PasswordResetDto>> ResetPassword(
            [FromBody] Request_PasswordResetDto passwordResetDto)
        {
            var user = await _userManager.FindByEmailAsync(passwordResetDto.Email);

            if (user == null)
            {
                return BadRequest(new Response_PasswordResetDto()
                {
                    isSuccess = false,
                    Message = "Không tìm thấy người dùng nào có email"
                });
            }

            if (passwordResetDto.newPassword != passwordResetDto.confirmPassword)
            {
                return BadRequest(new Response_PasswordResetDto()
                {
                    isSuccess = false,
                    Message = "Mật khẩu không phù hợp"
                });
            }

            //TOKEN DECODE İŞLEMİ
            var decodedToken = WebEncoders.Base64UrlDecode(passwordResetDto.Token);
            var normalizedToken = Encoding.UTF8.GetString(decodedToken);

            var result = await _userManager.ResetPasswordAsync(user, normalizedToken, passwordResetDto.newPassword);

            if (result.Succeeded)
            {
                return Ok(new Response_PasswordResetDto()
                {
                    isSuccess = true,
                    Message = "đặt lại mật khẩu thành công"
                });
            }

            List<string> errors = new List<string>();
            foreach (var error in result.Errors)
            {
                errors.Add(error.Description);
            }

            return BadRequest(new Response_PasswordResetDto()
            {
                isSuccess = false,
                Message = "Đã xảy ra lỗi khi thay đổi mật khẩu",
                Errors = errors
            });
        }

        [HttpPost]
        [Route("UpdateUserDetails")]
        [Authorize]

        public async Task<IActionResult> UpdateUser(
            [FromBody] Request_ApiUserDetailsUpdate userUpdate)
        {
            string userId = HttpContext.User.FindFirstValue("uid")!;

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return BadRequest("Không tìm thấy người dùng");
            }

            user.FirstName = userUpdate.FirstName;
            user.LastName = userUpdate.LastName;
            await _userManager.UpdateAsync(user);

            return Ok("Đã cập nhật người dùng");
        }
    }
}
