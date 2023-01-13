using FluentValidation;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class PostModelValidation : AbstractValidator<PostModel>
{
    public PostModelValidation()
    {
        RuleFor(x => x.Body)
            .NotNull()
            .NotEmpty()
            .WithMessage("Post Body cannot be null or empty")
            .MaximumLength(5000)
            .WithMessage("Body Maximum length is 5000");
    }
}