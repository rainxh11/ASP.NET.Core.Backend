namespace UATL.MailSystem.Common.Request;

public class LoginModel
{
    public LoginModel()
    {
    }

    public LoginModel(string username, string password)
    {
        UserName = username;
        Password = password;
    }

    public string UserName { get; set; }
    public string Password { get; set; }
}