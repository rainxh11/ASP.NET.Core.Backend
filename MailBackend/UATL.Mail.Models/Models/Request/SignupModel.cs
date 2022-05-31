namespace UATL.MailSystem.Common.Request;

public class SignupModel
{
    public string Name { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string ConfirmPassword { get; set; }
#nullable enable
    public string? Description { get; set; }
#nullable disable
}