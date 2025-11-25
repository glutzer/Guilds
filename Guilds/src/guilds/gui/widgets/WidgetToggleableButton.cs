using OpenTK.Mathematics;

namespace Guilds;

/// <summary>
/// Button that may be toggled, has delegate for up or down.
/// </summary>
public class WidgetToggleableButton : WidgetBaseToggleableButton
{
    private readonly TextObject textObj;
    private Vector4 color;

    public WidgetToggleableButton(Widget? parent, Gui gui, Action<bool> onClick, string text, bool lockedDown = true) : base(parent, gui, onClick, false, !lockedDown)
    {
        textObj = new TextObject(text, VanillaThemes.Font, 50, VanillaThemes.WhitishTextColor)
        {
            Shadow = true
        };

        OnResize += () =>
        {
            textObj.SetScaleFromWidget(this, 0.9f, 0.5f);
        };

        color = Vector4.One;

        onClick += (up) =>
        {
            MainAPI.Capi.Gui.PlaySound("tick");
        };
    }

    /// <summary>
    /// Sets the button to be held down without activating events.
    /// </summary>
    public void LockDown()
    {
        enabled = true;
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        NineSliceTexture tex = enabled ? VanillaThemes.InsetTexture : VanillaThemes.OutsetTexture;

        Vector4 c = color;
        Vector4 f = VanillaThemes.WhitishTextColor;

        if (state == EnumButtonState.Active)
        {
            c.Xyz *= 0.6f;
            f.Xyz *= 0.6f;
            shader.Uniform("color", c);
            RenderTools.RenderNineSlice(tex, shader, X, Y, Width, Height);
        }

        if (state == EnumButtonState.Hovered)
        {
            c.Xyz *= 1.2f;
            f.Xyz *= 1.2f;
            shader.Uniform("color", c);
            RenderTools.RenderNineSlice(tex, shader, X, Y, Width, Height);
        }

        if (state == EnumButtonState.Normal)
        {
            shader.Uniform("color", c);
            RenderTools.RenderNineSlice(tex, shader, X, Y, Width, Height);
        }

        textObj.color = f;
        textObj.RenderCenteredLine(XCenter, YCenter, shader, true);

        shader.ResetColor();
    }

    protected override void OnMousedOver()
    {
        MainAPI.Capi.Gui.PlaySound("menubutton");
    }

    protected override void OnClicked()
    {
        MainAPI.Capi.Gui.PlaySound("menubutton_press");
    }
}