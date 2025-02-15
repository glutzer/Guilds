using MareLib;
using OpenTK.Mathematics;

namespace Guilds;

public class WidgetGuildScrollBar : WidgetBaseScrollBar
{
    public NineSliceTexture background;
    public NineSliceTexture cursorTex;
    private Vector4 color;

    public WidgetGuildScrollBar(Widget? parent, Widget scrollWidget, int stepsPerPage = 10) : base(parent, scrollWidget, stepsPerPage)
    {
        background = GuiThemes.ScrollBar;
        cursorTex = GuiThemes.Button;
        color = GuiThemes.ButtonColor;
    }

    protected override void RenderBackground(int x, int y, int width, int height, MareShader shader)
    {
        Vector4 c = color;
        c.Xyz *= 0.5f;
        shader.Uniform("color", c);
        RenderTools.RenderNineSlice(background, shader, x, y, width, height);
        shader.Uniform("color", Vector4.One);
    }

    protected override void RenderCursor(int x, int y, int width, int height, MareShader shader, EnumButtonState barState)
    {
        Vector4 c = color;

        if (barState == EnumButtonState.Active)
        {
            c.Xyz *= 0.8f;
        }

        if (barState == EnumButtonState.Hovered)
        {
            c.Xyz *= 1.2f;
        }

        shader.Uniform("color", c);
        RenderTools.RenderNineSlice(cursorTex, shader, x, y, width, height);
        shader.Uniform("color", Vector4.One);
    }
}