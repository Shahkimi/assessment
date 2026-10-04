using TaskManager.Api.Contracts.Auth;

namespace TaskManager.Api.Services;

public interface ITokenService
{
    LoginResponse CreateToken(string email);
}
