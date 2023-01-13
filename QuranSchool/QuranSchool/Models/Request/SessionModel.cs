namespace QuranSchool.Models.Request;

public class SessionModel
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool OnHold { get; set; } = false;
}