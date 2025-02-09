using Newtonsoft.Json;
using OpenTK.Mathematics;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Guilds;

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

    public void ChangeRole(RoleUpdatePacket packet)
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
    [JsonProperty]
    public Dictionary<int, Guild> guilds = new();

    [JsonProperty]
    public int nextGuildId = 0;

    /// <summary>
    /// Map of player uid to every guild id they are in.
    /// </summary>
    [JsonProperty]
    public Dictionary<string, HashSet<int>> playerToGuilds = new();

    /// <summary>
    /// Player uid to guilds they are invited to.
    /// </summary>
    [JsonProperty]
    public Dictionary<string, HashSet<int>> playerToInvites = new();

    /// <summary>
    /// Metrics for every player that has logged in, by uid.
    /// </summary>
    [JsonProperty]
    public Dictionary<string, PlayerMetrics> playerMetrics = new();
    public IEnumerable<PlayerMetrics> AllMetrics => playerMetrics.Values;

    public bool IsValidUid(string uid)
    {
        // If a player is not in the metrics, he is not registered on the server.
        return playerMetrics.ContainsKey(uid);
    }

    /// <summary>
    /// When saving and loading on the server, verify data is correct.
    /// </summary>
    public void VerifyDataIntegrity(ICoreServerAPI sapi)
    {
        guilds ??= new Dictionary<int, Guild>();

        // Set offline to players not here.
        foreach (PlayerMetrics metrics in AllMetrics)
        {
            if (sapi.World.PlayerByUid(metrics.uid) == null)
            {
                metrics.isOnline = false;
            }
        }

        // Remove all values from members with an empty or null hash set.
        foreach (string playerUid in playerToGuilds.Keys.ToArray())
        {
            if (playerToGuilds[playerUid].Count == 0)
            {
                playerToGuilds.Remove(playerUid);
            }
        }

        foreach (string playerUid in playerToInvites.Keys.ToArray())
        {
            if (playerToInvites[playerUid].Count == 0)
            {
                playerToInvites.Remove(playerUid);
            }
        }

        // If a member is not in any of the guilds, remove it from the set.
        foreach (KeyValuePair<string, HashSet<int>> kvp in playerToGuilds)
        {
            foreach (int guildId in kvp.Value)
            {
                Guild? guild = GetGuild(guildId);
                if (guild == null)
                {
                    kvp.Value.Remove(guildId);
                    continue;
                }

                if (!guild.HasMember(kvp.Key))
                {
                    kvp.Value.Remove(guildId);
                }
            }
        }

        List<string> toRemove = new();
        foreach (KeyValuePair<string, PlayerMetrics> kvp in playerMetrics)
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

    #region Invites

    public HashSet<int> GetPlayersInvites(string playerUid)
    {
        if (!playerToInvites.TryGetValue(playerUid, out HashSet<int>? guildList) || guildList == null)
        {
            guildList = new HashSet<int>();
            playerToInvites[playerUid] = guildList;
        }

        return guildList;
    }

    public bool AddInvite(string playerUid, Guild guild, string invitedUid)
    {
        RoleInfo? role = guild.GetRole(playerUid);
        if (role == null) return false;

        if (!role.HasPermissions(GuildPerms.Invite)) return false;

        HashSet<int> invites = GetPlayersInvites(invitedUid);

        invites.Add(guild.id);
        guild.AddInvite(invitedUid);

        return true;
    }

    public bool RemoveInvite(string playerUid, Guild guild, string invitedUid)
    {
        if (playerUid != invitedUid)
        {
            RoleInfo? role = guild.GetRole(playerUid);
            if (role == null) return false;

            if (!role.HasPermissions(GuildPerms.Invite)) return false;
        }

        HashSet<int> invites = GetPlayersInvites(invitedUid);

        invites.Remove(guild.id);
        guild.RemoveInvite(invitedUid);

        return true;
    }

    public bool AcceptInvite(string invitedUid, Guild guild)
    {
        HashSet<int> invites = GetPlayersInvites(invitedUid);

        if (!invites.Remove(guild.id))
        {
            guild.RemoveInvite(invitedUid);
            return false;
        }

        return AddPlayerToGuild(invitedUid, guild);
    }

    public bool AddClientInvite(string playerUid, int guildId)
    {
        HashSet<int> invites = GetPlayersInvites(playerUid);
        return invites.Add(guildId);
    }

    public bool RemoveClientInvite(string playerUid, int guildId)
    {
        HashSet<int> invites = GetPlayersInvites(playerUid);
        return invites.Remove(guildId);
    }

    #endregion

    #region Guilds

    public HashSet<int> GetPlayersGuilds(string playerUid)
    {
        if (!playerToGuilds.TryGetValue(playerUid, out HashSet<int>? guildList) || guildList == null)
        {
            guildList = new HashSet<int>();
            playerToGuilds[playerUid] = guildList;
        }

        return guildList;
    }

    public Guild? GetGuild(int guildId)
    {
        if (guilds.TryGetValue(guildId, out Guild? guild))
        {
            return guild;
        }

        return null;
    }

    public bool AddPlayerToGuild(string playerUid, Guild guild)
    {
        HashSet<int> guilds = GetPlayersGuilds(playerUid);
        guilds.Add(guild.id);
        guild.AddMember(playerUid);

        return true;
    }

    public bool RemovePlayerFromGuild(string playerUid, Guild guild)
    {
        RoleInfo? role = guild.GetRole(playerUid);
        if (role?.id == 1) return false;

        HashSet<int> guilds = GetPlayersGuilds(playerUid);

        if (!guilds.Contains(guild.id))
        {
            return false;
        }

        guilds.Remove(guild.id);
        guild.RemoveMember(playerUid);

        PlayerMetrics? metrics = GetMetrics(playerUid);
        if (metrics != null && metrics.reppedGuildId == guild.id)
        {
            metrics.reppedGuildId = -1;
        }

        return true;
    }

    public bool IsGuildNameAllowed(string guildName)
    {
        if (guilds.Values.Any(g => g.name == guildName)) return false;
        if (guildName.Length is < 3 or > 32) return false;

        return true;
    }

    public bool CreateGuild(string guildName, IPlayer foundingPlayer)
    {
        // Guild exists with this name already.
        if (!IsGuildNameAllowed(guildName)) return false;

        if (GetPlayersGuilds(foundingPlayer.PlayerUID).Count > 10) return false; // Too many guilds.

        Guild guild = new(foundingPlayer, guildName, nextGuildId);
        nextGuildId++;
        guilds[guild.id] = guild;

        Random rand = new();
        Vector3 color = new(rand.NextSingle(), rand.NextSingle(), rand.NextSingle());
        color.X = MathF.Round(color.X, 2);
        color.Y = MathF.Round(color.Y, 2);
        color.Z = MathF.Round(color.Z, 2);
        guild.SetColor(color);

        GetPlayersGuilds(foundingPlayer.PlayerUID).Add(guild.id);

        return true;
    }

    public bool DisbandGuild(Guild guild)
    {
        foreach (MembershipInfo info in guild.MemberInfo)
        {
            GetPlayersGuilds(info.playerUid).Remove(guild.id);
        }

        foreach (string invite in guild.GetInvites())
        {
            GetPlayersInvites(invite).Remove(guild.id);
        }

        guilds.Remove(guild.id);

        return true;
    }

    public bool KickPlayer(string actingUid, string targetPlayerUid, Guild guild)
    {
        RoleInfo? actingRole = guild.GetRole(actingUid);
        RoleInfo? targetPlayerRole = guild.GetRole(targetPlayerUid);

        if (actingRole == null || targetPlayerRole == null) return false;
        if (targetPlayerRole.authority >= actingRole.authority || !actingRole.HasPermissions(GuildPerms.Kick)) return false;

        // Remove player from guild.
        return RemovePlayerFromGuild(targetPlayerUid, guild);
    }

    #endregion

    #region Roles

    public static bool AddRole(IPlayer player, Guild guild)
    {
        RoleInfo? role = guild.GetRole(player.PlayerUID);
        if (role == null) return false;

        if (!role.HasPermissions(GuildPerms.ManageRoles)) return false;

        guild.AddRole();

        return true;
    }

    public static bool RemoveRole(IPlayer player, Guild guild, int roleId)
    {
        RoleInfo? role = guild.GetRole(player.PlayerUID);
        if (role == null) return false;

        if (!role.HasPermissions(GuildPerms.ManageRoles)) return false;

        RoleInfo? targetRole = guild.GetRole(roleId);
        if (targetRole == null || targetRole.authority >= role.authority) return false;

        return guild.RemoveRole(roleId);
    }

    public bool UpdateRole(IPlayer player, RoleUpdatePacket packet)
    {
        if (packet.newName == null) return false;

        Guild? guild = GetGuild(packet.guildId);
        if (guild == null) return false;

        RoleInfo? role = guild.GetRole(player.PlayerUID);
        if (role == null) return false;

        if (!role.HasPermissions(GuildPerms.ManageRoles)) return false;

        if (packet.newAuthority >= role.authority) packet.newAuthority = role.authority - 1;

        RoleInfo? targetRole = guild.GetRole(packet.roleId);
        if (targetRole == null) return false;

        if (targetRole.authority >= role.authority) return false;

        targetRole.ChangeRole(packet);
        return true;
    }

    public static bool ChangeRole(string actingUid, string targetPlayerUid, Guild guild, int targetRole)
    {
        // Get acting player role.
        RoleInfo? actingRole = guild.GetRole(actingUid);
        RoleInfo? targetPlayerRole = guild.GetRole(targetPlayerUid);
        RoleInfo? role = guild.GetRole(targetRole);

        if (actingRole == null || targetPlayerRole == null || role == null) return false;

        if (!actingRole.HasPermissions(GuildPerms.Promote)) return false;
        if (targetPlayerRole.authority >= actingRole.authority || role.authority >= actingRole.authority) return false;

        guild.ChangePlayersRole(targetPlayerUid, role.id);

        return true;
    }

    #endregion

    #region Metrics

    public PlayerMetrics GetMetrics(IPlayer player)
    {
        if (!playerMetrics.TryGetValue(player.PlayerUID, out PlayerMetrics? metrics))
        {
            if (player is IClientPlayer clientPlayer)
            {
                metrics = new PlayerMetrics(clientPlayer);
            }
            else
            {
                metrics = new PlayerMetrics((IServerPlayer)player);
            }

            playerMetrics[player.PlayerUID] = metrics;
        }

        return metrics;
    }

    /// <summary>
    /// Get metrics on the client.
    /// </summary>
    public PlayerMetrics? GetMetrics(string playerUid)
    {
        if (!playerMetrics.TryGetValue(playerUid, out PlayerMetrics? metrics)) return null;
        return metrics;
    }

    #endregion
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
    [JsonProperty]
    public int reppedGuildId = -1;

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
    [ModuleInitializer]
    internal static void Init()
    {
        RuntimeTypeModel.Default.Add(typeof(Vector3), false)
            .Add("X")
            .Add("Y")
            .Add("Z");
    }

    [JsonProperty]
    public string name = "NaN";
    public void ChangeName(string name)
    {
        this.name = name;
    }

    [JsonProperty]
    public int id;

    [JsonProperty]
    private HashSet<string> invites = new();

    [JsonProperty]
    public RoleInfo[] roles;

    [JsonProperty]
    private Vector3 color;

    public Vector3 Color => color;

    public void SetColor(Vector3 color)
    {
        this.color = color;
    }

    /// <summary>
    /// Player uid to info about their membership.
    /// </summary>
    [JsonProperty]
    private readonly Dictionary<string, MembershipInfo> members = new();

    public int MemberCount => members.Count;

    /// <summary>
    /// Enumerate over every member.
    /// </summary>
    public IEnumerable<MembershipInfo> MemberInfo => members.Values;

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

    public Guild(IPlayer foundingPlayer, string name, int id)
    {
        this.name = name;
        this.id = id;

        // Initialize roles.
        // Roles 0 and 1 can't be deleted, they are the designated member and founder role. They can be altered.
        // People with role management permissions can't manage roles lower than their authority, and can't raise authority higher than theirs.
        roles = InitializeRoles();

        // Default founder to founder role.
        MembershipInfo membershipInfo = new(foundingPlayer.PlayerUID);
        members[foundingPlayer.PlayerUID] = membershipInfo;
        membershipInfo.roleId = 1;
    }

    /// <summary>
    /// Add a new empty role.
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

    public HashSet<string> GetInvites()
    {
        invites ??= new HashSet<string>();
        return invites;
    }

    public void AddMember(string playerUid)
    {
        members[playerUid] = new MembershipInfo(playerUid);
    }

    public void RemoveMember(string playerUid)
    {
        members.Remove(playerUid);
    }

    public void ChangePlayersRole(string playerUid, int roleId)
    {
        if (!members.TryGetValue(playerUid, out MembershipInfo? memberInfo)) return;
        memberInfo.roleId = roleId;
    }

    public Guild()
    {

    }
}