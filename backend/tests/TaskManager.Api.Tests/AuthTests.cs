using System.IdentityModel.Tokens.Jwt;
using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Common;
using TaskManager.Api.Contracts.Auth;
using TaskManager.Api.Options;
using TaskManager.Api.Services;
using TaskManager.Api.Validation;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace TaskManager.Api.Tests;

public class AuthTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Key = "unit-test-signing-key-unit-test-signing-key",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 30
    };

    private static readonly DemoUserOptions User = new() { Email = "admin@taskflow.com", Password = "Admin@123" };
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));

    private AuthService CreateService() => new(
        MsOptions.Create(User),
        new JwtTokenService(MsOptions.Create(Jwt), _clock),
        new LoginRequestValidator());

    [Fact]
    public async Task Login_with_correct_credentials_returns_token_with_expiry()
    {
        var response = await CreateService().LoginAsync(new LoginRequest("admin@taskflow.com", "Admin@123"));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.Equal("test-issuer", token.Issuer);
        Assert.Contains("test-audience", token.Audiences);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(30), response.ExpiresAtUtc);
    }

    [Fact]
    public async Task Login_email_is_case_insensitive_and_trimmed()
    {
        var response = await CreateService().LoginAsync(new LoginRequest("  ADMIN@TaskFlow.com ", "Admin@123"));
        Assert.False(string.IsNullOrEmpty(response.Token));
    }

    [Theory]
    [InlineData("admin@taskflow.com", "wrong")]
    [InlineData("admin@taskflow.com", "admin@123")]
    [InlineData("someone@else.com", "Admin@123")]
    public async Task Login_with_wrong_credentials_throws_invalid_credentials(string email, string password)
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            CreateService().LoginAsync(new LoginRequest(email, password)));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("not-an-email", "x")]
    [InlineData("admin@taskflow.com", "")]
    public async Task Login_with_invalid_input_throws_validation(string email, string password)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().LoginAsync(new LoginRequest(email, password)));
    }
}
