using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Application.Templates;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Fundoo.Notification.Application.Abstractions;

namespace Fundoo.Notification.Api.Services;

public sealed class SmtpEmailService(IConfiguration configuration) : INotificationService
{
    public async Task SendPasswordResetNotificationAsync(
        PasswordResetNotificationRequest request)
    {
        var body = PasswordResetEmailTemplate.Build(
            string.IsNullOrWhiteSpace(request.RecipientName) ? "User" : request.RecipientName,
            request.ResetToken);

        await SendEmailAsync(
            request.RecipientEmail,
            "Fundoo Notes - Password Reset",
            body);
    }

    public async Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request)
    {
        var body = WelcomeEmailTemplate.Build(
            request.RecipientName);

        await SendEmailAsync(
            request.RecipientEmail,
            "Welcome to Fundoo Notes!",
            body);
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string body)
    {
        var host = GetRequiredSetting("Host");

        if (!int.TryParse(
                GetRequiredSetting("Port"),
                out var port))
        {
            throw new InvalidOperationException(
                "EmailSettings:Port must be a valid number.");
        }

        var senderEmail = GetRequiredSetting("SenderEmail");
        var senderName = configuration["EmailSettings:SenderName"]
                         ?? "Fundoo Notes";

        var username = GetRequiredSetting("Username");
        var password = GetRequiredSetting("Password");

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(senderName, senderEmail));

        message.To.Add(MailboxAddress.Parse(recipientEmail));

        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = body
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            host,
            port,
            SecureSocketOptions.StartTls);

        try
        {
            await client.AuthenticateAsync(username, password);

            await client.SendAsync(message);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }

    private string GetRequiredSetting(string key)
    {
        return configuration[$"EmailSettings:{key}"]
               ?? throw new InvalidOperationException(
                   $"EmailSettings:{key} is missing.");
    }
}
