using FluentValidation;
using TaskManager.Api.Data.Configurations;
using TaskManager.Api.Domain;

namespace TaskManager.Api.Validation;

/// <summary>Shared rules so create and update can never drift apart.</summary>
internal static class TaskRequestRules
{
    public static IRuleBuilderOptions<T, string?> TitleRules<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Title is required.")
            .MaximumLength(TaskItemConfiguration.TitleMaxLength)
            .WithMessage($"Title must be at most {TaskItemConfiguration.TitleMaxLength} characters.");

    public static IRuleBuilderOptions<T, string?> DescriptionRules<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(TaskItemConfiguration.DescriptionMaxLength)
            .WithMessage($"Description must be at most {TaskItemConfiguration.DescriptionMaxLength} characters.");

    public static IRuleBuilderOptions<T, TaskPriority?> PriorityRules<T>(this IRuleBuilder<T, TaskPriority?> rule) =>
        rule.NotNull().WithMessage("Priority is required.")
            .IsInEnum().WithMessage("Priority must be Low, Medium or High.");
}
