using MareLib;
using OpenTK.Mathematics;
using System;

namespace Guilds;

/// <summary>
/// Button that may be toggled, has delegate for up or down.
/// </summary>
public class WidgetToggleableButton : WidgetBaseToggleableButton
{
    private readonly NineSliceTexture texture;
    private readonly TextObject textObj;
    private Vector4 color;

    public WidgetToggleableButton(Widget? parent, Action<bool> onClick, string text, bool lockedDown = true) : base(parent, onClick, !lockedDown)
    {
        texture = GuiThemes.Title;
        textObj = new TextObject(text, GuiThemes.Font, 50, GuiThemes.TextColor)
        {
            Shadow = true
        };

        OnResize += () =>
        {
            textObj.SetScaleFromWidget(this, 0.9f, 0.5f);
        };

        color = GuiThemes.ButtonColor;

        this.onClick += (up) =>
        {
            MainAPI.Capi.Gui.PlaySound("tick");
        };
    }

    /// <summary>
    /// Sets the button to be held down without activating events.
    /// </summary>
    public void LockDown()
    {
        state = EnumButtonState.Active;
    }

    public override void OnRender(float dt, MareShader shader)
    {
        Vector4 c = color;
        Vector4 f = GuiThemes.TextColor;

        if (state == EnumButtonState.Active)
        {
            c.Xyz *= 0.6f;
            f.Xyz *= 0.6f;
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