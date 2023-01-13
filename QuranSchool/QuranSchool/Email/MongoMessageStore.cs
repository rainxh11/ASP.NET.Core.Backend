using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using MongoDB.Entities;

namespace QuranSchool.Email;

public class MongoMessageStore : MessageStore
{
    private readonly ILogger<MongoMessageStore> _logger;

    public MongoMessageStore(ILogger<MongoMessageStore> logger)
    {
        _logger = logger;
    }

    public override async Task<SmtpResponse> SaveAsync(ISessionContext context,
        IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream();

            var position = buffer.GetPosition(0);
            var email = new Models.Email(transaction);

            while (buffer.TryGet(ref position, out var memory)) await stream.WriteAsync(memory, cancellationToken);

            await email.SaveAsync(cancellation: cancellationToken);

            stream.Position = 0;
            await email.Data.UploadAsync(stream, cancellation: cancellationToken);
            stream.Position = 0;
            var message = await MimeKit.MimeMessage.LoadAsync(stream, cancellationToken);
            _logger?.LogInformation("[EMAIL SERVER] Received E-Mail from: '{0}', Subject: {1}",
                string.Join(";", message.From.Select(x => x.Name)),
                message.Subject);

            return SmtpResponse.Ok;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "");
            return SmtpResponse.SyntaxError;
        }
    }
}