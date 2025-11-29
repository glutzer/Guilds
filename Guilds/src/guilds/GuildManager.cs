using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client;

namespace Guilds;

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
    /// Handled in page.
    /// </summary>
    public event Action<GuildPacket>? OnClientReceivedGuildPacket;

    /// <summary>
    /// Called on client when own guilds are altered (for full ui reset).
    /// </summary>
    public event Action? OnPlayersGuildsChanged;

    public GuildManager(bool isServer, ICoreAPI api) : base(isServer, api, "guilds")
    {

    }

    public void TriggerClientUpdate(GuildPacket packet)
    {
        OnClientReceivedGuildPacket?.Invoke(packet);
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

            MainAPI.Sapi.Event.PlayerDisconnect += player =>
            {
                PlayerMetrics metrics = guildData.GetMetrics(player);
                metrics.UpdateMetrics(player);
                metrics.isOnline = false;

                BroadcastPacket(metrics);
            };
        }
        else
        {
            guildData.isClient = true;

            ScreenManager.hotkeyManager.RegisterHotKey("guild", "Guild Window", (int)GlKeys.BackSlash, triggerOnUpAlso: false);
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
            OnClientReceivedGuildPacket = null;
        }
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {
        channel.RegisterMessageType<GuildData>();
        channel.RegisterMessageType<GuildPacket>();
        channel.RegisterMessageType<RoleUpdateInfo>();
        channel.RegisterMessageType<PlayerMetrics>();
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.SetMessageHandler<GuildData>(p =>
        {
            guildData = p;
            p.isClient = true;
        });

        channel.SetMessageHandler<PlayerMetrics>(p =>
        {
            if (p == null || p.uid == null || p.lastName == null) return;
            guildData.playerMetrics[p.uid] = p;
        });

        channel.SetMessageHandler<GuildPacket>(HandleRequestFromServer);
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {
        channel.SetMessageHandler<GuildPacket>(HandleRequestFromClient);
    }

    /// <summary>
    /// Mirrored changes on client, if successfully changed.
    /// </summary>
    public void HandleRequestFromServer(GuildPacket packet)
    {
        string ownUid = MainAPI.Capi.World.Player.PlayerUID;
        string? playerUid = packet.fromUid;
        if (playerUid == null) return;

        MainAPI.Capi.Event.EnqueueMainThreadTask(() =>
        {
            TriggerClientUpdate(packet);
        }, "");

        if (packet.type == EnumGuildPacket.Create)
        {
            string? name = packet.ReadData<string>();
            if (name != null) guildData.TryCreateGuild(name, playerUid);
            if (playerUid == ownUid) OnPlayersGuildsChanged?.Invoke();
            return;
        }

        Guild? guild = guildData.GetGuild(packet.guildId);

        if (packet.type == EnumGuildPacket.RepGuild)
        {
            PlayerMetrics? metrics = guildData.GetMetrics(playerUid);
            if (metrics == null) return;

            if (guild == null)
            {
                metrics.reppedGuildId = -1;
            }
            else if (guild.HasMember(playerUid))
            {
                metrics.reppedGuildId = packet.guildId;
            }

            return;
        }

        if (guild == null) return;

        if (packet.type == EnumGuildPacket.UpdateRole)
        {
            RoleUpdateInfo? p = packet.ReadData<RoleUpdateInfo>();
            if (p == null || p.newName == null) return;
            guildData.TryUpdateRole(playerUid, p, packet.guildId, packet.roleId);
            return;
        }

        if (packet.type == EnumGuildPacket.UpdateInfo)
        {
            GuildUpdateInfo? p = packet.ReadData<GuildUpdateInfo>();
            if (p == null || p.name == null) return;

            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || !roleInfo.HasPermissions(GuildPerms.ManageGuildInfo)) return;

            guild.ChangeName(p.name);
            guild.SetColor(p.color);

            if (guild.HasMember(ownUid)) OnPlayersGuildsChanged?.Invoke();

            return;
        }

        if (packet.type == EnumGuildPacket.AcceptInvite)
        {
            guildData.TryAcceptInvite(playerUid, guild);
            if (playerUid == ownUid) OnPlayersGuildsChanged?.Invoke();
            return;
        }

        if (packet.type == EnumGuildPacket.CancelInvite && packet.targetPlayer == playerUid)
        {
            guildData.TryRemoveInvite(playerUid, guild, playerUid);
            return;
        }

        if (packet.type == EnumGuildPacket.AddRole)
        {
            GuildData.TryAddRole(playerUid, guild);
            return;
        }

        if (packet.type == EnumGuildPacket.RemoveRole)
        {
            GuildData.TryRemoveRole(playerUid, guild, packet.roleId);
            return;
        }

        if (packet.type == EnumGuildPacket.Disband)
        {
            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || roleInfo.id != 1) return; // Only founder may disband.

            bool inGuild = guild.HasMember(ownUid);
            guildData.DisbandGuild(guild);

            if (inGuild) OnPlayersGuildsChanged?.Invoke();

            return;
        }

        if (packet.type == EnumGuildPacket.Leave)
        {
            guildData.TryRemovePlayerFromGuild(playerUid, guild);

            if (playerUid == ownUid) OnPlayersGuildsChanged?.Invoke();

            return;
        }

        // All packets from here target another player.
        if (packet.targetPlayer == null) return;
        if (!guildData.IsValidUid(packet.targetPlayer)) return;

        if (packet.type is EnumGuildPacket.Invite)
        {
            guildData.TryAddInvite(playerUid, guild, packet.targetPlayer);
            return;
        }

        if (packet.type is EnumGuildPacket.CancelInvite)
        {
            guildData.TryRemoveInvite(playerUid, guild, packet.targetPlayer);
            return;
        }

        if (packet.type == EnumGuildPacket.Kick)
        {
            guildData.TryKickPlayer(playerUid, packet.targetPlayer, guild);

            if (playerUid == ownUid) OnPlayersGuildsChanged?.Invoke();

            return;
        }

        if (packet.type == EnumGuildPacket.Promote)
        {
            GuildData.TryChangeRole(playerUid, packet.targetPlayer, guild, packet.roleId);
            return;
        }
    }

    public void HandleRequestFromClient(IServerPlayer player, GuildPacket packet)
    {
        string playerUid = player.PlayerUID;
        packet.fromUid = playerUid;
        BroadcastPacket(packet);

        if (packet.type == EnumGuildPacket.Create)
        {
            string? name = packet.ReadData<string>();
            if (name != null) guildData.TryCreateGuild(name, playerUid);
            return;
        }

        Guild? guild = guildData.GetGuild(packet.guildId);

        if (packet.type == EnumGuildPacket.RepGuild)
        {
            PlayerMetrics metrics = guildData.GetMetrics(player);

            if (guild == null)
            {
                metrics.reppedGuildId = -1;
            }
            else if (guild.HasMember(playerUid))
            {
                metrics.reppedGuildId = packet.guildId;
            }

            return;
        }

        if (guild == null) return;

        if (packet.type == EnumGuildPacket.UpdateRole)
        {
            RoleUpdateInfo? p = packet.ReadData<RoleUpdateInfo>();
            if (p == null || p.newName == null) return;
            guildData.TryUpdateRole(playerUid, p, packet.guildId, packet.roleId);
            return;
        }

        if (packet.type == EnumGuildPacket.UpdateInfo)
        {
            GuildUpdateInfo? p = packet.ReadData<GuildUpdateInfo>();
            if (p == null || p.name == null) return;

            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || !roleInfo.HasPermissions(GuildPerms.ManageGuildInfo)) return;

            guild.ChangeName(p.name);
            guild.SetColor(p.color);
            return;
        }

        if (packet.type == EnumGuildPacket.AcceptInvite)
        {
            guildData.TryAcceptInvite(playerUid, guild);
            return;
        }

        if (packet.type == EnumGuildPacket.CancelInvite && packet.targetPlayer == player.PlayerUID)
        {
            guildData.TryRemoveInvite(playerUid, guild, playerUid);
            return;
        }

        if (packet.type == EnumGuildPacket.AddRole)
        {
            GuildData.TryAddRole(playerUid, guild);
            return;
        }

        if (packet.type == EnumGuildPacket.RemoveRole)
        {
            GuildData.TryRemoveRole(playerUid, guild, packet.roleId);
            return;
        }

        if (packet.type == EnumGuildPacket.Disband)
        {
            RoleInfo? roleInfo = guild.GetRole(playerUid);
            if (roleInfo == null || roleInfo.id != 1) return; // Only founder may disband.
            guildData.DisbandGuild(guild);
            return;
        }

        if (packet.type == EnumGuildPacket.Leave)
        {
            guildData.TryRemovePlayerFromGuild(playerUid, guild);
            return;
        }

        // All packets from here target another player.
        if (packet.targetPlayer == null) return;
        if (!guildData.IsValidUid(packet.targetPlayer)) return;

        if (packet.type is EnumGuildPacket.Invite)
        {
            guildData.TryAddInvite(playerUid, guild, packet.targetPlayer);
            return;
        }

        if (packet.type is EnumGuildPacket.CancelInvite)
        {
            guildData.TryRemoveInvite(playerUid, guild, packet.targetPlayer);
            return;
        }

        if (packet.type == EnumGuildPacket.Kick)
        {
            guildData.TryKickPlayer(playerUid, packet.targetPlayer, guild);
            return;
        }

        if (packet.type == EnumGuildPacket.Promote)
        {
            GuildData.TryChangeRole(playerUid, packet.targetPlayer, guild, packet.roleId);
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