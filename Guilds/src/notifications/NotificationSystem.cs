using MareLib;
using OpenTK.Mathematics;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Guilds;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class NotificationPacket
{
    public string? text;
    public Vector3 color;

    public NotificationPacket(string text, Vector3 color)
    {
        this.text = text;
        this.color = color;
    }
}

[GameSystem]
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
            hud = new NotificationHud();
            hud.TryOpen();
        }
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {
        channel.RegisterMessageType<NotificationPacket>();
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.SetMessageHandler<NotificationPacket>(OnNotificationPacket);
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {

    }

    /// <summary>
    /// Add a notification on the client.
    /// </summary>
    public static void AddNotification(string text, Vector3 color = default)
    {
        if (color == default)
        {
            color = Vector3.One;
        }

        MainAPI.GetGameSystem<NotificationSystem>(EnumAppSide.Client).hud?.AddNotification(text, color);
    }

    /// <summary>
    /// Give a notification to a player.
    /// </summary>
    public static void SendNotification(string text, IServerPlayer player, Vector3 color = default)
    {
        if (color == default)
        {
            color = Vector3.One;
        }

        MainAPI.GetGameSystem<NotificationSystem>(EnumAppSide.Server).SendPacket(new NotificationPacket(text, color), player);
    }

    /// <summary>
    /// Give a notification to a player on the server.
    /// </summary>
    public static void SendNotification(string text, string playerUid, Vector3 color = default)
    {
        if (MainAPI.Sapi.World.PlayerByUid(playerUid) is not IServerPlayer player) return;

        if (color == default)
        {
            color = Vector3.One;
        }

        MainAPI.GetGameSystem<NotificationSystem>(EnumAppSide.Server).SendPacket(new NotificationPacket(text, color), player);
    }

    /// <summary>
    /// Give a notification to everyone.
    /// </summary>
    public static void BroadcastNotification(string text, Vector3 color = default)
    {
        if (color == default)
        {
            color = Vector3.One;
        }

        MainAPI.GetGameSystem<NotificationSystem>(EnumAppSide.Server).BroadcastPacket(new NotificationPacket(text, color));
    }

    /// <summary>
    /// Send a packet to every member in a guild.
    /// </summary>
    public static void SendGuildNotification(string text, Guild guild, Vector3 color = default)
    {
        if (color == default)
        {
            color = Vector3.One;
        }

        NotificationPacket packet = new(text, color);

        foreach (MembershipInfo info in guild.MemberInfo)
        {
            IPlayer? player = MainAPI.Sapi.World.PlayerByUid(info.playerUid);
            if (player == null) continue;

            MainAPI.GetGameSystem<NotificationSystem>(EnumAppSide.Server).SendPacket(packet, (IServerPlayer)player);
        }
    }

    private void OnNotificationPacket(NotificationPacket packet)
    {
        if (packet.text == null) return;
        hud?.AddNotification(packet.text, packet.color);
    }
}