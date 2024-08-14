using ECommerceNet8.DTOs.ApiUserDtos.request;
using ECommerceNet8.DTOs.ApiUserDtos.response;

namespace ECommerceNet8.Repositories.AuthRepository
{
    public interface IAuthRepository
    {
        Task<Response_ApiUserRegisterDto> Register(Request_ApiUserRegisterDto userDto);
        Task<Response_ApiUserRegisterDto> RegisterAdmin(Request_ApiUserRegisterDto userDto, int secretKey);
        Task<Response_LoginDto> Login(Request_LoginDto login);
        Task<Response_LoginDto> VerifyAndGenerateTokens(Request_TokenDto tokenDto);
        Task<bool> LogoutDeleteRefreshToken(string userId);
    }
}
