using SAPS.DDMS.API.Models;


namespace SAPS.DDMS.API.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification> CreateAsync(Notification notification);
        Task<List<Notification>> GetByUserIdAsync(int userId);
        Task<Notification> GetByIdAsync(int id);
        Task MarkAsReadAsync(int id);
    }
}