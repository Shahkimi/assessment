using TaskManager.Api.Contracts.Auth;

namespace TaskManager.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
