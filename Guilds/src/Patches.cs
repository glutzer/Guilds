using HarmonyLib;
using MareLib;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.Server;

namespace Guilds;

public class Patches
{
    [HarmonyPatch(typeof(CmdLand))]
    [HarmonyPatch("acquireClaimInProgress")]
    public static class AcquireClaimInProgressPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref TextCommandResult __result)
        {
            __result = TextCommandResult.Error("Guilds have disabled claiming.");
            return false;
        }
    }

    // Rewrite this to make player unable to touch claimed chunks instead.
    [HarmonyPatch(typeof(WorldMap))]
    [HarmonyPatch("GetBlockingLandClaimant")]
    public static class ClaimPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result, WorldMap __instance, IPlayer forPlayer, BlockPos pos, EnumBlockAccessFlags accessFlag)
        {
            if (forPlayer == null) return false;

            long key = __instance.MapRegionIndex2D(pos.X / __instance.RegionSize, pos.Z / __instance.RegionSize);

            if (__instance.LandClaimByRegion.TryGetValue(key, out List<LandClaim>? claims))
            {
                foreach (LandClaim item in claims)
                {
                    if (item.PositionInside(pos) && (item.TestPlayerAccess(forPlayer, accessFlag) == EnumPlayerAccessResult.Denied) && (!item.AllowUseEveryone || accessFlag != EnumBlockAccessFlags.Use))
                    {
                        __result = item.LastKnownOwnerName;
                        return false;
                    }
                }
            }

            // Now check guild claims.
            GridPos2d chunkPos = new(pos.X / 32, pos.Z / 32);

            if (forPlayer.Entity.Api == null) return false;
            ClaimManager claimManager = MainAPI.GetGameSystem<ClaimManager>(forPlayer.Entity.Api.Side);

            if (claimManager.claimData.TryGetClaim(chunkPos, out GuildClaim claim))
            {
                Guild? guild = claimManager.guildManager.guildData.GetGuild(claim.guildId);
                if (guild == null) return false;

                RoleInfo? roleInfo = guild.GetRole(forPlayer.PlayerUID);
                if (roleInfo == null)
                {
                    __result = $"guild {guild.name}";
                    return false;
                }

                if (accessFlag == EnumBlockAccessFlags.Use && !roleInfo.HasPermissions(GuildPerms.UseBlocks))
                {
                    __result = $"{guild.name} has not granted use permissions";
                    return false;
                }

                if (accessFlag == EnumBlockAccessFlags.BuildOrBreak && !roleInfo.HasPermissions(GuildPerms.BreakBlocks))
                {
                    __result = $"{guild.name} has not granted build permissions";
                    return false;
                }
            }

            return false;
        }
    }
}