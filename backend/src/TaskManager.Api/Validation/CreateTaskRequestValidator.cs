using FluentValidation;
using TaskManager.Api.Contracts.Tasks;

namespace TaskManager.Api.Validation;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).TitleRules();
        RuleFor(x => x.Description).DescriptionRules();
        RuleFor(x => x.Priority).PriorityRules();
    }
}
