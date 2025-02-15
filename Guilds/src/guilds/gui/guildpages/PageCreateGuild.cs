using MareLib;
using OpenTK.Mathematics;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Guilds;

[GuildPage("Create Guild", 1000)]
public class PageCreateGuild : GuildPageEntry
{
    public PageCreateGuild(string name, int priority, int id, GuildGui guildGui) : base(name, priority, id, guildGui)
    {
    }

    public override void OnCreatePage(Widget parent)
    {
        Widget bg = new WidgetSliceBackground(parent, GuiThemes.Title, new Vector4(0.1f, 0.1f, 0.1f, 1))
            .Alignment(Align.Center)
            .Fixed(0, 0, 64, 12);

        WidgetTextBoxSingle textBox = (WidgetTextBoxSingle)new WidgetTextBoxSingle(bg, FontRegistry.GetFont("friz"), Vector4.One)
        .Alignment(Align.Center)
        .Fixed(0, 0, 64, 12);

        new WidgetGuildButton(textBox, () =>
        {
            GuildPacket packet = new()
            {
                type = EnumGuildPacket.Create,
                data = SerializerUtil.Serialize(textBox.text.Text)
            };

            MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
        }, "Create Guild").Alignment(Align.CenterBottom, AlignFlags.OutsideV).Fixed(0, 0, 64, 12);
    }
}