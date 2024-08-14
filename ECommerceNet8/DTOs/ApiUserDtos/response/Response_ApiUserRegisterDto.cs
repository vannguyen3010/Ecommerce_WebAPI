using ECommerceNet8.Models.AuthModels;

namespace ECommerceNet8.DTOs.ApiUserDtos.response
{
    public class Response_ApiUserRegisterDto
    {
        public bool isSuccess { get; set; } 
        public ApiUser apiUser {  get; set; }
        public List<string> Message { get; set; } = new List<string>();
    }
}
