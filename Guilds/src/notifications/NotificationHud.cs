using OpenTK.Mathematics;
using Vintagestory.API.Client;

namespace Guilds;

public class NotificationHud : Gui
{
    public override EnumDialogType DialogType => EnumDialogType.HUD;
    public override bool UnregisterOnClose => false;

    public override void PopulateWidgets()
    {

    }

    public void AddNotification(string text, Vector3 color)
    {
        foreach (WidgetFadingText fader in ForWidgets<WidgetFadingText>())
        {
            // Move every widget up.
            Vector2i pos = fader.GetFixedPos();
            fader.FixedPos(pos.X, pos.Y - (int)(fader.text.font.LineHeight * fader.text.fontScale));
        }

        Widget widget = new WidgetFadingText(null, this, text, 30, false, color)
            .Alignment(Align.RightBottom);

        AddWidget(widget);

        MainAPI.Capi.Gui.PlaySound("tick");
    }
}