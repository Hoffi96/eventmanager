namespace HelferApp.Services;

public interface IEmailService
{
    Task SendAsync(string toAddress, string subject, string body);
}
