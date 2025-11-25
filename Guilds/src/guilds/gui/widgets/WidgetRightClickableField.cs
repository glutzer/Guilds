using OpenTK.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Guilds;

/// <summary>
/// Field that can be right clicked.
/// </summary>
public class WidgetRightClickableField : WidgetBaseButton
{
    private readonly NineSliceTexture tex = VanillaThemes.OutsetTexture;
    private readonly TextObject text;

    public WidgetRightClickableField(Widget? parent, Gui gui, Action onClick, string text) : base(parent, gui, onClick)
    {
        this.text = new TextObject(text, VanillaThemes.Font, 50, VanillaThemes.WhitishTextColor)
        {
            Shadow = true
        };

        OnResize += () =>
        {
            this.text.SetScaleFromWidget(this, 0.9f, 0.5f);
        };
    }

    protected override void GuiEvents_MouseDown(MouseEvent obj)
    {
        if (obj.Button != EnumMouseButton.Right) return;
        base.GuiEvents_MouseDown(obj);
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        if (!RenderTools.IsPointInsideScissor(X, Y)) return;

        shader.Uniform("color", state != EnumButtonState.Normal ? new Vector4(0.8f, 0.8f, 0.8f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f));
        RenderTools.RenderNineSlice(tex, shader, X, Y, Width, Height);
        shader.Uniform("color", Vector4.One);

        text.RenderCenteredLine(XCenter, YCenter, shader, true);
    }
}