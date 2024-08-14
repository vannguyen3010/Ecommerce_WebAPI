using System.ComponentModel.DataAnnotations;

namespace ECommerceNet8.DTOs.ApiUserDtos.request
{
    public class Request_ApiUserRegisterDto
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
        [Required]
        [StringLength(15,
            ErrorMessage = "Mật khẩu của bạn phải có từ 8 đến 15 ký tự",
            MinimumLength = 8)]
        public string? Password { get; set; }
        [Required]
        public string? FirstName { get; set; }
        [Required]
        public string? LastName { get; set; }
    }
}
