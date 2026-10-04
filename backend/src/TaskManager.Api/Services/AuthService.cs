using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Options;
using TaskManager.Api.Common;
using TaskManager.Api.Contracts.Auth;
using TaskManager.Api.Options;

namespace TaskManager.Api.Services;

public class AuthService(
    IOptions<DemoUserOptions> demoUser,
    ITokenService tokens,
    IValidator<LoginRequest> validator) : IAuthService
{
    private readonly DemoUserOptions _user = demoUser.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        // Same error for "unknown email" and "wrong password" so the API does not reveal which was wrong.
        var emailOk = string.Equals(request.Email!.Trim(), _user.Email, StringComparison.OrdinalIgnoreCase);
        var passwordOk = ConstantTimeEquals(request.Password!, _user.Password);

        if (!emailOk || !passwordOk)
            throw new InvalidCredentialsException();

        return tokens.CreateToken(_user.Email);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        // Hash first so inputs of different length still compare in constant time.
        var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }
}
