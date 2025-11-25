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
        WidgetVanillaTextInputBox textB = (WidgetVanillaTextInputBox)new WidgetVanillaTextInputBox(parent, parent.Gui, false, true, null, null, "Guild Name").Alignment(Align.Center)
            .Fixed(0, 0, 64, 12);

        new WidgetVanillaButton(textB, parent.Gui, () =>
        {
            GuildPacket packet = new()
            {
                type = EnumGuildPacket.Create,
                data = SerializerUtil.Serialize(textB.text.Text)
            };

            MainAPI.GetGameSystem<GuildManager>(EnumAppSide.Client).SendPacket(packet);
        }, "Create Guild").Alignment(Align.CenterBottom, AlignFlags.OutsideV).Fixed(0, 0, 64, 12);
    }
}