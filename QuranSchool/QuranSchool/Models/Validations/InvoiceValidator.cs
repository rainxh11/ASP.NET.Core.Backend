using FluentValidation;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

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
                    if (model.Paid > formation.Price - model.Discount)
                        model.Paid = formation.Price - model.Discount;
                }
                catch
                {
                    validator.AddFailure(model.Formation.ID, "Error getting formation with this ID!");
                }
            });
    }
}