namespace QuranSchool.Models;

public class PasswordResetModel
{
    public string Token { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
}