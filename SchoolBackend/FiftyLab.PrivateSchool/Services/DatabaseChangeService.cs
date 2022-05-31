using System.Diagnostics;
using Hangfire;
using MongoDB.Driver;
using MongoDB.Entities;
using System.Reactive.Linq;

namespace FiftyLab.PrivateSchool.Services;

public class DatabaseChangeService : IHostedService
{
    private readonly ILogger<DatabaseChangeService> _logger;

    private IDisposable? _accountSubscription;
    private IDisposable? _formationSubscription;
    private IDisposable? _studentSubscription;
    private IDisposable? _invoiceSubscription;
    private IDisposable? _transactionSubscription;

    private readonly Watcher<Account> _accountWatcher;
    private readonly Watcher<Formation> _formationWatcher;
    private readonly Watcher<Student> _studentWatcher;
    private readonly Watcher<Invoice> _invoiceWatcher;
    private readonly Watcher<Transaction> _transactionWatcher;

    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly NotificationService _notificationService;


    public DatabaseChangeService(
        IBackgroundJobClient bgJobs,
        NotificationService nservice,
        ILogger<DatabaseChangeService> logger)
    {
        _accountWatcher = DB.Watcher<Account>("account");
        _studentWatcher = DB.Watcher<Student>("student");
        _formationWatcher = DB.Watcher<Formation>("formation");
        _invoiceWatcher = DB.Watcher<Invoice>("invoice");
        _transactionWatcher = DB.Watcher<Transaction>("transaction");

        _logger = logger;
        _notificationService = nservice;
        _backgroundJobs = bgJobs;
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

    public void StartObservables(CancellationToken ct = default)
    {
        var invoiceChanges = Observable
            .FromEvent<IEnumerable<ChangeStreamDocument<Invoice>>>(
                x => _invoiceWatcher.OnChangesCSD += x,
                x => _invoiceWatcher.OnChangesCSD -= x)
            .SelectMany(x => x);

        _invoiceSubscription = invoiceChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _notificationService.SendAll("refresh_invoice")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var studentChanges = Observable
            .FromEvent<IEnumerable<ChangeStreamDocument<Student>>>(
                x => _studentWatcher.OnChangesCSD += x,
                x => _studentWatcher.OnChangesCSD -= x)
            .SelectMany(x => x);

        _studentSubscription = studentChanges
            .Sample(TimeSpan.FromSeconds(1))
            .Do(x => _backgroundJobs.Enqueue(() => _notificationService.SendAll("refresh_student")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var formationChanges = Observable
            .FromEvent<IEnumerable<ChangeStreamDocument<Formation>>>(
                x => _formationWatcher.OnChangesCSD += x,
                x => _formationWatcher.OnChangesCSD -= x)
            .SelectMany(x => x);

        _formationSubscription = formationChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _notificationService.SendAll("refresh_formation")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var accountChanges = Observable
            .FromEvent<IEnumerable<ChangeStreamDocument<Account>>>(
                x => _accountWatcher.OnChangesCSD += x,
                x => _accountWatcher.OnChangesCSD -= x)
            .SelectMany(x => x);

        _accountSubscription = accountChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _notificationService.SendAll("refresh_account")))
            .Subscribe();
        //----------------------------------------------------------------------------------//

        var transactionChanges = Observable
            .FromEvent<IEnumerable<ChangeStreamDocument<Transaction>>>(
                x => _transactionWatcher.OnChangesCSD += x,
                x => _transactionWatcher.OnChangesCSD -= x)
            .SelectMany(x => x);

        _transactionSubscription = transactionChanges
            .Sample(TimeSpan.FromSeconds(0.25))
            .Do(x => _backgroundJobs.Enqueue(() => _notificationService.SendAll("refresh_transaction")))
            .Do(x => _backgroundJobs.Enqueue(() => FixPaidInvoices()))
            .Subscribe();
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