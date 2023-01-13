namespace QuranSchool.Models;

public class Notification : ClientEntity
{
    public Notification(NotificationType type, string body)
    {
    }

    public NotificationType Type { get; set; }
    public string Body { get; set; }
}