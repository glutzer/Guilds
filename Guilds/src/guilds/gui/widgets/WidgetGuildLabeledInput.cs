namespace Guilds;

public class WidgetGuildLabeledInput : Widget
{
    public NineSliceTexture texture;
    public WidgetTextBoxSingle textBox;
    public string lastText;

    public Action<string> onNewText;
    public Func<string, bool> isTextValid;

    public WidgetGuildLabeledInput(Widget? parent, Gui gui, string defaultText, string label, Action<string> onNewText, Func<string, bool> isTextValid) : base(parent, gui)
    {
        texture = VanillaThemes.OutsetTexture;

        this.onNewText = onNewText;
        this.isTextValid = isTextValid;

        textBox = (WidgetTextBoxSingle)new WidgetVanillaTextInputBox(this, gui, false, true, onNewText, defaultText, label)
            .Alignment(Align.LeftTop)
            .Percent(0.5f, 0, 0.5f, 1);

        new WidgetTextLine(this, gui, VanillaThemes.Font, label, VanillaThemes.WhitishTextColor, true)
            .Alignment(Align.LeftTop)
            .Percent(0f, 0f, 0.5f, 1f);

        lastText = defaultText;
    }

    public void OnNewText(string newText)
    {
        if (!isTextValid(newText))
        {
            textBox.SetTextNoEvent(lastText);
            return;
        }

        lastText = newText;

        onNewText(newText);
    }
}