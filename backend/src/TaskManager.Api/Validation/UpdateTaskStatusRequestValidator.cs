using FluentValidation;
using TaskManager.Api.Contracts.Tasks;

namespace TaskManager.Api.Validation;

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotNull().WithMessage("Status is required.")
            .IsInEnum().WithMessage("Status must be Todo or Done.");
    }
}
