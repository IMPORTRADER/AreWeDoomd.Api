using System.Net;
using System.Net.Mail;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Email;

public sealed class SmtpEmailSender(
    IOptionsMonitor<SmtpOptions> optionsMonitor,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly IOptionsMonitor<SmtpOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<SmtpEmailSender> _logger = logger;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.IsConfigured())
        {
            _logger.LogInformation("SMTP configuration is missing. Pretending to send email to {Email}.", message.To);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            Credentials = new NetworkCredential(options.Username, options.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        using var mailMessage = new MailMessage(options.From, message.To, message.Subject, message.Body);

        await client.SendMailAsync(mailMessage);
    }
}

