using MareLib;
using Newtonsoft.Json;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client;

namespace Guilds;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class RoleChangePacket
{
    public string? guildName;
    public int roleId;
    public string? newName;
    public int newAuthority;
    public GuildPerms newPerms;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public enum EnumGuildPacket
{
    Invite,
    CancelInvite,
    AcceptInvite,
    Kick,
    Promote,
    AddRole,
    RemoveRole,
    Create,
    Leave,
    Disband
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildRequestPacket
{
    public EnumGuildPacket type;
    public string? targetPlayer;
    public int targetRoleId;
    public string? guildName;
    public byte[]? data;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullGuildPacket
{
    public Dictionary<string, Guild> guilds = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullMembersPacket
{
    public Dictionary<string, HashSet<string>> members = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullInvitesPacket
{
    public Dictionary<string, HashSet<string>> pendingInvites = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullMetricsPacket
{
    public Dictionary<string, PlayerMetrics> metrics = new();
}

/// <summary>
/// On the server, manages all guilds and saves/loads.
/// On client, holds requested guild info.
/// </summary>
[GameSystem]
public class GuildManager : NetworkedGameSystem
{
    public GuildData guildData = new();
    public GuildGui? guildGui;
    public readonly object guildLock = new();

    public GuildManager(bool isServer, ICoreAPI api) : base(isServer, api, "guilds")
    {

    }

    public override void OnStart()
    {
        if (isServer)
        {
            LoadSaveData();
            MainAPI.Sapi.Event.GameWorldSave += OnSave;

            MainAPI.Sapi.Event.PlayerJoin += player =>
            {
                PlayerMetrics metrics = guildData.GetMetrics(player);
                metrics.UpdateMetrics(player);
                metrics.isOnline = true;

                SendPacket(guildData, player); // Sync full data.

                guildData.SyncMetrics(this);
            };

            MainAPI.Sapi.Event.PlayerLeave += player =>
            {
                PlayerMetrics metrics = guildData.GetMetrics(player);
                metrics.UpdateMetrics(player);
                metrics.isOnline = false;

                guildData.SyncMetrics(this);
            };
        }
        else
        {
            ScreenManager.hotkeyManager.RegisterHotKey("guild", "Guild Window", (int)GlKeys.V, triggerOnUpAlso: false);
            MainAPI.Capi.Input.SetHotKeyHandler("guild", key =>
            {
                guildGui ??= new GuildGui();
                guildGui.Toggle();

                return true;
            });
        }
    }

    public override void OnClose()
    {
        if (!isServer)
        {
            GuiThemes.ClearCache();
        }
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.RegisterMessageType<GuildRequestPacket>();
        channel.RegisterMessageType<GuildData>();
        channel.RegisterMessageType<Guild>();
        channel.RegisterMessageType<FullGuildPacket>();
        channel.RegisterMessageType<FullMembersPacket>();
        channel.RegisterMessageType<FullInvitesPacket>();
        channel.RegisterMessageType<FullMetricsPacket>();
        channel.RegisterMessageType<RoleChangePacket>();

        channel.SetMessageHandler<GuildRequestPacket>(HandleRequestFromServer);
        channel.SetMessageHandler<GuildData>(HandleFullDataSync);

        channel.SetMessageHandler<Guild>(p =>
        {
            guildData.GuildReceived(p);
        });

        channel.SetMessageHandler<FullGuildPacket>(p =>
        {
            guildData.GuildsReceived(p);
        });

        channel.SetMessageHandler<FullMembersPacket>(p =>
        {
            guildData.MembersReceived(p);
        });

        channel.SetMessageHandler<FullInvitesPacket>(p =>
        {
            guildData.InvitesReceived(p);
        });

        channel.SetMessageHandler<FullMetricsPacket>(p =>
        {
            guildData.MetricsReceived(p);
        });
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {
        channel.RegisterMessageType<GuildRequestPacket>();
        channel.RegisterMessageType<GuildData>();
        channel.RegisterMessageType<Guild>();
        channel.RegisterMessageType<FullGuildPacket>();
        channel.RegisterMessageType<FullMembersPacket>();
        channel.RegisterMessageType<FullInvitesPacket>();
        channel.RegisterMessageType<FullMetricsPacket>();
        channel.RegisterMessageType<RoleChangePacket>();

        channel.SetMessageHandler<GuildRequestPacket>(HandleRequestFromClient);

        channel.SetMessageHandler<RoleChangePacket>((player, p) =>
        {
            if (guildData.UpdateRole(player, p) == GuildStatus.Success)
            {
                guildData.SyncGuild(this, p.guildName!);
            }
        });
    }

    #region Client

    public void HandleFullDataSync(GuildData data)
    {
        guildData = data;
        if (guildGui?.IsOpened() == true)
        {
            guildGui?.SetWidgets();
        }
    }

    /// <summary>
    /// Handle an invite from the server.
    /// </summary>
    public void HandleRequestFromServer(GuildRequestPacket packet)
    {
        if (packet.guildName == null) return;

        if (packet.type is EnumGuildPacket.Invite or EnumGuildPacket.CancelInvite)
        {
            IPlayer clientPlayer = MainAPI.Capi.World.Player;

            if (packet.type == EnumGuildPacket.Invite)
            {
                if (guildData.AddClientInvite(clientPlayer.PlayerUID, packet.guildName))
                {
                    guildGui?.RefreshPage();
                }
            }
            else if (packet.type == EnumGuildPacket.CancelInvite)
            {
                if (guildData.RemoveClientInvite(clientPlayer.PlayerUID, packet.guildName))
                {
                    guildGui?.RefreshPage();
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// Sync the full data to a player.
    /// </summary>
    public void SyncFullData(IServerPlayer player)
    {
        SendPacket(guildData, player);
    }

    public void HandleRequestFromClient(IServerPlayer player, GuildRequestPacket packet)
    {
        string playerUid = player.PlayerUID;
        if (packet.guildName == null) return;

        // Create.
        if (packet.type == EnumGuildPacket.Create)
        {
            if (guildData.CreateGuild(packet.guildName, player) == GuildStatus.Success)
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.AcceptInvite)
        {
            if (guildData.AcceptInvite(playerUid, packet.guildName) is GuildStatus.Success)
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            GuildRequestPacket cancelInvitePacket = new()
            {
                type = EnumGuildPacket.CancelInvite,
                guildName = packet.guildName
            };

            SendPacket(cancelInvitePacket, player);

            return;
        }

        if (packet.type == EnumGuildPacket.CancelInvite && packet.targetPlayer == player.PlayerUID)
        {
            if (guildData.RemoveInvite(playerUid, packet.guildName, playerUid) == GuildStatus.Success)
            {
                guildData.SyncMembers(this);
                guildData.SyncGuild(this, packet.guildName);
            }

            GuildRequestPacket cancelInvitePacket = new()
            {
                type = EnumGuildPacket.CancelInvite,
                guildName = packet.guildName
            };

            SendPacket(cancelInvitePacket, player);
        }

        Guild? guild = guildData.GetGuild(packet.guildName);
        if (guild == null) return;

        RoleInfo? role = guild.GetRole(playerUid);
        if (role == null) return;

        if (packet.type == EnumGuildPacket.AddRole)
        {
            if (guildData.AddRole(player, packet.guildName) == GuildStatus.Success)
            {
                guildData.SyncGuild(this, packet.guildName);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.RemoveRole)
        {
            if (guildData.RemoveRole(player, packet.guildName, packet.targetRoleId) == GuildStatus.Success)
            {
                guildData.SyncGuild(this, packet.guildName);
            }

            return;
        }

        // Disband.
        if (packet.type == EnumGuildPacket.Disband)
        {
            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || roleInfo.id != 1) return; // Not founder.

            if (guildData.DisbandGuild(packet.guildName) == GuildStatus.Success)
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        // Leave.
        if (packet.type == EnumGuildPacket.Leave)
        {
            if (guildData.RemovePlayerFromGuild(playerUid, packet.guildName) == GuildStatus.Success)
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        if (packet.targetPlayer == null) return;
        if (!guildData.IsValidUid(packet.targetPlayer)) return;

        if (packet.type is EnumGuildPacket.Invite)
        {
            if (guildData.AddInvite(playerUid, packet.guildName, packet.targetPlayer) == GuildStatus.Success)
            {
                guildData.SyncInvites(this);
                guildData.SyncGuild(this, packet.guildName);
            }

            return;
        }

        if (packet.type is EnumGuildPacket.CancelInvite)
        {
            if (guildData.RemoveInvite(playerUid, packet.guildName, packet.targetPlayer) == GuildStatus.Success)
            {
                guildData.SyncInvites(this);
                guildData.SyncGuild(this, packet.guildName);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.Kick)
        {
            if (guildData.KickPlayer(playerUid, packet.targetPlayer, packet.guildName) == GuildStatus.Success)
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.Promote)
        {
            if (packet.targetPlayer == null) return;

            if (guildData.ChangeRole(playerUid, packet.targetPlayer, packet.guildName, packet.targetRoleId) == GuildStatus.Success)
            {
                guildData.SyncGuilds(this);
            }

            return;
        }
    }

    public void LoadSaveData()
    {
        string guildFile = Path.Combine(GamePaths.DataPath, "guilds.json");
        if (File.Exists(guildFile))
        {
            try
            {
                string text = File.ReadAllText(guildFile);
                guildData = JsonConvert.DeserializeObject<GuildData>(text)!;
            }
            catch
            {
                guildData = new();
            }
        }

        guildData ??= new GuildData();

        guildData.VerifyDataIntegrity();

        //byte[]? data = ((ICoreServerAPI)api).WorldManager.SaveGame.GetData("guilds");

        //if (data != null)
        //{
        //    try
        //    {
        //        GuildData loadedData = SerializerUtil.Deserialize<GuildData>(data);
        //        guildData = loadedData;
        //    }
        //    catch
        //    {

        //    }
        //}
    }

    private void OnSave()
    {
        JsonSerializerSettings settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        guildData.VerifyDataIntegrity();

        string guildFile = Path.Combine(GamePaths.DataPath, "guilds.json");
        File.WriteAllText(guildFile, JsonConvert.SerializeObject(guildData, settings));

        //byte[] data = SerializerUtil.Serialize(guildData);
        //((ICoreServerAPI)api).WorldManager.SaveGame.StoreData("guilds", data);
    }
}