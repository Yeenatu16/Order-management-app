using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OrderManagementApp.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        var senderName = _config["EmailSettings:SenderName"] ?? "Order Management System";
        var senderEmail = _config["EmailSettings:SenderEmail"] ?? throw new InvalidOperationException("Sender email is missing.");
        var host = _config["EmailSettings:Server"] ?? "smtp.gmail.com";
        var port = int.TryParse(_config["EmailSettings:Port"], out var p) ? p : 587;
        var password = _config["EmailSettings:Password"] ?? throw new InvalidOperationException("SMTP password is missing.");

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(senderName, senderEmail));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
        email.Body = bodyBuilder.ToMessageBody();

        using var smtp = new SmtpClient();
        smtp.CheckCertificateRevocation = false;
        // Connect via STARTTLS on port 587
        await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(senderEmail, password);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
        
        _logger.LogInformation("Verification email successfully dispatched to {Email}", toEmail);
    }
}