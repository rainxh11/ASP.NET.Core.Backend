using FluentValidation;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class GroupHoldValidator : AbstractValidator<GroupHoldModel>
{
    public GroupHoldValidator()
    {
        RuleFor(x => x.HoldDate)
            .GreaterThanOrEqualTo(DateTime.Today)
            .WithMessage("Hold date cannot be earlier than today!");


        RuleForEach(x => x.Groups)
            .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Group>()
                        .Match(f => f.Eq(x => x.ID, id) & f.Eq(x => x.Cancelled, false) & f.Ne(x => x.Finished, true))
                        .ExecuteAnyAsync()
                        .GetAwaiter().GetResult();
                    ;
                    return exist;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("Group doesn't exist / invalid / disabled / finished!");
    }
}