using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Guilds;

[GameSystem]
public class GuildConfigSystem : NetworkedGameSystem
{
    public GuildConfigSystem(bool isServer, ICoreAPI api) : base(isServer, api, "guildconfigs")
    {
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {
        channel.RegisterMessageType<GuildsConfig>();
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.SetMessageHandler<GuildsConfig>(GuildsConfig.SetClientInstance);
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {

    }

    public void ConfigDirty()
    {
        BroadcastPacket(GuildsConfig.Instance);
    }

    public override void Initialize()
    {
        if (isServer)
        {
            MainAPI.Sapi.Event.SaveGameLoaded += () =>
            {
                GuildsConfig.LoadInstance();
            };

            MainAPI.Sapi.Event.GameWorldSave += () =>
            {
                GuildsConfig.SaveInstance();
            };

            MainAPI.Sapi.Event.PlayerJoin += p =>
            {
                SendPacket(GuildsConfig.Instance, p);
            };
        }
    }
}