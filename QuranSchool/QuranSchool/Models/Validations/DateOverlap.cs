namespace QuranSchool.Models.Validations;

public class DateOverlap
{
    public static bool IsOverlapped((string ID, DateTime Start, DateTime End) range,
        params (string ID, DateTime Start, DateTime End)[] dates)
    {
        //bool overlap = tStartA < tEndB && tStartB < tEndA;
        return dates.Where(x => x.ID != range.ID).Any(x => range.Start <= x.End && x.Start <= range.End);
    }

    public static bool IsOverlapped(TimeOnly firstStart, TimeOnly firstEnd, TimeOnly secondStart, TimeOnly secondEnd)
    {
        return firstStart <= secondEnd && secondStart <= firstEnd;
    }

    public static bool IsOverlapped(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
    {
        return firstStart <= secondEnd && secondStart <= firstEnd;
    }
}