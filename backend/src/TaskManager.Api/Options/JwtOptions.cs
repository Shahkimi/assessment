using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC signing key. Comes from env (Jwt__Key); never committed.</summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters.")]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; init; } = 60;
}
