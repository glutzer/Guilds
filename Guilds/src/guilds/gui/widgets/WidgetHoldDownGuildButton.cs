using OpenTK.Mathematics;
using Vintagestory.API.Client;

namespace Guilds;

public class WidgetHoldDownGuildButton : Widget
{
    protected EnumButtonState state = EnumButtonState.Normal;
    protected Action onClick;

    private float accum;
    private readonly float time;

    private readonly NineSliceTexture texture;
    protected Vector4 fontColor;

    protected TextObject text;

    public WidgetHoldDownGuildButton(Widget? parent, Gui gui, float time, Action onClick, string text) : base(parent, gui)
    {
        fontColor = VanillaThemes.WhitishTextColor;

        this.time = time;
        this.onClick = onClick;
        texture = VanillaThemes.OutsetTexture;

        this.text = new TextObject(text, VanillaThemes.Font, 50, fontColor)
        {
            Shadow = true
        };

        OnResize += () =>
        {
            this.text.SetScaleFromWidget(this, 0.9f, 0.5f);
        };
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        guiEvents.MouseMove += GuiEvents_MouseMove;
        guiEvents.MouseDown += GuiEvents_MouseDown;
        guiEvents.MouseUp += GuiEvents_MouseUp;
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        if (state != EnumButtonState.Active)
        {
            accum -= dt;
        }
        else if (accum < time)
        {
            accum += dt;
            if (accum >= time)
            {
                MainAPI.Capi.Event.EnqueueMainThreadTask(() =>
                {
                    onClick();
                }, "button");
            }
        }

        accum = Math.Clamp(accum, 0f, time);

        NuttyShader barShader = NuttyShaderRegistry.Get("bargui");
        barShader.Use();
        barShader.Uniform("progress", accum / time);

        Vector4 c = Vector4.One;
        Vector4 f = fontColor;

        if (state == EnumButtonState.Active)
        {
            c.Xyz *= 0.8f;
            f.Xyz *= 0.8f;
            barShader.Uniform("color", c);
            RenderTools.RenderNineSlice(texture, barShader, X, Y, Width, Height);
        }

        if (state == EnumButtonState.Hovered)
        {
            c.Xyz *= 1.2f;
            f.Xyz *= 1.2f;
            barShader.Uniform("color", c);
            RenderTools.RenderNineSlice(texture, barShader, X, Y, Width, Height);
        }

        if (state == EnumButtonState.Normal)
        {
            barShader.Uniform("color", c);
            RenderTools.RenderNineSlice(texture, barShader, X, Y, Width, Height);
        }

        barShader.Uniform("color", Vector4.One);

        shader.Use();

        text.color = f;
        text.RenderCenteredLine(XCenter, YCenter, shader, true);
    }

    protected virtual void GuiEvents_MouseMove(MouseEvent obj)
    {
        if (IsInAllBounds(obj) && !obj.Handled)
        {
            if (state != EnumButtonState.Active) state = EnumButtonState.Hovered;
            obj.Handled = true;
        }
        else
        {
            if (state != EnumButtonState.Active) state = EnumButtonState.Normal;
        }
    }

    protected virtual void GuiEvents_MouseDown(MouseEvent obj)
    {
        if (!obj.Handled && IsInAllBounds(obj))
        {
            obj.Handled = true;
            state = EnumButtonState.Active;
        }
    }

    protected virtual void GuiEvents_MouseUp(MouseEvent obj)
    {
        if (state != EnumButtonState.Active) return;

        state = IsInAllBounds(obj) ? EnumButtonState.Hovered : EnumButtonState.Normal;
    }
}