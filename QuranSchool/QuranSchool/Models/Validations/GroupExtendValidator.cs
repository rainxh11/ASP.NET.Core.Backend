using DevExpress.CodeParser;
using FluentValidation;
using Jetsons.JetPack;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class GroupExtendValidator : AbstractValidator<GroupExtendModel>
{
    public GroupExtendValidator()
    {
        RuleFor(x => x)
            .Must(x => !(x.For is null && x.Until is null))
            .WithMessage(
                "Until & For (Days) cannot be both null, specify a period or a date for the group extension!");

        RuleFor(x => x.For)
            .Must(x => x is null or > 0)
            .WithMessage("For (Days) must be over 0");

        RuleFor(x => x.Until)
            .Must(x => x is null || x.ToDateTime().Date > DateTime.Today)
            .WithMessage("Until date cannot be prior to today")
            .Must((model, until) =>
            {
                try
                {
                    var group = DB.Find<Group>()
                        .MatchID(model.Id)
                        .ExecuteSingleAsync()
                        .RunSync();
                    return group?.ExpireOn < until?.Date || until is null;
                }
                catch
                {
                    return false;
                }
            });
    }
}