using MareLib;
using OpenTK.Mathematics;
using System;
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

    private Gui? gui;
    private readonly Widget container;
    private readonly Action<T, WidgetRightClickableField> onFieldClicked;

    public WidgetSortableTable(Widget? parent, List<T> data, Action<T, WidgetRightClickableField> onFieldClicked, params Column<T>[] columns) : base(parent)
    {
        this.data = data;
        this.onFieldClicked = onFieldClicked;
        this.columns = columns;

        // Initialize columns.
        float totalWeight = columns.Sum(c => c.widthWeight);
        float totalAdvance = 0;

        for (int i = 0; i < columns.Length; i++)
        {
            Column<T> column = columns[i];

            float ratio = column.widthWeight / totalWeight;

            new WidgetToggleableButton(this, (down) =>
            {
                if (!down) return;

                // Release other buttons.
                ForEachChild<WidgetToggleableButton>(button =>
                {
                    button.Release();
                });

                this.data.Sort(column.comparer);

                UpdateData();
            },
            $"{column.name}", new Vector4(0.5f, 0, 0, 1f))
                .Percent(totalAdvance, 0, ratio, 1)
                .Alignment(Align.LeftTop);

            totalAdvance += ratio;
        }

        container = new WidgetDummy(this).Percent(0, 0, 1, 1).Alignment(Align.CenterBottom, AlignFlags.OutsideV).SetChildSizing(ChildSizing.Height | ChildSizing.Once);

        UpdateData();
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        gui = guiEvents.gui;
    }

    public void UpdateData()
    {
        container.ClearChildren();

        for (int i = 0; i < data.Count; i++)
        {
            float totalWeight = columns.Sum(c => c.widthWeight);
            float totalAdvance = 0;

            T datum = data[i];

            for (int c = 0; c < columns.Length; c++)
            {
                Column<T> column = columns[c];
                float ratio = column.widthWeight / totalWeight;
                // Add.

                Vector2i pos = GetFixedPos();

                WidgetRightClickableField field = (WidgetRightClickableField)new WidgetRightClickableField(container, () => { }, column.getValue(datum))
                    .Percent(totalAdvance, 0, ratio, 1)
                    .FixedY(i * 8) // Take height of sort button.
                    .FixedHeight(8)
                    .Alignment(Align.LeftTop);

                field.SetCallback(() =>
                {
                    onFieldClicked(datum, field);
                });

                totalAdvance += ratio;
            }
        }

        container.SetBounds();
        gui?.MarkForRepartition();
    }
}