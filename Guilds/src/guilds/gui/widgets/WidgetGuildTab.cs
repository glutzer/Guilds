using OpenTK.Mathematics;

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

    public WidgetGuildTab(Widget? parent, Gui gui, Action<bool> onToggle, bool flip, Vector4 color, string tabName, bool allowRelease = false) : base(parent, gui, onToggle, false, allowRelease)
    {
        tab = GuiThemes.Tab;
        this.flip = flip;
        this.color = color;
        textObj = new TextObject(tabName, VanillaThemes.Font, 50, VanillaThemes.WhitishTextColor)
        {
            Shadow = true
        };

        OnResize += () =>
        {
            textObj.SetScaleFromWidget(this, 0.9f, 0.7f);
        };

        onToggle += (on) =>
        {
            MainAPI.Capi.Gui.PlaySound("tick");
        };
    }

    public void SetDown()
    {
        enabled = true;
        accum = 1f;
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        Vector4 f = Vector4.One;

        if (state != EnumButtonState.Normal || enabled)
        {
            accum += dt * 2f;
        }
        else
        {
            accum -= dt * 2f;
        }

        accum = Math.Clamp(accum, 0f, 1f);
        shader.BindTexture(tab, "tex2d");

        Vector4 c = color;

        if (state is EnumButtonState.Hovered or EnumButtonState.Active || enabled)
        {
            c.Xyz *= 1.2f;
            f.Xyz *= 1.2f;
        }

        shader.Color = c;

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

        shader.ResetColor();
    }
}