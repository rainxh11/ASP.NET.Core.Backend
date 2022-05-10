﻿using System.Reactive.Linq;
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

    public async Task AddLogin(HttpContext context, Account account)
    {
        try
        {
            var userAgentString = context.Request.Headers["User-Agent"].ToString();
            var userAgent = _userAgentService.Parse(userAgentString);
            var accountLogin = new AccountLogin(account.ToBaseAccount(), userAgent, context);

            await BlobCache.LocalMachine.InsertObject(accountLogin.Id, accountLogin,
                DateTimeOffset.Now.AddMonths(12));
        }
        catch (Exception ex)
        {
            var x = "";
        }
    }
}