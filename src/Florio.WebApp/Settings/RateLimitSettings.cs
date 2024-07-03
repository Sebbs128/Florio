using System.Threading.RateLimiting;

namespace Florio.WebApp.Settings;

public class RateLimitSettings
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
    public QueueProcessingOrder QueueProcessingOrder { get; set; }
    public int QueueLimit { get; set; }
}
