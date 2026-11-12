using Fundoo.Notification.Application.DTO;

namespace Fundoo.Notification.Application.Abstractions
{
    public interface INotificationService
    {
        Task SendPasswordResetNotificationAsync(PasswordResetNotificationRequest request);

        Task SendWelcomeNotificationAsync(WelcomeNotificationRequest request);
    }
}
