using MareLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Guilds;

[ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
public struct GridPos2d : IEquatable<GridPos2d>
{
    public int X;
    public int Z;

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
}

public class ClaimManager : NetworkedGameSystem
{
    private Dictionary<GridPos2d, GuildClaim> guildClaims = new();

    public ClaimManager(bool isServer, ICoreAPI api) : base(isServer, api, "guildclaims")
    {
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {

    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {

    }
}