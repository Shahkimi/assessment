using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Options;

/// <summary>
/// The assessment allows a single hardcoded user. It is still read from configuration
/// (DemoUser__Email / DemoUser__Password) so credentials are not baked into source or images.
/// </summary>
public class DemoUserOptions
{
    public const string SectionName = "DemoUser";

    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
