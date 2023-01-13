using DevExpress.Utils;
using Hangfire;
using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Models;
using QuranSchool.Models.Request;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using Transaction = QuranSchool.Models.Transaction;

namespace QuranSchool.Services;

public class DatabaseChangeService : IHostedService
{
    private readonly Watcher<Account> _accountWatcher;
    private readonly SessionEndCollector collector;
    private readonly IRecurringJobManager recurringJobs;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly Watcher<Formation> _formationWatcher;
    private readonly AccountGenerator _generator;
    private readonly Watcher<Group> _groupWatcher;
    private readonly Watcher<Invoice> _invoiceWatcher;
    private readonly ILogger<DatabaseChangeService> _logger;
    private readonly Watcher<Student> _studentWatcher;
    private readonly Watcher<Teacher> _teacherWatcher;
    private readonly Watcher<Transaction> _transactionWatcher;
    private readonly WebsocketNotificationService _websocketNotificationService;

    private IDisposable? _accountSubscription;
    private IDisposable? _formationSubscription;
    private IDisposable? _invoiceSubscription;
    private IDisposable? _studentSubscription;
    private IDisposable? _transactionSubscription;


    public DatabaseChangeService(
        SessionEndCollector collector, 
        IServiceScopeFactory scopeFactory,
        IRecurringJobManager recurringJobs,
        IBackgroundJobClient bgJobs,
        WebsocketNotificationService nservice,
        ILogger<DatabaseChangeService> logger)
    {
        _accountWatcher = DB.Watcher<Account>("account");
        _studentWatcher = DB.Watcher<Student>("student");
        _formationWatcher = DB.Watcher<Formation>("formation");
        _invoiceWatcher = DB.Watcher<Invoice>("invoice");
        _transactionWatcher = DB.Watcher<Transaction>("transaction");
        _teacherWatcher = DB.Watcher<Teacher>("teacher");
        _groupWatcher = DB.Watcher<Group>("group");

        _logger = logger;
        _websocketNotificationService = nservice;
        this.collector = collector;
        this.recurringJobs = recurringJobs;
        _backgroundJobs = bgJobs;
        _generator = _generator = scopeFactory.CreateScope().ServiceProvider.GetService<AccountGenerator>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        var eventType = EventType.Created | EventType.Deleted | EventType.Updated;

        _accountWatcher.Start(eventType, cancellation: cancellationToken);
        _studentWatcher.Start(eventType, cancellation: cancellationToken);
        _invoiceWatcher.Start(eventType, cancellation: cancellationToken);
        _formationWatcher.Start(eventType, cancellation: cancellationToken);
        _transactionWatcher.Start(eventType, cancellation: cancellationToken);
        _groupWatcher.Start(eventType, cancellation: cancellationToken);
        _teacherWatcher.Start(eventType, cancellation: cancellationToken);

        StartObservables(cancellationToken);
    }


    public Task StopAsync(CancellationToken cancellationToken)
    {
        _accountSubscription?.Dispose();
        _formationSubscription?.Dispose();
        _invoiceSubscription?.Dispose();
        _studentSubscription?.Dispose();
        _transactionSubscription?.Dispose();

        return Task.CompletedTask;
    }

    public async void StartObservables(CancellationToken ct = default)
    {
        await CreateSessionEndNotifications(ct);

        var invoiceChanges = _invoiceWatcher.ToObservableChangeStream();

        _invoiceSubscription = invoiceChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_invoice")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var studentChanges = _studentWatcher.ToObservableChangeStream();

        _studentSubscription = studentChanges
            .Sample(TimeSpan.FromSeconds(1))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_student")))
            .Do(async x =>
            {
                if (x.OperationType == ChangeStreamOperationType.Insert)
                {
                    var account = new CreateAccountModel
                    {
                        Name = x.FullDocument.Name,
                        Description = $"Auto Generated Student Account for {x.FullDocument.Name}",
                        Role = AccountType.Student
                    };
                    await _generator.CreateAccount(x.FullDocument.ID, account);
                }
            })
            .Subscribe();

        //----------------------------------------------------------------------------------//

        var formationChanges = _formationWatcher.ToObservableChangeStream();

        _formationSubscription = formationChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_formation")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var accountChanges = _accountWatcher.ToObservableChangeStream();

        _accountSubscription = accountChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_account")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var transactionChanges = _transactionWatcher.ToObservableChangeStream();

        _transactionSubscription = transactionChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_transaction")))
            .Do(x => _backgroundJobs.Enqueue(() => FixPaidInvoices()))
            .Subscribe();
        //----------------------------------------------------------------------------------//
        var groupChanges = _groupWatcher.ToObservableChangeStream();

        groupChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_group")))
            .Subscribe();

        groupChanges
            .Sample(TimeSpan.FromSeconds(1))
            .Do(async x => await CreateSessionEndNotifications(ct))
            .Subscribe();

        //----------------------------------------------------------------------------------//
        var teacherChanges = _teacherWatcher.ToObservableChangeStream();

        teacherChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _websocketNotificationService.SendAll("refresh_teacher")))
            .Do(async x =>
            {
                if (x.OperationType == ChangeStreamOperationType.Insert)
                {
                    var account = new CreateAccountModel
                    {
                        Name = x.FullDocument.Name,
                        Description = $"Auto Generated Teacher Account for {x.FullDocument.Name}",
                        Role = AccountType.Teacher
                    };
                    await _generator.CreateAccount(x.FullDocument.ID, account);
                }
            })
            .Subscribe();
    }
    public async Task CreateSessionEndNotifications(CancellationToken ct = default)
    {
        try
        {
            var times = await collector.GetAvaillableSessionsEndTimes(ct);

            var cron = times.ToCronExpression();

            recurringJobs.AddOrUpdate("EndingSession",
                () => _websocketNotificationService.SendAll("refresh_group"),
                cron);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex.Message);
        }    
    }

    public static async Task FixPaidInvoices()
    {
        try
        {
            var invoices = await DB.Find<Invoice>()
                .Match(x => x.Type == InvoiceType.Debt)
                .ExecuteAsync();

            await invoices
                .Where(x => x.LeftUnpaid <= 0)
                .Select(x =>
                {
                    x.Type = InvoiceType.Paid;
                    return x;
                }).SaveAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }
}