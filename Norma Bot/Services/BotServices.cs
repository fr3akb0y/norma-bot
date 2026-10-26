using Norma_Bot.Data;
using Norma_Bot.Services.Hoyoverse;

namespace Norma_Bot.Services;

public static class BotServices
{
    public static ReactionRoleService ReactionRoles { get; set; } = null!;
    public static HoyoverseRepository HoyoverseRepository { get; set; } = null!;
    public static HoyoverseNotificationService HoyoverseNotifications { get; set; } = null!;
}
