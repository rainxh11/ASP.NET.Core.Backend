using Hangfire;
using Ng.Services;
using QuranSchool.Models;

namespace QuranSchool.Helpers;

public class LoginInfoSaver
{
    private readonly IUserAgentService _userAgentService;
    private readonly IBackgroundJobClient _bgJobs;

    public LoginInfoSaver(IUserAgentService userAgentService, IBackgroundJobClient bgJobs)
    {
        _userAgentService = userAgentService;
        _bgJobs = bgJobs;
    }

    public async Task AddLogin(HttpContext context, Account account)
    {
    }
}