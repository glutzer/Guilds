using MareLib;
using OpenTK.Mathematics;
using System;

namespace Guilds;

/// <summary>
/// Button that has a pre-defined texture for guilds and adjusts font scale to text.
/// Font color is opposite the button color for readability.
/// </summary>
public class WidgetGuildButton : WidgetBaseButton
{
    private readonly TextObject textObj;
    private Vector4 color;
    private Vector4 fontColor;

    private readonly NineSliceTexture texture;

    public WidgetGuildButton(Widget? parent, Action onClick, string text, Vector4 color, Vector4 fontColor) : base(parent, onClick)
    {
        this.color = color;
        this.fontColor = fontColor;

        textObj = new TextObject(text, FontRegistry.GetFont("friz"), 50, fontColor);

        // Fit text object into the button.
        OnResize += () =>
        {
            textObj.SetScaleFromWidget(this, 0.9f, 0.5f);
        };

        this.onClick += () =>
        {
            MainAPI.Capi.Gui.PlaySound("tick");
        };

        texture = GuiThemes.Button;
    }

    public override void OnRender(float dt, MareShader shader)
    {
        Vector4 c = color;
        Vector4 f = fontColor;

        if (state == EnumButtonState.Active)
        {
            c.Xyz *= 0.8f;
            f.Xyz *= 0.8f;
            shader.Uniform("color", c);
            RenderTools.RenderNineSlice(texture, shader, X, Y, Width, Height);
        }

        if (state == EnumButtonState.Hovered)
        {
            c.Xyz *= 1.2f;
            f.Xyz *= 1.2f;
            shader.Uniform("color", c);
            RenderTools.RenderNineSlice(texture, shader, X, Y, Width, Height);
        }

        if (state == EnumButtonState.Normal)
        {
            shader.Uniform("color", c);
            RenderTools.RenderNineSlice(texture, shader, X, Y, Width, Height);
        }

        textObj.color = f;
        textObj.RenderCenteredLine(XCenter, YCenter, shader, true);
    }
}