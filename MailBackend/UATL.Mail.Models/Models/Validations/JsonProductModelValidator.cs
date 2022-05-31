using FluentValidation;
using UATL.MailSystem.Common.Request;

namespace UATL.MailSystem.Common.Validations;

public class JsonProductModelValidator : AbstractValidator<JsonProductModel>
{
    public JsonProductModelValidator()
    {
        RuleFor(x => x.ProductName)
            .NotNull().WithMessage("Product Name Cannot be null!")
            .NotEmpty().WithMessage("Product Name cannot be empty!")
            .MinimumLength(2).WithMessage("Account Name Minimum length is 2 characters!");

        RuleFor(x => x.Barcode)
            .NotNull().WithMessage("Product Barcode Cannot be null!")
            .NotEmpty().WithMessage("Product Barcode cannot be empty!")
            .MinimumLength(3).WithMessage("Barcode minimum length is 3 digits!")
            .Matches(@"^[A-Z0-9]*$").WithMessage("Barcode can only consists of uppercase latin letters or digits!");
    }
}