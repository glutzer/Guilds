using MareLib;
using OpenTK.Mathematics;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Guilds;

/// <summary>
/// Field that can be right clicked
/// </summary>
public class WidgetRightClickableField : WidgetBaseButton
{
    private readonly NineSliceTexture tex = GuiThemes.Title;
    private readonly TextObject text;

    public WidgetRightClickableField(Widget? parent, Action onClick, string text) : base(parent, onClick)
    {
        this.text = new TextObject(text, FontRegistry.GetFont("friz"), 50, Vector4.One);

        OnResize += () =>
        {
            this.text.SetScaleFromWidget(this, 0.9f, 0.5f);
        };
    }

    protected override void GuiEvents_MouseDown(MouseEvent obj)
    {
        if (obj.Button == EnumMouseButton.Left) return;
        base.GuiEvents_MouseDown(obj);
    }

    public override void OnRender(float dt, MareShader shader)
    {
        if (!RenderTools.IsPointInsideScissor(X, Y)) return;

        shader.Uniform("color", state != EnumButtonState.Normal ? new Vector4(0.15f, 0.15f, 0.15f, 1) : new Vector4(0.1f, 0.1f, 0.1f, 1));
        RenderTools.RenderNineSlice(tex, shader, X, Y, Width, Height);
        shader.Uniform("color", Vector4.One);

        text.RenderCenteredLine(XCenter, YCenter, shader, true);
    }
}