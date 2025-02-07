using MareLib;
using OpenTK.Mathematics;
using System;

namespace Guilds;

/// <summary>
/// Button as a tab.
/// </summary>
public class WidgetGuildTab : WidgetBaseToggleableButton
{
    private readonly Texture tab;
    private readonly bool flip;
    private float accum;
    private Vector4 color;

    private readonly TextObject textObj;

    public override int SortPriority => -1;

    public WidgetGuildTab(Widget? parent, Action<bool> onClick, bool flip, Vector4 color, string tabName, bool allowRelease = false) : base(parent, onClick, allowRelease)
    {
        tab = GuiThemes.Tab;
        this.flip = flip;
        this.color = color;
        textObj = new TextObject(tabName, FontRegistry.GetFont("friz"), 50, Vector4.One);

        OnResize += () =>
        {
            textObj.SetScaleFromWidget(this, 0.9f, 0.7f);
        };

        this.onClick += (on) =>
        {
            MainAPI.Capi.Gui.PlaySound("tick");
        };
    }

    public void SetDown()
    {
        state = EnumButtonState.Active;
    }

    public override void OnRender(float dt, MareShader shader)
    {
        Vector4 f = Vector4.One;

        if (state != EnumButtonState.Normal)
        {
            accum += dt * 2;
        }
        else
        {
            accum -= dt * 2;
        }

        accum = Math.Clamp(accum, 0, 1);
        shader.BindTexture(tab, "tex2d");

        Vector4 c = color;

        if (state == EnumButtonState.Hovered || state == EnumButtonState.Active)
        {
            c.Xyz *= 1.2f;
            f.Xyz *= 1.2f;
        }

        shader.Uniform("color", c);

        if (flip)
        {
            RenderTools.RenderQuad(shader, X + Width, Y, -Width - (accum * Width), Height);
            textObj.RenderLeftAlignedLine(X + Width - (Width * 0.05f), Y + (Height / 2), shader, true);
        }
        else
        {
            RenderTools.RenderQuad(shader, X, Y, Width + (accum * Width), Height);
            textObj.RenderLine(X + (Width * 0.05f), Y + (Height / 2), shader, 0, true);
        }

        shader.Uniform("color", f);
    }
}