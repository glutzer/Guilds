using MareLib;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace Guilds;

public struct RenderableTile
{
    public GridPos2d position;
    public int guildId;
    public BorderFlags borderFlags;
    public Vector3 color;
}

public class GuildClaimMapLayer : MapLayer
{
    public override string Title => "Guild Claims";
    public override string LayerGroupCode => "guildclaims";
    public override EnumMapAppSide DataSide => EnumMapAppSide.Client;
    public ClaimManager claimManager;
    public GuildManager guildManager;

    public Texture blank;

    private GuiDialogWorldMap? mapDialog;

    public GuildClaimMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
    {
        claimManager = MainAPI.GetGameSystem<ClaimManager>(EnumAppSide.Client);
        guildManager = MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client);
        blank = GuiThemes.Blank;

        claimManager.onGuildColorsChanged += OnGuildColorChanged;
        claimManager.onClaimAdded += OnTileAdded;
        claimManager.onClaimRemoved += OnTileRemoved;
    }

    public override void Dispose()
    {
        base.Dispose();

        claimManager.onGuildColorsChanged -= OnGuildColorChanged;
        claimManager.onClaimAdded -= OnTileAdded;
        claimManager.onClaimRemoved -= OnTileRemoved;
    }

    private GridPos2d mousedPos;

    private readonly Dictionary<GridPos2d, RenderableTile> tiles = new();

    /// <summary>
    /// Update all tiles within bounds.
    /// </summary>
    public void OnGuildColorChanged()
    {
        // Get ref to each value in tiles.
        foreach (RenderableTile t in tiles.Values.ToArray())
        {
            RenderableTile tile = t;
            Guild? guild = guildManager.guildData.GetGuild(tile.guildId);
            if (guild == null) continue;
            tile.color = guild.Color;
            tiles[tile.position] = tile;
        }
    }

    public void OnTileAdded(GuildClaim claim)
    {
        if (lastMap != null)
        {
            // Prevent adding off-screen tiles but not entirely sure how it works.
            Vector2 screenPos = TranslateChunkPosToViewPos(claim.position, lastMap);
            if (screenPos.X < -64 || screenPos.Y < -64 || screenPos.X > MainAPI.RenderWidth + (64 * lastMap.ZoomLevel) || screenPos.Y > MainAPI.RenderWidth + (64 * lastMap.ZoomLevel)) return;
        }

        Guild? guild = guildManager.guildData.GetGuild(claim.guildId);
        if (guild != null)
        {
            tiles[claim.position] = new RenderableTile()
            {
                position = claim.position,
                guildId = claim.guildId,
                borderFlags = claim.borderFlags,
                color = guild.Color
            };
        }

        foreach (GridPos2d pos in claim.position.Adjacents())
        {
            if (claimManager.claimData.TryGetClaim(pos, out GuildClaim adjClaim))
            {
                guild = guildManager.guildData.GetGuild(adjClaim.guildId);
                if (guild == null) continue;

                tiles[pos] = new RenderableTile()
                {
                    position = adjClaim.position,
                    guildId = adjClaim.guildId,
                    borderFlags = adjClaim.borderFlags,
                    color = guild.Color
                };
            }
        }
    }

    public void OnTileRemoved(GridPos2d claim)
    {
        if (!tiles.Remove(claim)) return;

        foreach (GridPos2d pos in claim.Adjacents())
        {
            if (claimManager.claimData.TryGetClaim(pos, out GuildClaim adjClaim))
            {
                Guild? guild = guildManager.guildData.GetGuild(adjClaim.guildId);
                if (guild == null) continue;

                tiles[pos] = new RenderableTile()
                {
                    position = adjClaim.position,
                    guildId = adjClaim.guildId,
                    borderFlags = adjClaim.borderFlags,
                    color = guild.Color
                };
            }
        }
    }

    public override void OnViewChangedClient(List<Vec2i> nowVisible, List<Vec2i> nowHidden)
    {
        foreach (Vec2i pos in nowVisible)
        {
            if (claimManager.claimData.TryGetClaim(new GridPos2d(pos.X, pos.Y), out GuildClaim claim))
            {
                OnTileAdded(claim);
            }
        }
        foreach (Vec2i pos in nowHidden)
        {
            OnTileRemoved(new GridPos2d(pos.X, pos.Y));
        }
    }

    private GuiElementMap? lastMap;

    public override void Render(GuiElementMap mapElem, float dt)
    {
        if (!Active) return;

        lastMap = mapElem;

        mapDialog ??= mapElem.GetField<GuiDialogWorldMap>("worldmapdlg");

        ShaderProgramBase? currentShader = ShaderProgramBase.CurrentShaderProgram;

        MareShader guiShader = MareShaderRegistry.Get("claimgui");
        guiShader.Use();
        guiShader.BindTexture(blank, "tex2d");

        RenderTools.DisableDepthTest();

        foreach (RenderableTile claim in tiles.Values)
        {
            GridPos2d pos = claim.position * 32;
            if (pos.X < mapElem.CurrentBlockViewBounds.X1 - 256 || pos.X > mapElem.CurrentBlockViewBounds.X2 + 256 || pos.Z < mapElem.CurrentBlockViewBounds.Z1 - 256 || pos.Z > mapElem.CurrentBlockViewBounds.Z2 + 256) continue;

            Vector2 screenPos = TranslateChunkPosToViewPos(claim.position, mapElem);

            guiShader.Uniform("sideMask", (int)claim.borderFlags);
            guiShader.Uniform("color", new Vector4(claim.color, 0.5f));
            RenderTools.RenderQuad(guiShader, screenPos.X, screenPos.Y, 32 * mapElem.ZoomLevel, 32 * mapElem.ZoomLevel);
        }

        guiShader.Uniform("sideMask", 0);

        if (mapDialog.DialogType != EnumDialogType.HUD)
        {
            Vector2 mPos = TranslateChunkPosToViewPos(mousedPos, mapElem);

            if (32 * mapElem.ZoomLevel > 24)
            {
                guiShader.Uniform("color", MainAPI.Capi.World.Player.Entity.Controls.ShiftKey ? new Vector4(0.9f, 0.4f, 0.4f, 0.6f) : new Vector4(0.4f, 0.9f, 0.4f, 0.6f));
                RenderTools.RenderNineSlice(GuiThemes.Button, guiShader, mPos.X, mPos.Y, 32 * mapElem.ZoomLevel, 32 * mapElem.ZoomLevel);
            }
            else
            {
                guiShader.Uniform("color", MainAPI.Capi.World.Player.Entity.Controls.ShiftKey ? new Vector4(0.9f, 0.4f, 0.4f, 0.3f) : new Vector4(0.4f, 0.9f, 0.4f, 0.3f));
                guiShader.Uniform("sideMask", 15);
                RenderTools.RenderQuad(guiShader, mPos.X, mPos.Y, 32 * mapElem.ZoomLevel, 32 * mapElem.ZoomLevel);
            }
        }

        guiShader.Uniform("color", Vector4.One);

        RenderTools.EnableDepthTest();
        currentShader?.Use();
    }

    public static Vector2 TranslateChunkPosToViewPos(GridPos2d gridPos, GuiElementMap mapElem)
    {
        double num = mapElem.CurrentBlockViewBounds.X2 - mapElem.CurrentBlockViewBounds.X1;
        double num2 = mapElem.CurrentBlockViewBounds.Z2 - mapElem.CurrentBlockViewBounds.Z1;

        return new Vector2(
            (float)mapElem.Bounds.renderX + ((float)(((gridPos.X * 32) - mapElem.CurrentBlockViewBounds.X1) / num * mapElem.Bounds.InnerWidth)),
            (float)mapElem.Bounds.renderY + ((float)(((gridPos.Z * 32) - mapElem.CurrentBlockViewBounds.Z1) / num2 * mapElem.Bounds.InnerHeight))
            );
    }

    public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
    {
        Vec3d worldPos = new();
        float mouseX = (float)(args.X - mapElem.Bounds.renderX);
        float mouseY = (float)(args.Y - mapElem.Bounds.renderY);
        mapElem.TranslateViewPosToWorldPos(new Vec2f(mouseX, mouseY), ref worldPos);

        // Set to chunk coordinate.
        worldPos.X = (int)worldPos.X / 32 * 32;
        worldPos.Z = (int)worldPos.Z / 32 * 32;

        mousedPos = new((int)worldPos.X / 32, (int)worldPos.Z / 32);

        if (claimManager.claimData.TryGetClaim(mousedPos, out GuildClaim claim))
        {
            Guild? guild = guildManager.guildData.GetGuild(claim.guildId);
            if (guild == null) return;

            hoverText.AppendLine($"Claimed by {guild.name}");
        }
    }

    public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
    {
        if (args.Button != EnumMouseButton.Middle || !Active) return;

        args.Handled = true;

        ClaimPacket packet = new()
        {
            position = mousedPos
        };

        if (MainAPI.Capi.World.Player.Entity.Controls.ShiftKey)
        {
            packet.removed = true;
        }

        claimManager.SendPacket(packet);
    }
}