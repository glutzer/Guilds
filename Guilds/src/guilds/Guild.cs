using MareLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Guilds;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public enum GuildStatus
{
    Success,
    Error,
    GuildNotFound,
    NotInGuild,
    AlreadyInGuild,
    GuildExists,
    NoPermission
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
[Flags]
public enum GuildPerms
{
    Invite = 0,
    Kick = 1 << 0,
    Promote = 1 << 1,
    ManageRoles = 1 << 2,
    ManageClaims = 1 << 3,
    ManageGuildInfo = 1 << 4,
    BreakBlocks = 1 << 5,
    UseBlocks = 1 << 6
}

[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class RoleInfo
{
    [JsonProperty]
    public string name;
    [JsonProperty]
    public int id;
    [JsonProperty]
    private GuildPerms guildPerms;

    /// <summary>
    /// Level of privilege over other players.
    /// </summary>
    [JsonProperty]
    public int authority;

    public RoleInfo(string name, int id)
    {
        this.name = name;
        this.id = id;
    }

    public RoleInfo()
    {

    }

    public RoleInfo AddPermissions(GuildPerms perms)
    {
        guildPerms |= perms;
        return this;
    }

    public RoleInfo RemovePermissions(GuildPerms perms)
    {
        guildPerms &= ~perms;
        return this;
    }

    public bool HasPermissions(GuildPerms perms)
    {
        return (guildPerms & perms) == perms;
    }

    /// <summary>
    /// Give all permissions to this role.
    /// </summary>
    public RoleInfo Admin()
    {
        guildPerms = (GuildPerms)int.MaxValue;
        authority = int.MaxValue;

        return this;
    }

    public void ChangeRole(RoleChangePacket packet)
    {
        name = packet.newName!; // Checked null beforehand.
        authority = packet.newAuthority;
        guildPerms = packet.newPerms;
    }

    public GuildPerms GetPermissions()
    {
        return guildPerms;
    }
}

[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildData
{
    /// <summary>
    /// Guild name to guild. (All guilds stored in here.
    /// </summary>
    [JsonProperty]
    private Dictionary<string, Guild> guilds = new();

    /// <summary>
    /// Player uid to guilds they're in for indexing.
    /// </summary>
    [JsonProperty]
    private Dictionary<string, HashSet<string>> members = new();

    /// <summary>
    /// Player uid to guilds they are currently invited to.
    [JsonProperty]
    private Dictionary<string, HashSet<string>> pendingInvites = new();

    /// <summary>
    /// Metrics for every player that has logged in, by uid.
    /// </summary>
    [JsonProperty]
    private Dictionary<string, PlayerMetrics> metrics = new();

    public IEnumerable<PlayerMetrics> AllMetrics => metrics.Values;

    public bool IsValidUid(string uid)
    {
        return metrics.ContainsKey(uid);
    }

    /// <summary>
    /// When saving and loading on the server, verify data is correct.
    /// </summary>
    public void VerifyDataIntegrity()
    {
        // Set offline to all metrics.
        foreach (PlayerMetrics metrics in AllMetrics)
        {
            metrics.isOnline = false;
        }

        // Remove all values from members with an empty or null hash set.
        foreach (string playerUid in members.Keys.ToArray())
        {
            if (members[playerUid].Count == 0)
            {
                members.Remove(playerUid);
            }
        }

        foreach (string playerUid in pendingInvites.Keys.ToArray())
        {
            if (pendingInvites[playerUid].Count == 0)
            {
                pendingInvites.Remove(playerUid);
            }
        }

        // Check if any guild has 0 members, remove it.
        foreach (string guildName in guilds.Keys.ToArray())
        {
            if (guilds[guildName].MemberCount == 0)
            {
                guilds.Remove(guildName);
            }
        }

        // If a member is not in any of the guilds, remove it from the set.
        foreach (KeyValuePair<string, HashSet<string>> kvp in members)
        {
            foreach (string guildName in kvp.Value)
            {
                Guild? guild = GetGuild(guildName);
                if (guild == null)
                {
                    kvp.Value.Remove(guildName);
                    continue;
                }

                if (!guild.HasMember(kvp.Key))
                {
                    kvp.Value.Remove(guildName);
                }
            }
        }

        List<string> toRemove = new();
        foreach (KeyValuePair<string, PlayerMetrics> kvp in metrics)
        {
            // Check if over 6 months since last login.
            if (kvp.Value.lastOnline + 15552000 < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            // Unequal uid.
            if (kvp.Key != kvp.Value.uid)
            {
                toRemove.Add(kvp.Key);
            }
        }
    }

    public GuildStatus AddInvite(string playerUid, string guildName, string invitedUid)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        RoleInfo? role = GetPlayersRole(guildName, playerUid);
        if (role == null) return GuildStatus.Error;

        if (!role.HasPermissions(GuildPerms.Invite)) return GuildStatus.NoPermission;

        HashSet<string> invites = GetPlayersInvites(invitedUid);

        invites.Add(guild.Name);
        guild.AddInvite(invitedUid);

        return GuildStatus.Success;
    }

    public GuildStatus AcceptInvite(string invitedUid, string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        HashSet<string> invites = GetPlayersInvites(invitedUid);

        if (!invites.Remove(guildName))
        {
            guild.RemoveInvite(invitedUid);
            return GuildStatus.Error;
        }

        return AddPlayerToGuild(invitedUid, guild.Name);
    }

    /// <summary>
    /// Removes an invite, allows any permission if to self.
    /// </summary>
    public GuildStatus RemoveInvite(string playerUid, string guildName, string invitedUid)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        if (playerUid != invitedUid)
        {
            RoleInfo? role = GetPlayersRole(guildName, playerUid);
            if (role == null) return GuildStatus.Error;

            if (!role.HasPermissions(GuildPerms.Invite)) return GuildStatus.NoPermission;
        }

        HashSet<string> invites = GetPlayersInvites(invitedUid);

        invites.Remove(guild.Name);
        guild.RemoveInvite(invitedUid);

        return GuildStatus.Success;
    }

    public bool AddClientInvite(string playerUid, string guildName)
    {
        HashSet<string> invites = GetPlayersInvites(playerUid);
        return invites.Add(guildName);
    }

    public bool RemoveClientInvite(string playerUid, string guildName)
    {
        HashSet<string> invites = GetPlayersInvites(playerUid);
        return invites.Remove(guildName);
    }

    public HashSet<string> GetPlayersGuilds(string playerUid)
    {
        if (!members.TryGetValue(playerUid, out HashSet<string>? guildList))
        {
            guildList = new HashSet<string>();
            members[playerUid] = guildList;
        }

        if (guildList == null)
        {
            guildList = new HashSet<string>();
            members[playerUid] = guildList;
        }

        return guildList;
    }

    public HashSet<string> GetPlayersInvites(string playerUid)
    {
        if (!pendingInvites.TryGetValue(playerUid, out HashSet<string>? guildList))
        {
            guildList = new HashSet<string>();
            pendingInvites[playerUid] = guildList;
        }

        if (guildList == null)
        {
            guildList = new HashSet<string>();
            pendingInvites[playerUid] = guildList;
        }

        return guildList;
    }

    public GuildStatus UpdateRole(IServerPlayer player, RoleChangePacket packet)
    {
        if (packet.guildName == null || packet.newName == null) return GuildStatus.Error;

        Guild? guild = GetGuild(packet.guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        RoleInfo? role = GetPlayersRole(packet.guildName, player);
        if (role == null) return GuildStatus.Error;

        if (!role.HasPermissions(GuildPerms.ManageRoles)) return GuildStatus.NoPermission;

        if (packet.newAuthority >= role.authority) packet.newAuthority = role.authority - 1;

        RoleInfo? targetRole = guild.GetRole(packet.roleId);
        if (targetRole == null) return GuildStatus.Error;

        if (targetRole.authority >= role.authority) return GuildStatus.NoPermission;

        targetRole.ChangeRole(packet);
        return GuildStatus.Success; // Success, re-send the guild.
    }

    /// <summary>
    /// Add a new role with a random name.
    /// </summary>
    public GuildStatus AddRole(IServerPlayer player, string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        RoleInfo? role = GetPlayersRole(guildName, player);
        if (role == null) return GuildStatus.Error;

        if (!role.HasPermissions(GuildPerms.ManageRoles)) return GuildStatus.NoPermission;

        guild.AddRole();

        return GuildStatus.Success;
    }

    public GuildStatus RemoveRole(IServerPlayer player, string guildName, int roleId)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        RoleInfo? role = GetPlayersRole(guildName, player);
        if (role == null) return GuildStatus.Error;

        if (!role.HasPermissions(GuildPerms.ManageRoles)) return GuildStatus.NoPermission;

        RoleInfo? targetRole = guild.GetRole(roleId);
        if (targetRole == null || targetRole.authority >= role.authority) return GuildStatus.NoPermission;

        if (!guild.RemoveRole(roleId)) return GuildStatus.Error;

        return GuildStatus.Success;
    }

    public bool IsPlayerInGuild(string playerUid, string guildName)
    {
        return GetPlayersGuilds(playerUid).Contains(guildName);
    }

    public Guild? GetGuild(string guildName)
    {
        if (!guilds.TryGetValue(guildName, out Guild? guild)) return null;
        return guild;
    }

    public RoleInfo? GetPlayersRole(string guildName, string playerUid)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return null;
        return guild.GetRole(playerUid);
    }

    public RoleInfo? GetPlayersRole(string guildName, IPlayer player)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return null;
        return guild.GetRole(player.PlayerUID);
    }

    public PlayerMetrics GetMetrics(IPlayer player)
    {
        if (!metrics.TryGetValue(player.PlayerUID, out PlayerMetrics? playerMetrics))
        {
            if (player is IClientPlayer clientPlayer)
            {
                playerMetrics = new PlayerMetrics(clientPlayer);
            }
            else
            {
                playerMetrics = new PlayerMetrics((IServerPlayer)player);
            }

            metrics[player.PlayerUID] = playerMetrics;
        }

        return playerMetrics;
    }

    public PlayerMetrics? GetMetrics(string playerUid)
    {
        if (!metrics.TryGetValue(playerUid, out PlayerMetrics? playerMetrics)) return null;
        return playerMetrics;
    }

    public void UpdateClientMetrics(PlayerMetrics metric)
    {
        if (metric.uid == null || metric.lastName == null) return;

        metrics[metric.uid] = metric;
    }

    public void UpdateClientMetrics(List<PlayerMetrics> metrics)
    {
        metrics.Clear();

        foreach (PlayerMetrics metric in metrics)
        {
            UpdateClientMetrics(metric);
        }
    }

    /// <summary>
    /// Add a player to a guild.
    /// </summary>
    public GuildStatus AddPlayerToGuild(string playerUid, string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null)
        {
            return GuildStatus.GuildNotFound;
        }

        HashSet<string> guilds = GetPlayersGuilds(playerUid);
        guilds.Add(guildName);
        guild.AddMember(playerUid);

        return GuildStatus.Success;
    }

    /// <summary>
    /// Remove a player from a guild.
    /// </summary>
    public GuildStatus RemovePlayerFromGuild(string playerUid, string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null)
        {
            return GuildStatus.GuildNotFound;
        }

        RoleInfo? role = guild.GetRole(playerUid);
        if (role?.id == 1) return GuildStatus.Error;

        HashSet<string> guilds = GetPlayersGuilds(playerUid);

        if (!guilds.Contains(guildName))
        {
            return GuildStatus.NotInGuild;
        }

        guilds.Remove(guildName);
        guild.RemoveMember(playerUid);

        return GuildStatus.Success;
    }

    /// <summary>
    /// Found a guild with a player.
    /// </summary>
    public GuildStatus CreateGuild(string guildName, IPlayer foundingPlayer)
    {
        if (guilds.ContainsKey(guildName))
        {
            return GuildStatus.GuildExists;
        }

        Guild guild = new(foundingPlayer, guildName);
        guilds[guildName] = guild;

        GetPlayersGuilds(foundingPlayer.PlayerUID).Add(guild.Name);

        return GuildStatus.Success;
    }

    /// <summary>
    /// Disbands a guild, returns all uids in that guild if successful.
    /// </summary>
    public GuildStatus DisbandGuild(string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        foreach (MembershipInfo info in guild.MemberInfo)
        {
            GetPlayersGuilds(info.playerUid).Remove(guild.Name);
        }

        guilds.Remove(guildName);

        return GuildStatus.Success;
    }

    /// <summary>
    /// Promote a player on the server.
    /// </summary>
    public GuildStatus ChangeRole(string actingUid, string targetPlayerUid, string guildName, int targetRole)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        // Get acting player role.
        RoleInfo? actingRole = GetPlayersRole(actingUid, guildName);
        RoleInfo? targetPlayerRole = GetPlayersRole(targetPlayerUid, guildName);
        RoleInfo? role = guild.GetRole(targetRole);

        if (actingRole == null || targetPlayerRole == null || role == null) return GuildStatus.Error;

        if (!actingRole.HasPermissions(GuildPerms.Promote)) return GuildStatus.NoPermission;
        if (targetPlayerRole.authority >= actingRole.authority || role.authority >= actingRole.authority) return GuildStatus.NoPermission;

        guild.ChangeRole(targetPlayerUid, role.id);

        return GuildStatus.Success;
    }

    /// <summary>
    /// Handle kick packet.
    /// </summary>
    public GuildStatus KickPlayer(string actingUid, string targetPlayerUid, string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return GuildStatus.GuildNotFound;

        RoleInfo? actingRole = GetPlayersRole(actingUid, guildName);
        RoleInfo? targetPlayerRole = GetPlayersRole(targetPlayerUid, guildName);

        if (actingRole == null || targetPlayerRole == null) return GuildStatus.Error;
        if (targetPlayerRole.authority >= actingRole.authority || !actingRole.HasPermissions(GuildPerms.Kick)) return GuildStatus.NoPermission;

        // Remove player from guild.
        return RemovePlayerFromGuild(targetPlayerUid, guildName);
    }

    // Packets ----------
    public void GuildReceived(Guild data)
    {
        guilds[data.Name] = data;
        RefreshClient();
    }

    public void SyncGuild(GuildManager manager, string guildName)
    {
        Guild? guild = GetGuild(guildName);
        if (guild == null) return;
        manager.BroadcastPacket(guild);
    }

    public void GuildsReceived(FullGuildPacket packet)
    {
        if (packet.guilds == null) return;
        guilds = packet.guilds;
        ResetClient(); // Guilds added/removed.
    }

    public void SyncGuilds(GuildManager manager)
    {
        FullGuildPacket packet = new()
        {
            guilds = guilds
        };

        manager.BroadcastPacket(packet);
    }

    public void MembersReceived(FullMembersPacket packet)
    {
        if (packet.members == null) return;
        members = packet.members;
        RefreshClient();
    }

    public void SyncMembers(GuildManager manager)
    {
        FullMembersPacket packet = new()
        {
            members = members
        };

        manager.BroadcastPacket(packet);
    }

    public void InvitesReceived(FullInvitesPacket packet)
    {
        if (packet.pendingInvites == null) return;
        pendingInvites = packet.pendingInvites;
        RefreshClient();
    }

    public void SyncInvites(GuildManager manager)
    {
        FullInvitesPacket packet = new()
        {
            pendingInvites = pendingInvites
        };

        manager.BroadcastPacket(packet);
    }

    public void MetricsReceived(FullMetricsPacket packet)
    {
        if (packet.metrics == null) return;
        metrics = packet.metrics;
        RefreshClient();
    }

    public void SyncMetrics(GuildManager manager)
    {
        FullMetricsPacket packet = new()
        {
            metrics = metrics
        };

        manager.BroadcastPacket(packet);
    }

    public static void RefreshClient()
    {
        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        if (manager.guildGui?.IsOpened() == true)
        {
            manager.guildGui?.RefreshPage();
        }
    }

    public static void ResetClient()
    {
        GuildManager manager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        if (manager.guildGui?.IsOpened() == true)
        {
            manager.guildGui?.SetWidgets();
        }
    }
}

/// <summary>
/// Membership info of each player.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class MembershipInfo
{
    /// <summary>
    /// What role this player has in this guild.
    /// 0 by default.
    /// </summary>
    public int roleId;
    public string playerUid = "NaN";

    public MembershipInfo(string playerUid)
    {
        this.playerUid = playerUid;
    }

    public MembershipInfo()
    {

    }
}

/// <summary>
/// Player metrics for every tracked player.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class PlayerMetrics
{
    [JsonProperty]
    public string lastName = "NaN";
    [JsonProperty]
    public long lastOnline;
    [JsonProperty]
    public bool isOnline;
    [JsonProperty]
    public string uid = "NaN";

    public PlayerMetrics(IServerPlayer player)
    {
        lastName = player.PlayerName;
        uid = player.PlayerUID;

        isOnline = player.ConnectionState != EnumClientState.Offline;

        DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow;
        lastOnline = dateTimeOffset.ToUnixTimeSeconds();
    }

    public PlayerMetrics(IClientPlayer player)
    {
        lastName = player.PlayerName;
        uid = player.PlayerUID;

        isOnline = false;

        DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow;
        lastOnline = dateTimeOffset.ToUnixTimeSeconds();
    }

    public void UpdateMetrics(IServerPlayer player)
    {
        lastName = player.PlayerName;
        isOnline = player.ConnectionState != EnumClientState.Offline;

        DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow;
        lastOnline = dateTimeOffset.ToUnixTimeSeconds();
    }

    public string GetLastOnlineString()
    {
        if (isOnline) return "Online";

        DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow;
        long currentTime = dateTimeOffset.ToUnixTimeSeconds();

        long timeSinceLastOnline = currentTime - lastOnline;

        if (timeSinceLastOnline < 60)
        {
            return $"{timeSinceLastOnline}s ago";
        }
        else if (timeSinceLastOnline < 3600)
        {
            return $"{timeSinceLastOnline / 60}m ago";
        }
        else if (timeSinceLastOnline < 86400)
        {
            return $"{timeSinceLastOnline / 3600}h ago";
        }
        else
        {
            return $"{timeSinceLastOnline / 86400}d ago";
        }
    }

    public PlayerMetrics()
    {

    }
}

/// <summary>
/// Guild sent to client, saved on server.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class Guild
{
    [JsonProperty]
    public string Name { get; private set; }
    public int MemberCount => members.Count;

    /// <summary>
    /// Enumerate over every member.
    /// </summary>
    public IEnumerable<MembershipInfo> MemberInfo => members.Values;

    /// <summary>
    /// Player uid to info about their membership.
    /// </summary>
    [JsonProperty]
    private readonly Dictionary<string, MembershipInfo> members = new();

    // Players currently invited to this guild.
    [JsonProperty]
    private HashSet<string> invites = new();

    [JsonProperty]
    public RoleInfo[] roles;

    public RoleInfo? GetRole(string playerUid)
    {
        roles ??= InitializeRoles();

        if (!members.TryGetValue(playerUid, out MembershipInfo? memberInfo)) return null;
        if (memberInfo.roleId >= roles.Length) return null;
        return roles[memberInfo.roleId];
    }

    public RoleInfo? GetRole(int roleId)
    {
        roles ??= InitializeRoles();
        if (roleId >= roles.Length) return null;
        return roles[roleId];
    }

    public bool HasMember(string playerUid)
    {
        return members.ContainsKey(playerUid);
    }

    public Guild(IPlayer foundingPlayer, string name)
    {
        Name = name;

        // Initialize roles.
        // Roles 0 and 1 can't be deleted, they are the designated member and founder role. They can be altered.
        // People with role management permissions can't manage roles lower than their authority, and can't raise authority higher than theirs.
        roles = InitializeRoles();

        // Default founder to founder role.
        MembershipInfo membershipInfo = new(foundingPlayer.PlayerUID);
        members[foundingPlayer.PlayerUID] = membershipInfo;
        membershipInfo.roleId = 1;
    }

    public void ChangeName(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Add a new randomly named role.
    /// </summary>
    public void AddRole()
    {
        RoleInfo roleInfo = new("New Role", roles.Length);
        Array.Resize(ref roles, roles.Length + 1);
        roles[^1] = roleInfo;
    }

    /// <summary>
    /// Tries to remove a role, returns if actually removed.
    /// </summary>
    public bool RemoveRole(int roleId)
    {
        if (roleId >= roles.Length) return false;

        RoleInfo roleInfo = roles[roleId];

        RoleInfo[] newRoles = roles.Where(inf => inf.id != roleId).ToArray();

        if (roleId is 0 or 1) return false;

        for (int i = 0; i < newRoles.Length; i++)
        {
            newRoles[i].id = i;
        }

        roles = newRoles;

        foreach (MembershipInfo membership in MemberInfo)
        {
            if (membership.roleId == roleId) membership.roleId = 0;
        }

        return true;
    }

    private static RoleInfo[] InitializeRoles()
    {
        RoleInfo[] roles = new RoleInfo[2];
        roles[0] = new RoleInfo("Member", 0);
        roles[1] = new RoleInfo("Founder", 1).Admin();
        return roles;
    }

    public bool AddInvite(string playerUid)
    {
        invites ??= new HashSet<string>();
        return invites.Add(playerUid);
    }

    public bool RemoveInvite(string playerUid)
    {
        invites ??= new HashSet<string>();
        return invites.Remove(playerUid);
    }

    public bool IsInvited(string playerUid)
    {
        invites ??= new HashSet<string>();
        return invites.Contains(playerUid);
    }

    public void AddMember(string playerUid)
    {
        members[playerUid] = new MembershipInfo(playerUid);
    }

    public void RemoveMember(string playerUid)
    {
        members.Remove(playerUid);
    }

    public void ChangeRole(string playerUid, int roleId)
    {
        if (!members.TryGetValue(playerUid, out MembershipInfo? memberInfo)) return;
        memberInfo.roleId = roleId;
    }

    public Guild()
    {

    }
}