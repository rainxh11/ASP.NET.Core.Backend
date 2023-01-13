using SmtpServer;
using SmtpServer.Authentication;
using SmtpServer.Storage;

namespace QuranSchool.Email;

public static class EmailExtension
{
    public static IServiceCollection AddSmtpServer(this IServiceCollection services,
        Func<SmtpServerOptionsBuilder, ISmtpServerOptions> options)
    {
        return services
            .AddSingleton<IMessageStore, MongoMessageStore>()
            .AddSingleton<MessageStore, MongoMessageStore>()
            .AddSingleton<IUserAuthenticator, EmailUserAuthenticator>()
            .AddSingleton<IUserAuthenticatorFactory, EmailUserAuthenticatorFactory>()
            .AddSingleton<IMailboxFilter, EmailBoxFilter>()
            .AddSingleton<IMailboxFilterFactory, EmailBoxFilterFactory>()
            .AddSingleton(provider => new SmtpServer.SmtpServer(options(new SmtpServerOptionsBuilder()), provider))
            .AddHostedService<SmtpServerBackgroundService>();
    }

    public static IServiceCollection AddEmailSender(this IServiceCollection services)
    {
        return services
            .AddScoped<EmailSender>();
    }
}