using MareLib;
using OpenTK.Mathematics;

namespace Guilds;

/// <summary>
/// Fades out and removes itself after a set time. Sets fixed width to text length.
/// </summary>
public class WidgetFadingText : Widget
{
    private const float FADE_TIME = 5f;
    private float age;
    private readonly Texture texture;
    public readonly TextObject text;
    private readonly bool left;

    public WidgetFadingText(Widget? parent, string label, int fontScale, bool left, Vector3 notificationColor) : base(parent)
    {
        texture = GuiThemes.Blank;
        text = new TextObject(label, GuiThemes.Font, fontScale, new Vector4(notificationColor, 1f))
        {
            Shadow = true
        };

        NoScaling();

        this.left = left;

        int pixelWidth = text.PixelLength;
        int pixelHeight = (int)(text.font.LineHeight * fontScale);

        FixedSize(pixelWidth, pixelHeight);
    }

    public override void OnRender(float dt, MareShader shader)
    {
        age += dt;

        if (age > FADE_TIME)
        {
            RemoveSelf();
            return;
        }

        shader.Uniform("color", new Vector4(0, 0, 0, (1 - (age / FADE_TIME)) * 0.5f));
        shader.BindTexture(texture, "tex2d");

        RenderTools.RenderQuad(shader, X, Y, Width, Height);

        shader.Uniform("color", Vector4.One);

        text.color.W = 1 - (age / FADE_TIME);

        if (left)
        {
            text.RenderLine(X, Y + (Height / 2), shader, 0, true);
        }
        else
        {
            text.RenderLeftAlignedLine(X + Width, Y + (Height / 2), shader, true);
        }
    }
}