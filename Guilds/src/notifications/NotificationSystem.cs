using MareLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Guilds;

public class NotificationSystem : NetworkedGameSystem
{
    private NotificationHud? hud;

    public NotificationSystem(bool isServer, ICoreAPI api) : base(isServer, api, "gnotif")
    {
    }

    public override void PostInitialize()
    {
        if (!isServer)
        {
            //hud = new NotificationHud();
            //hud.TryOpen();
        }
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {

    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {

    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {

    }
}