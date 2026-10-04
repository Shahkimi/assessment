using FluentValidation;
using TaskManager.Api.Contracts.Tasks;

namespace TaskManager.Api.Validation;

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).TitleRules();
        RuleFor(x => x.Description).DescriptionRules();
        RuleFor(x => x.Priority).PriorityRules();
    }
}
