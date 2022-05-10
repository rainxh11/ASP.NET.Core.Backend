using FiftyLab.PrivateSchool.Models.Request;
using FluentValidation;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Validations;

public class StudentValidator : AbstractValidator<StudentModel>
{
    public StudentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Student Name Cannot be empty!")
            .MinimumLength(5).WithMessage("Student Name Minimum length is 5 characters!")
            .MaximumLength(100).WithMessage("Maximum length of Student Name is 100 characters")
            .Matches(@"^[a-zA-Z\s]*$").WithMessage("Special characters & digits are not allowed in Account Name!");

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Now.AddYears(-1))
            .WithMessage("Student Age must be more than 1 years")
            .GreaterThan(DateTime.Now.AddYears(-100))
            .WithMessage("Student Age must be less than 100 years");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone Number cannot be empty")
            .Matches(@"^0(\d| ){8,20}").WithMessage("Not a valid phone number");


    }
}

public class FormationValidator : AbstractValidator<FormationModel>
{
    public FormationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Formation name cannot be empty!")
            .MinimumLength(3).WithMessage("Formation name length is 3 characters minimum!")
            .MaximumLength(100).WithMessage("Formation name length is 100 characters maximum!");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Formation price cannot be negative")
            .NotEmpty().NotNull();

        RuleFor(x => x.DurationDays)
            .GreaterThan(0).WithMessage("Formation days cannot be 0")
            .NotEmpty().NotNull();
    }
}

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
public class InvoiceValidator : AbstractValidator<InvoiceModel>
{
    public InvoiceValidator()
    {
        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(DateTime.Now.AddMonths(-1))
            .WithMessage("Formation Starting date can only be a month prior today!")
            .LessThanOrEqualTo(DateTime.Now.AddMonths(3))
            .WithMessage("Formation starting date maximum date is 3 months from today!")
            .NotEmpty().NotNull();

        RuleFor(x => x.Student.ID)
            .NotNull().NotEmpty()
            .MustAsync(async (id, ct) =>
            {
                try
                {
                    var exist = await DB.Find<Student>().MatchID(id).ExecuteAnyAsync(ct);
                    return exist;
                }
                catch (Exception ex)
                {
                    return false;
                }

            }).WithMessage("Student with this ID doesn't exist!");

        RuleFor(x => x.Formation.ID)
            .NotNull().NotEmpty()
            .MustAsync(async (id, ct) =>
            {
                try
                {
                    var exist = await DB.Find<Formation>().MatchID(id).ExecuteAnyAsync(ct);
                    return exist;
                }
                catch (Exception ex)
                {
                    return false;
                }

            }).WithMessage("Formation with this ID doesn't exist!");

        RuleFor(x => x.Paid)
            .GreaterThanOrEqualTo(0).WithMessage("Payment cannot be negative");

        RuleFor(x => x.Discount)
            .GreaterThanOrEqualTo(0).WithMessage("Discount cannot be negative");

        RuleFor(x => x)

            .CustomAsync(async (model, validator, ct) =>
            {
                try
                {
                    var formation = await DB.Find<Formation>().MatchID(model.Formation.ID).ExecuteSingleAsync(ct);
                    if (model.Discount > formation.Price)
                        model.Discount = formation.Price;
                    if (model.Paid > (formation.Price - model.Discount))
                        model.Paid = formation.Price - model.Discount;
                }
                catch
                {
                    validator.AddFailure(model.Formation.ID, "Error getting formation with this ID!");
                }
            });
    }
}