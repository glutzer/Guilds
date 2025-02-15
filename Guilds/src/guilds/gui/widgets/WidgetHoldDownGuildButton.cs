using MareLib;
using OpenTK.Mathematics;
using System;
using Vintagestory.API.Client;

namespace Guilds;

public class WidgetHoldDownGuildButton : Widget
{
    protected EnumButtonState state = EnumButtonState.Normal;
    protected Action onClick;

    private float accum;
    private readonly float time;

    private readonly NineSliceTexture texture;
    protected Vector4 color;
    protected Vector4 fontColor;

    protected TextObject text;

    public WidgetHoldDownGuildButton(Widget? parent, float time, Action onClick, string text) : base(parent)
    {
        color = GuiThemes.ButtonColor;
        fontColor = GuiThemes.TextColor;

        this.time = time;
        this.onClick = onClick;
        texture = GuiThemes.Button;

        this.text = new TextObject(text, GuiThemes.Font, 50, fontColor);

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

    public override void OnRender(float dt, MareShader shader)
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

        accum = Math.Clamp(accum, 0, time);

        MareShader barShader = MareShaderRegistry.Get("bargui");
        barShader.Use();
        barShader.Uniform("progress", accum / time);

        Vector4 c = color;
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
        if (!obj.Handled && IsInsideAndClip(obj))
        {
            obj.Handled = true;
            state = EnumButtonState.Active;
        }
    }

    protected virtual void GuiEvents_MouseUp(MouseEvent obj)
    {
        if (state != EnumButtonState.Active) return;

        if (IsInsideAndClip(obj))
        {
            state = EnumButtonState.Hovered;
        }
        else
        {
            state = EnumButtonState.Normal;
        }
    }
}