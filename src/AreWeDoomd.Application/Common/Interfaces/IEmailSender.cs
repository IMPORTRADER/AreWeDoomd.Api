using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

