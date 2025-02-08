using MareLib;
using Newtonsoft.Json;
using OpenTK.Mathematics;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client;

namespace Guilds;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class RoleChangePacket
{
    public int guildId;
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
    Disband,
    RepGuild
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildRequestPacket
{
    public EnumGuildPacket type;
    public string? targetPlayer;
    public int roleId;
    public int guildId;
    public byte[]? data;

    public T? ReadData<T>()
    {
        if (data == null) return default;
        return SerializerUtil.Deserialize<T>(data);
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullGuildPacket
{
    public Dictionary<int, Guild> guilds = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullMembersPacket
{
    public Dictionary<string, HashSet<int>> members = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullInvitesPacket
{
    public Dictionary<string, HashSet<int>> pendingInvites = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class FullMetricsPacket
{
    public Dictionary<string, PlayerMetrics> metrics = new();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildInfoPacket
{
    public int guildId;
    public string? name;
    public Vector3 color;
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
        channel.RegisterMessageType<GuildInfoPacket>();

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
        channel.RegisterMessageType<GuildInfoPacket>();

        channel.SetMessageHandler<GuildRequestPacket>(HandleRequestFromClient);

        channel.SetMessageHandler<RoleChangePacket>((player, p) =>
        {
            if (guildData.UpdateRole(player, p))
            {
                Guild? guild = guildData.GetGuild(p.guildId);
                if (guild != null) GuildData.SyncGuild(this, guild);
            }
        });

        channel.SetMessageHandler<GuildInfoPacket>((player, p) =>
        {
            if (p.name == null) return;

            Guild? guild = guildData.GetGuild(p.guildId);
            if (guild == null) return;

            RoleInfo? roleInfo = guild.GetRole(player.PlayerUID);
            if (roleInfo == null || !roleInfo.HasPermissions(GuildPerms.ManageGuildInfo)) return;

            guild.ChangeName(p.name);
            guild.SetColor(p.color);

            guildData.SyncGuilds(this);
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
        if (packet.type is EnumGuildPacket.Invite or EnumGuildPacket.CancelInvite)
        {
            IPlayer clientPlayer = MainAPI.Capi.World.Player;

            if (packet.type == EnumGuildPacket.Invite)
            {
                if (guildData.AddClientInvite(clientPlayer.PlayerUID, packet.guildId))
                {
                    guildGui?.RefreshPage();
                }
            }
            else if (packet.type == EnumGuildPacket.CancelInvite)
            {
                if (guildData.RemoveClientInvite(clientPlayer.PlayerUID, packet.guildId))
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

        // Create.
        if (packet.type == EnumGuildPacket.Create)
        {
            string? name = packet.ReadData<string>();
            if (name == null) return;

            if (guildData.CreateGuild(name, player))
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        Guild? guild = guildData.GetGuild(packet.guildId);

        if (packet.type == EnumGuildPacket.RepGuild)
        {
            PlayerMetrics metrics = guildData.GetMetrics(player);

            if (guild == null)
            {
                metrics.reppedGuildId = -1;
                guildData.SyncMetrics(this);
            }
            else if (guild.HasMember(playerUid))
            {
                metrics.reppedGuildId = packet.guildId;
                guildData.SyncMetrics(this);
            }

            return;
        }

        if (guild == null) return;

        if (packet.type == EnumGuildPacket.AcceptInvite)
        {
            if (guildData.AcceptInvite(playerUid, guild))
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            GuildRequestPacket cancelInvitePacket = new()
            {
                type = EnumGuildPacket.CancelInvite,
                guildId = packet.guildId
            };

            SendPacket(cancelInvitePacket, player);

            return;
        }

        if (packet.type == EnumGuildPacket.CancelInvite && packet.targetPlayer == player.PlayerUID)
        {
            if (guildData.RemoveInvite(playerUid, guild, playerUid))
            {
                guildData.SyncMembers(this);
                GuildData.SyncGuild(this, guild);
            }

            GuildRequestPacket cancelInvitePacket = new()
            {
                type = EnumGuildPacket.CancelInvite,
                guildId = packet.guildId
            };

            SendPacket(cancelInvitePacket, player);
        }

        if (packet.type == EnumGuildPacket.AddRole)
        {
            if (GuildData.AddRole(player, guild))
            {
                GuildData.SyncGuild(this, guild);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.RemoveRole)
        {
            if (GuildData.RemoveRole(player, guild, packet.roleId))
            {
                GuildData.SyncGuild(this, guild);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.Disband)
        {
            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || roleInfo.id != 1) return; // Not founder.

            if (guildData.DisbandGuild(guild))
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
                MainAPI.GetGameSystem<ClaimManager>(EnumAppSide.Server).OnGuildDisbanded(packet.guildId);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.Leave)
        {
            if (guildData.RemovePlayerFromGuild(playerUid, guild))
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        // All packets from here target another player.
        if (packet.targetPlayer == null) return;
        if (!guildData.IsValidUid(packet.targetPlayer)) return;

        if (packet.type is EnumGuildPacket.Invite)
        {
            if (guildData.AddInvite(playerUid, guild, packet.targetPlayer))
            {
                guildData.SyncInvites(this);
                GuildData.SyncGuild(this, guild);
            }

            return;
        }

        if (packet.type is EnumGuildPacket.CancelInvite)
        {
            if (guildData.RemoveInvite(playerUid, guild, packet.targetPlayer))
            {
                guildData.SyncInvites(this);
                GuildData.SyncGuild(this, guild);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.Kick)
        {
            if (guildData.KickPlayer(playerUid, packet.targetPlayer, guild))
            {
                guildData.SyncMembers(this);
                guildData.SyncGuilds(this);
            }

            return;
        }

        if (packet.type == EnumGuildPacket.Promote)
        {
            if (packet.targetPlayer == null) return;

            if (GuildData.ChangeRole(playerUid, packet.targetPlayer, guild, packet.roleId))
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

        guildData.VerifyDataIntegrity((ICoreServerAPI)api);

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

        guildData.VerifyDataIntegrity((ICoreServerAPI)api);

        string guildFile = Path.Combine(GamePaths.DataPath, "guilds.json");
        File.WriteAllText(guildFile, JsonConvert.SerializeObject(guildData, settings));

        //byte[] data = SerializerUtil.Serialize(guildData);
        //((ICoreServerAPI)api).WorldManager.SaveGame.StoreData("guilds", data);
    }
}