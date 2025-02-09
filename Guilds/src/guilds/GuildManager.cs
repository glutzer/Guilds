using MareLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client;

namespace Guilds;

[Flags]
public enum EnumClientGuildUpdate
{
    GuildAdded = 1,
    GuildRemoved = 2,
    GuildRolesChanged = 4, // Roles changed or a players role changed.
    GuildInfoChanged = 8, // Guild name/color etc changed.
    GuildMembersChanged = 16, // Guild invites or guild member changes.
    MetricsChanged = 32 // Metrics of a player changed.
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

    /// <summary>
    /// Event when the client receives a server update.
    /// Attaches relevant object if possible.
    /// For updating gui.
    /// </summary>
    public static event Action<EnumClientGuildUpdate, object?>? OnClientUpdate;

    public GuildManager(bool isServer, ICoreAPI api) : base(isServer, api, "guilds")
    {

    }

    public static void TriggerClientUpdate(EnumClientGuildUpdate type, object? obj)
    {
        OnClientUpdate?.Invoke(type, obj);
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

                BroadcastPacket(metrics);
                SendPacket(guildData, player); // Sync full data.
            };

            MainAPI.Sapi.Event.PlayerLeave += player =>
            {
                PlayerMetrics metrics = guildData.GetMetrics(player);
                metrics.UpdateMetrics(player);
                metrics.isOnline = false;

                BroadcastPacket(metrics);
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
        else
        {
            OnClientUpdate = null;
        }
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {
        channel.RegisterMessageType<GuildData>();
        channel.RegisterMessageType<GuildRequestPacket>();
        channel.RegisterMessageType<RoleUpdatePacket>();
        channel.RegisterMessageType<GuildInfoPacket>();
        channel.RegisterMessageType<PlayerMetrics>();
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.SetMessageHandler<GuildData>(p =>
        {
            guildData = p;
        });

        channel.SetMessageHandler<PlayerMetrics>(p =>
        {
            if (p == null || p.uid == null || p.lastName == null) return;
            guildData.playerMetrics[p.uid] = p;

            // Events.
            TriggerClientUpdate(EnumClientGuildUpdate.MetricsChanged, p);
        });

        channel.SetMessageHandler<GuildRequestPacket>(HandleRequestFromServer);

        channel.SetMessageHandler<RoleUpdatePacket>(p =>
        {
            if (p.fromUid == null) return;
            IPlayer? player = MainAPI.Capi.World.PlayerByUid(p.fromUid);
            if (player == null) return;

            if (guildData.UpdateRole(player, p))
            {
                Guild? guild = guildData.GetGuild(p.guildId);

                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildRolesChanged, guild);
            }
        });

        channel.SetMessageHandler<GuildInfoPacket>(p =>
        {
            if (p.name == null) return;
            if (p.fromUid == null) return;
            IPlayer? player = MainAPI.Capi.World.PlayerByUid(p.fromUid);
            if (player == null) return;

            Guild? guild = guildData.GetGuild(p.guildId);
            if (guild == null) return;

            RoleInfo? roleInfo = guild.GetRole(player.PlayerUID);
            if (roleInfo == null || !roleInfo.HasPermissions(GuildPerms.ManageGuildInfo)) return;

            guild.ChangeName(p.name);
            guild.SetColor(p.color);

            // Events.
            TriggerClientUpdate(EnumClientGuildUpdate.GuildInfoChanged, guild);
        });
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {
        channel.SetMessageHandler<GuildRequestPacket>(HandleRequestFromClient);

        channel.SetMessageHandler<RoleUpdatePacket>((player, p) =>
        {
            if (guildData.UpdateRole(player, p))
            {
                p.fromUid = player.PlayerUID;
                BroadcastPacket(p);
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

            p.fromUid = player.PlayerUID;
            BroadcastPacket(p);
        });
    }

    /// <summary>
    /// Mirrored changes on client, if successfully changed.
    /// </summary>
    public void HandleRequestFromServer(GuildRequestPacket packet)
    {
        string? playerUid = packet.fromUid;
        if (playerUid == null) return;
        IPlayer? player = MainAPI.Capi.World.PlayerByUid(playerUid);
        if (player == null) return;

        if (packet.type == EnumGuildRequestPacket.Create)
        {
            string? name = packet.ReadData<string>();
            if (name == null) return;

            if (guildData.CreateGuild(name, player))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildAdded, guildData.GetGuild(guildData.nextGuildId - 1));
            }

            return;
        }

        Guild? guild = guildData.GetGuild(packet.guildId);

        if (packet.type == EnumGuildRequestPacket.RepGuild)
        {
            PlayerMetrics metrics = guildData.GetMetrics(player);

            if (guild == null)
            {
                metrics.reppedGuildId = -1;
                // Events.
                //TriggerClientUpdate(EnumClientGuildUpdate.MetricsChanged, metrics);
            }
            else if (guild.HasMember(playerUid))
            {
                metrics.reppedGuildId = packet.guildId;
                // Events.
                //TriggerClientUpdate(EnumClientGuildUpdate.MetricsChanged, metrics);
            }

            return;
        }

        if (guild == null) return;

        if (packet.type == EnumGuildRequestPacket.AcceptInvite)
        {
            if (guildData.AcceptInvite(playerUid, guild))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildMembersChanged, guild);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.CancelInvite && packet.targetPlayer == player.PlayerUID)
        {
            if (guildData.RemoveInvite(playerUid, guild, playerUid))
            {
                // Events.
            }
        }

        if (packet.type == EnumGuildRequestPacket.AddRole)
        {
            if (GuildData.AddRole(player, guild))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildRolesChanged, guild);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.RemoveRole)
        {
            if (GuildData.RemoveRole(player, guild, packet.roleId))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildRolesChanged, guild);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Disband)
        {
            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || roleInfo.id != 1) return; // Only founder may disband.

            string ownUid = MainAPI.Capi.World.Player.PlayerUID;
            bool inGuild = guild.HasMember(ownUid);

            if (guildData.DisbandGuild(guild))
            {
                // Events.
                //MainAPI.GetGameSystem<ClaimManager>(EnumAppSide.Client).OnGuildDisbanded(packet.guildId);
                // Claim manager deals with this.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildRemoved, inGuild);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Leave)
        {
            if (guildData.RemovePlayerFromGuild(playerUid, guild))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildMembersChanged, guild);
            }

            return;
        }

        // All packets from here target another player.
        if (packet.targetPlayer == null) return;
        if (!guildData.IsValidUid(packet.targetPlayer)) return;

        if (packet.type is EnumGuildRequestPacket.Invite)
        {
            if (guildData.AddInvite(playerUid, guild, packet.targetPlayer))
            {
                // Events
            }

            return;
        }

        if (packet.type is EnumGuildRequestPacket.CancelInvite)
        {
            if (guildData.RemoveInvite(playerUid, guild, packet.targetPlayer))
            {
                // Events.
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Kick)
        {
            if (guildData.KickPlayer(playerUid, packet.targetPlayer, guild))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildMembersChanged, guild);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Promote)
        {
            if (packet.targetPlayer == null) return;

            if (GuildData.ChangeRole(playerUid, packet.targetPlayer, guild, packet.roleId))
            {
                // Events.
                TriggerClientUpdate(EnumClientGuildUpdate.GuildRolesChanged, guild);
            }

            return;
        }
    }

    public void HandleRequestFromClient(IServerPlayer player, GuildRequestPacket packet)
    {
        string playerUid = player.PlayerUID;
        packet.fromUid = playerUid;

        if (packet.type == EnumGuildRequestPacket.Create)
        {
            string? name = packet.ReadData<string>();
            if (name == null) return;

            if (guildData.CreateGuild(name, player))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        Guild? guild = guildData.GetGuild(packet.guildId);

        if (packet.type == EnumGuildRequestPacket.RepGuild)
        {
            PlayerMetrics metrics = guildData.GetMetrics(player);

            if (guild == null)
            {
                metrics.reppedGuildId = -1;
                BroadcastPacket(packet);
            }
            else if (guild.HasMember(playerUid))
            {
                metrics.reppedGuildId = packet.guildId;
                BroadcastPacket(packet);
            }

            return;
        }

        if (guild == null) return;

        if (packet.type == EnumGuildRequestPacket.AcceptInvite)
        {
            if (guildData.AcceptInvite(playerUid, guild))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.CancelInvite && packet.targetPlayer == player.PlayerUID)
        {
            if (guildData.RemoveInvite(playerUid, guild, playerUid))
            {
                BroadcastPacket(packet);
            }
        }

        if (packet.type == EnumGuildRequestPacket.AddRole)
        {
            if (GuildData.AddRole(player, guild))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.RemoveRole)
        {
            if (GuildData.RemoveRole(player, guild, packet.roleId))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Disband)
        {
            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || roleInfo.id != 1) return; // Only founder may disband.

            if (guildData.DisbandGuild(guild))
            {
                BroadcastPacket(packet);
                MainAPI.GetGameSystem<ClaimManager>(EnumAppSide.Server).OnGuildDisbanded(packet.guildId);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Leave)
        {
            if (guildData.RemovePlayerFromGuild(playerUid, guild))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        // All packets from here target another player.
        if (packet.targetPlayer == null) return;
        if (!guildData.IsValidUid(packet.targetPlayer)) return;

        if (packet.type is EnumGuildRequestPacket.Invite)
        {
            if (guildData.AddInvite(playerUid, guild, packet.targetPlayer))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        if (packet.type is EnumGuildRequestPacket.CancelInvite)
        {
            if (guildData.RemoveInvite(playerUid, guild, packet.targetPlayer))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Kick)
        {
            if (guildData.KickPlayer(playerUid, packet.targetPlayer, guild))
            {
                BroadcastPacket(packet);
            }

            return;
        }

        if (packet.type == EnumGuildRequestPacket.Promote)
        {
            if (packet.targetPlayer == null) return;

            if (GuildData.ChangeRole(playerUid, packet.targetPlayer, guild, packet.roleId))
            {
                BroadcastPacket(packet);
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
    }

    private void OnSave()
    {
        JsonSerializerSettings settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new IgnorePropertiesResolver()
        };

        guildData.VerifyDataIntegrity((ICoreServerAPI)api);

        string guildFile = Path.Combine(GamePaths.DataPath, "guilds.json");
        File.WriteAllText(guildFile, JsonConvert.SerializeObject(guildData, settings));
    }
}

public class IgnorePropertiesResolver : DefaultContractResolver
{
    public IgnorePropertiesResolver()
    {

    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);

        // Check if member is a property or field.
        if (member is PropertyInfo prop)
        {
            property.ShouldSerialize = _ => false;
        }

        return property;
    }
}