using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Linq;

namespace Guilds;

public class Column<T>
{
    public readonly string name;
    public readonly float widthWeight;
    public Func<T, string> getValue;
    public Comparison<T> comparer;

    public Column(string name, float widthWeight, Func<T, string> getValue, Comparison<T> comparer)
    {
        this.name = name;
        this.widthWeight = widthWeight;
        this.getValue = getValue;
        this.comparer = comparer;
    }
}

/// <summary>
/// Sortable table with custom columns.
/// </summary>
public class WidgetSortableTable<T> : Widget
{
    private readonly List<T> data;
    private readonly Column<T>[] columns;

    private readonly Widget container;
    private readonly Action<T, WidgetRightClickableField> onFieldClicked;

    public WidgetSortableTable(Widget? parent, Gui gui, List<T> data, Action<T, WidgetRightClickableField> onFieldClicked, params Column<T>[] columns) : base(parent, gui)
    {
        this.data = data;
        this.onFieldClicked = onFieldClicked;
        this.columns = columns;

        // Initialize columns.
        float totalWeight = columns.Sum(c => c.widthWeight);
        float totalAdvance = 0f;

        Widget topButtonContainer = new WidgetContainer(this, gui)
            .PercentWidth(1)
            .FixedHeight(12)
            .Alignment(Align.CenterTop);

        for (int i = 0; i < columns.Length; i++)
        {
            Column<T> column = columns[i];

            float ratio = column.widthWeight / totalWeight;

            new WidgetToggleableButton(topButtonContainer, gui, (down) =>
            {
                if (!down) return;

                // Release other buttons.
                topButtonContainer.ForEachChild<WidgetToggleableButton>(button =>
                {
                    button.Release();
                });

                this.data.Sort(column.comparer);

                UpdateData();
            },
            $"{column.name}")
                .Percent(totalAdvance, 0f, ratio, 1f)
                .Alignment(Align.LeftTop);

            totalAdvance += ratio;
        }

        container = new WidgetContainer(this, gui)
            .PercentWidth(1f)
            .FixedY(Gui.Scaled(12))
            .Alignment(Align.CenterTop)
            .SetChildSizing(ChildSizing.Height | ChildSizing.Once);

        UpdateData();
    }

    public void UpdateData()
    {
        container.DeleteChildren();

        for (int i = 0; i < data.Count; i++)
        {
            float totalWeight = columns.Sum(c => c.widthWeight);
            float totalAdvance = 0f;

            T datum = data[i];

            for (int c = 0; c < columns.Length; c++)
            {
                Column<T> column = columns[c];
                float ratio = column.widthWeight / totalWeight;
                // Add.

                Vector2i pos = GetFixedPos();

                WidgetRightClickableField field = (WidgetRightClickableField)new WidgetRightClickableField(container, Gui, () => { }, column.getValue(datum))
                    .Percent(totalAdvance, 0f, ratio, 1f)
                    .FixedY(Gui.Scaled(i * 8)) // Take height of sort button.
                    .FixedHeight(8)
                    .Alignment(Align.LeftTop);

                field.SetCallback(() =>
                {
                    onFieldClicked(datum, field);
                });

                totalAdvance += ratio;
            }
        }
    }
}