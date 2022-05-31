using System.Reactive.Linq;
using Akavache;
using FiftyLab.PrivateSchool;
using FiftyLab.PrivateSchool.Models;
using Hangfire;
using Ng.Services;

namespace FiftyLab.PrivateSchool.Helpers;

public class LoginInfoSaver
{
    private IBackgroundJobClient _bgJobs;
    private readonly IUserAgentService _userAgentService;

    public LoginInfoSaver(IUserAgentService userAgentService, IBackgroundJobClient bgJobs)
    {
        _userAgentService = userAgentService;
        _bgJobs = bgJobs;
    }

    public Task AddLogin(HttpContext context, Account account)
    {
        return Task.CompletedTask;
    }
}