namespace QuranSchool.Models.Request;

public class GroupHoldModel
{
    public IEnumerable<string> Groups { get; set; }

    public DateTime? HoldDate { get; set; } = DateTime.Now;
}