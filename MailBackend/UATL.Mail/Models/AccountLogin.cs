using Ng.Services;
using UATL.MailSystem.Common;

namespace UATL.Mail.Models;

public class AccountLogin
{
    public AccountLogin()
    {
    }

    public AccountLogin(AccountBase account, UserAgent userAgent, HttpContext context)
    {
        Account = account;
        Date = DateTime.Now;
        Id = Guid.NewGuid().ToString();
        IsBrowser = userAgent.IsBrowser;
        IsRobot = userAgent.IsRobot;
        IsMobile = userAgent.IsMobile;
        Platform = userAgent.Platform;
        Browser = userAgent.Browser;
        BrowserVersion = userAgent.BrowserVersion;
        Mobile = userAgent.Mobile;
        Robot = userAgent.Robot;
        try
        {
            Header = context.Request.Headers["User-Agent"];
            ClientOrigin = context.Request.Headers["Host"];
        }
        catch
        {
        }
    }

    public string Id { get; set; }
    public DateTime Date { get; set; }
    public AccountBase Account { get; set; }

    public string ClientOrigin { get; set; }
    public string Header { get; set; }
    public bool IsBrowser { get; set; }

    public bool IsRobot { get; set; }

    public bool IsMobile { get; set; }

    public string Platform { get; set; }

    public string Browser { get; set; }

    public string BrowserVersion { get; set; }

    public string Mobile { get; set; }

    public string Robot { get; set; }
}