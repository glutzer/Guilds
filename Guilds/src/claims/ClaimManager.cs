using HarmonyLib;
using MareLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Guilds;

[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public struct GridPos2d : IEquatable<GridPos2d>
{
    [JsonProperty]
    public int X;

    [JsonProperty]
    public int Z;

    public GridPos2d()
    {

    }

    public GridPos2d(int x, int z)
    {
        X = x;
        Z = z;
    }

    public GridPos2d(BlockPos blockPos)
    {
        X = blockPos.X;
        Z = blockPos.Z;
    }

    public readonly bool Equals(GridPos2d other)
    {
        return X == other.X && Z == other.Z;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is GridPos i && Equals(i);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Z);
    }

    public static bool operator ==(GridPos2d left, GridPos2d right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridPos2d left, GridPos2d right)
    {
        return !(left == right);
    }

    public static GridPos2d operator +(GridPos2d left, GridPos2d right)
    {
        return new GridPos2d(left.X + right.X, left.Z + right.Z);
    }

    public static GridPos2d operator -(GridPos2d left, GridPos2d right)
    {
        return new GridPos2d(left.X - right.X, left.Z - right.Z);
    }

    public static GridPos2d operator *(GridPos2d left, int right)
    {
        return new GridPos2d(left.X * right, left.Z * right);
    }

    public static GridPos2d operator /(GridPos2d left, int right)
    {
        return new GridPos2d(left.X / right, left.Z / right);
    }

    public readonly IEnumerable<GridPos2d> Adjacents()
    {
        yield return new GridPos2d(X - 1, Z);
        yield return new GridPos2d(X + 1, Z);
        yield return new GridPos2d(X, Z - 1);
        yield return new GridPos2d(X, Z + 1);
    }
}

[JsonObject(MemberSerialization.OptIn)]
[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class ClaimData
{
    [JsonProperty]
    public Dictionary<GridPos2d, GuildClaim> guildClaims = new();

    public bool TryGetClaim(GridPos2d position, [NotNullWhen(true)] out GuildClaim claim)
    {
        return guildClaims.TryGetValue(position, out claim);
    }

    public void RemoveClaim(GridPos2d position)
    {
        guildClaims.Remove(position);

        foreach (GridPos2d adjacent in position.Adjacents())
        {
            GridPos2d offset = adjacent - position;

            if (guildClaims.TryGetValue(adjacent, out GuildClaim adjacentClaim))
            {
                // Claim adjacent, remove border flag from adjacent claim.

                adjacentClaim.borderFlags |= offset switch
                {
                    { X: -1 } => BorderFlags.East,
                    { X: 1 } => BorderFlags.West,
                    { Z: -1 } => BorderFlags.North,
                    { Z: 1 } => BorderFlags.South,
                    _ => 0
                };

                guildClaims[adjacent] = adjacentClaim;
            }
        }
    }

    /// <summary>
    /// Add a claim, on both the client or server.
    /// </summary>
    public void AddClaim(GridPos2d position, int guildId)
    {
        GuildClaim claim = new(position, guildId);

        foreach (GridPos2d adjacent in position.Adjacents())
        {
            GridPos2d offset = adjacent - position;

            if (guildClaims.TryGetValue(adjacent, out GuildClaim adjacentClaim))
            {
                // If a claim is adjacent on this face, remove the flag.
                if (adjacentClaim.guildId == claim.guildId)
                {
                    adjacentClaim.borderFlags &= offset switch
                    {
                        { X: -1 } => ~BorderFlags.East,
                        { X: 1 } => ~BorderFlags.West,
                        { Z: -1 } => ~BorderFlags.North,
                        { Z: 1 } => ~BorderFlags.South,
                        _ => 0
                    };

                    guildClaims[adjacent] = adjacentClaim;
                }
                else
                {
                    claim.borderFlags |= offset switch
                    {
                        { X: -1 } => BorderFlags.West,
                        { X: 1 } => BorderFlags.East,
                        { Z: -1 } => BorderFlags.South,
                        { Z: 1 } => BorderFlags.North,
                        _ => 0
                    };
                }
            }
            else
            {
                // No claim adjacent, add border flag to this one.
                claim.borderFlags |= offset switch
                {
                    { X: -1 } => BorderFlags.West,
                    { X: 1 } => BorderFlags.East,
                    { Z: -1 } => BorderFlags.South,
                    { Z: 1 } => BorderFlags.North,
                    _ => 0
                };
            }
        }

        guildClaims[position] = claim;
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public class ClaimPacket
{
    public GridPos2d position;
    public bool removed;
    public int guildId;
}

[GameSystem]
public class ClaimManager : NetworkedGameSystem
{
    public ClaimData claimData = new();
    public GuildManager guildManager = null!;

    public static Harmony? Harmony { get; set; }

    public Action? onGuildColorsChanged;
    public Action<GuildClaim>? onClaimAdded;
    public Action<GridPos2d>? onClaimRemoved;

    public ClaimManager(bool isServer, ICoreAPI api) : base(isServer, api, "guildclaims")
    {
        if (Harmony == null)
        {
            Harmony = new Harmony("guilds");
            Harmony.PatchAll();
        }
    }

    public void AddClaimServer(GridPos2d position, int guildId)
    {
        claimData.AddClaim(position, guildId);
        BroadcastPacket(new ClaimPacket() { position = position, guildId = guildId });
        onClaimAdded?.Invoke(claimData.guildClaims[position]);
    }

    public void RemoveClaimServer(GridPos2d position)
    {
        claimData.RemoveClaim(position);
        BroadcastPacket(new ClaimPacket() { position = position, removed = true });
        onClaimRemoved?.Invoke(position);
    }

    public override void OnStart()
    {
        guildManager = MainAPI.GetGameSystem<GuildManager>(api.Side);

        if (isServer)
        {
            LoadSaveData();
            MainAPI.Sapi.Event.GameWorldSave += OnSave;

            MainAPI.Sapi.Event.PlayerJoin += player =>
            {
                SendPacket(claimData, player); // Sync full data.
            };
        }
        else
        {
            api.ModLoader.GetModSystem<WorldMapManager>().RegisterMapLayer<GuildClaimMapLayer>("guildclaims", 0.8f);

            MareShaderRegistry.AddShader("guilds:claimgui", "guilds:claimgui", "claimgui");
        }
    }

    /// <summary>
    /// Client may send a claim packet to request a claim for his currently repped guild.
    /// </summary>
    public void ServerReceivedClaimPacket(IServerPlayer player, ClaimPacket packet)
    {
        PlayerMetrics metrics = guildManager.guildData.GetMetrics(player);
        Guild? reppedGuild = guildManager.guildData.GetGuild(metrics.reppedGuildId);
        if (reppedGuild == null) return;

        RoleInfo? role = reppedGuild.GetRole(player.PlayerUID);
        if (role == null || !role.HasPermissions(GuildPerms.ManageClaims)) return;

        packet.guildId = reppedGuild.Id;

        if (packet.removed)
        {
            if (claimData.TryGetClaim(packet.position, out GuildClaim claim))
            {
                if (claim.guildId != reppedGuild.Id) return;
                claimData.RemoveClaim(packet.position);
                BroadcastPacket(packet);
            }
        }
        else if (!claimData.TryGetClaim(packet.position, out GuildClaim _))
        {
            claimData.AddClaim(packet.position, reppedGuild.Id);
            BroadcastPacket(packet);
        }
    }

    public void OnGuildDisbanded(int guildId)
    {
        // Remove all claims that guild had.
        List<GridPos2d> positions = claimData.guildClaims.Values.Where(c => c.guildId == guildId).Select(c => c.position).ToList();
        ClaimPacket packet = new()
        {
            removed = true,
            guildId = guildId
        };

        foreach (GridPos2d position in positions)
        {
            claimData.RemoveClaim(position);
            packet.position = position;
            BroadcastPacket(packet);
        }
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.RegisterMessageType<ClaimData>();
        channel.RegisterMessageType<ClaimPacket>();

        channel.SetMessageHandler<ClaimData>((c) =>
        {
            claimData = c;
        });

        channel.SetMessageHandler<ClaimPacket>((c) =>
        {
            if (c.removed)
            {
                if (claimData.TryGetClaim(c.position, out GuildClaim claim))
                {
                    claimData.RemoveClaim(c.position);
                    onClaimRemoved?.Invoke(claim.position);
                }
            }
            else
            {
                claimData.AddClaim(c.position, c.guildId);
                onClaimAdded?.Invoke(claimData.guildClaims[c.position]);
            }
        });
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {
        channel.RegisterMessageType<ClaimData>();
        channel.RegisterMessageType<ClaimPacket>();

        channel.SetMessageHandler<ClaimPacket>(ServerReceivedClaimPacket);
    }

    public void LoadSaveData()
    {
        string guildFile = Path.Combine(GamePaths.DataPath, "guildclaims.json");
        if (File.Exists(guildFile))
        {
            try
            {
                // Load from list.
                string text = File.ReadAllText(guildFile);
                List<GuildClaim> list = JsonConvert.DeserializeObject<List<GuildClaim>>(text)!;
                if (list?.Count > 0)
                {
                    claimData.guildClaims = list.ToDictionary(i => i.position, i => i);
                }
            }
            catch
            {
                claimData = new();
            }
        }

        claimData ??= new ClaimData();
    }

    private void OnSave()
    {
        // Save a list of key value pairs instead for json.
        List<GuildClaim> list = claimData.guildClaims.Values.ToList();

        JsonSerializerSettings settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        string guildFile = Path.Combine(GamePaths.DataPath, "guildclaims.json");
        File.WriteAllText(guildFile, JsonConvert.SerializeObject(list, settings));
    }

    public override void OnClose()
    {
        if (Harmony != null)
        {
            Harmony.UnpatchAll();
            Harmony = null;
        }
    }
}