using FluentValidation;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class PaymentValidator : AbstractValidator<PaymentModel>
{
    public PaymentValidator()
    {
        RuleFor(x => x)
            .Custom((model, validator) =>
            {
                if (model.Paid == null && model.Discount == null)
                    validator.AddFailure("Paid amount & Discount cannot be both null");
                if (model.Paid == 0 && model.Discount == 0)
                    validator.AddFailure("Paid amount & Discount cannot be both 0");
                if (model.Paid < 0 || model.Discount < 0)
                    validator.AddFailure("Paid amount & Discount cannot be negative");
            });
    }
}