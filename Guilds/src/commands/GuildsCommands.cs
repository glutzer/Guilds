using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Guilds;

[GameSystem]
public class GuildsCommands : GameSystem
{
    private GuildManager guildManager = null!;

    public GuildsCommands(bool isServer, ICoreAPI api) : base(isServer, api)
    {
    }

    public override void PreInitialize()
    {
        guildManager = MainAPI.GetGameSystem<GuildManager>(api.Side);
    }

    public override void Initialize()
    {
        if (isServer)
        {
            CommandArgumentParsers parsers = MainAPI.Sapi.ChatCommands.Parsers;

            // Create main command. Requires a player to call it, ensuring the player is not null.
            IChatCommand command = MainAPI.Sapi.ChatCommands.Create("guilds");
            command
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer();

            IChatCommand adminCommand = MainAPI.Sapi.ChatCommands.Create("guildsadmin");
            adminCommand
                .RequiresPrivilege(Privilege.ban)
                .RequiresPlayer();

            adminCommand
                .BeginSubCommand("disband")
                .WithArgs(parsers.Word("guildName"))
                .HandleWith(c =>
                {
                    string guildName = (string)c[0];

                    Guild? guild = guildManager.guildData.GetGuildByName(guildName);

                    if (guild == null) return TextCommandResult.Error("Guild not found.");

                    GuildPacket requestPacket = GuildPacket.Create(EnumGuildPacket.Disband, null, guild.id, 0);
                    string founderUid = guild.MemberInfo.FirstOrDefault(x => x.roleId == 1)?.playerUid ?? "";
                    requestPacket.fromUid = founderUid;

                    guildManager.guildData.DisbandGuild(guild);
                    guildManager.BroadcastPacket(requestPacket);

                    return TextCommandResult.Success("Disbanded guild.");
                });
        }
    }
}