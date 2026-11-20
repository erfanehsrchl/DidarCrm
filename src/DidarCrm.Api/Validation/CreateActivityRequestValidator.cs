using DidarCrm.Api.Contracts.Activities;
using DidarCrm.Domain.Activities;
using FluentValidation;

namespace DidarCrm.Api.Validation;

public sealed class CreateActivityRequestValidator
    : AbstractValidator<CreateActivityRequest>
{
    public CreateActivityRequestValidator()
    {
        RuleFor(request => request.ContactId)
            .NotEmpty()
            .WithMessage("شناسه مخاطب الزامی است.");

        RuleFor(request => request.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("عنوان فعالیت الزامی است.")
            .MaximumLength(ActivityTitle.MaxLength)
            .WithMessage(
                $"عنوان فعالیت نباید بیشتر از {ActivityTitle.MaxLength} کاراکتر باشد.");
    }
}
