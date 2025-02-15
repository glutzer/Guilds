using OpenTK.Mathematics;
using ProtoBuf;
using Vintagestory.API.Util;

namespace Guilds;

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
    UpdateRole,

    Create,
    Leave,
    Disband,
    UpdateInfo,

    RepGuild
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class RoleUpdateInfo
{
    public string? newName;
    public int newAuthority;
    public GuildPerms newPerms;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildUpdateInfo
{
    public string? name;
    public Vector3 color;
}

/// <summary>
/// Send from client -> server for a request.
/// Broadcasted back to clients.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class GuildPacket
{
    public EnumGuildPacket type;
    public string? targetPlayer;
    public int roleId;
    public int guildId;
    public byte[]? data;

    /// <summary>
    /// Only set on server when sending back to clients.
    /// </summary>
    public string? fromUid;

    public static GuildPacket Create<T>(EnumGuildPacket type, T data, string? targetPlayer = null, int guildId = 0, int roleId = 0)
    {
        return new GuildPacket { type = type, targetPlayer = targetPlayer, roleId = roleId, guildId = guildId, data = SerializerUtil.Serialize(data) };
    }

    public static GuildPacket Create(EnumGuildPacket type, string? targetPlayer = null, int guildId = 0, int roleId = 0, byte[]? data = null)
    {
        return new GuildPacket { type = type, targetPlayer = targetPlayer, roleId = roleId, guildId = guildId, data = data };
    }

    public T? ReadData<T>()
    {
        if (data == null) return default;
        return SerializerUtil.Deserialize<T>(data);
    }
}