namespace TaskManager.Api.Contracts.Auth;

public record LoginResponse(string Token, DateTime ExpiresAtUtc);
