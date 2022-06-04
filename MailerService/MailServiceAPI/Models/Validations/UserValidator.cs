using FluentValidation;

namespace ReniwnMailServiceApi.Models.Validations;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Provide a valid Email!");
    }
}