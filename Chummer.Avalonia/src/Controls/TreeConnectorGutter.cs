using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Chummer.NewUI.Controls;

/// <summary>
///     Draws classic-TreeView connectors for one TreeViewItem: a continuous dashed vertical line for
///     each ancestor level that still has more siblings below it (so a trunk keeps running past
///     unrelated, deeper rows instead of stopping the moment an ancestor's subtree is expanded), this
///     item's own vertical anchor (top-half joining the previous sibling, bottom-half joining the
///     next), and a horizontal dashed "leg" bridging from the parent's own column over to this item's
///     column - the classic "+--" tee that every item gets regardless of whether it also has a box.
///     This has to be a custom-rendered control rather than a fixed set of XAML Line elements because
///     the number of ancestor columns varies per item - there's no fixed shape count to declare
///     upfront in a ControlTemplate.
/// </summary>
public sealed class TreeConnectorGutter : Control
{
    private const double ColumnWidth = 16;
    private const double BoxWidth = 9;
    private const double BoxHalfWidth = BoxWidth / 2;
    private const double MidY = 9;
    private const double RowHeight = 18;
    private const double YOffset = 1;

    private static readonly IPen ConnectorPen =
        new Pen(new SolidColorBrush(Color.Parse("#ACA899")), 1, new DashStyle(new List<double> { 1, 1 }, 0));

    private static readonly IBrush DotBrush = new SolidColorBrush(Color.Parse("#ACA899"));

    public override void Render(DrawingContext context)
    {
        if (TemplatedParent is not TreeViewItem item)
            return;

        // Walk from this item up to the root TreeView, recording at each level whether that
        // ancestor (or the item itself, at the last step) was the last child in its own parent's
        // Items collection. Built bottom-up, then reversed so index 0 is the top-most ancestor
        // and the last index is the item itself - index N therefore always corresponds to
        // TreeViewItem.Level == N, which is what LineX expects.
        var isLastAtLevel = new List<bool>();
        Control node = item;
        while (node.Parent is ItemsControl parent)
        {
            isLastAtLevel.Add(IsLastSibling(node, parent));
            if (parent is TreeViewItem parentItem)
                node = parentItem;
            else
                break;
        }

        isLastAtLevel.Reverse();

        // Every level's vertical guide sits at that level's own expander-box CENTER, not the
        // midpoint of its 16px indent slot - the box lives in the *next* Grid column (right after
        // this gutter), so its center is offset by half its own width past the slot boundary.
        double LineX(int level)
        {
            return (level + 1) * ColumnWidth + BoxHalfWidth;
        }

        // Ancestor pass-through: for every shallower level that still has more siblings below it,
        // its trunk must keep running past this row even though this row belongs to a different,
        // deeper branch - otherwise the line would appear to stop the instant an ancestor's
        // subtree is expanded, instead of continuing on to the ancestor's own next sibling.
        for (var level = 0; level < isLastAtLevel.Count - 1; level++)
            if (!isLastAtLevel[level])
            {
                var x = LineX(level);
                context.DrawLine(ConnectorPen, new Point(x, 0), new Point(x, RowHeight));
            }

        // This item's own anchor: top-half joins the incoming connection from above (the previous
        // sibling's own trunk) - a first child has no previous sibling, its only incoming
        // connection is the horizontal leg below, so only a true first-level root with nothing at
        // all above it skips the top-half entirely. Bottom-half draws only if this item itself has
        // a following sibling.
        var myX = LineX(isLastAtLevel.Count - 1);
        var amLast = isLastAtLevel.Count == 0 || isLastAtLevel[^1];
        var isRoot = isLastAtLevel.Count <= 1;
        var isFirstRoot = isRoot && IsFirstSibling(item, (ItemsControl)item.Parent!);
        var topY = isFirstRoot ? MidY : 0;
        context.DrawLine(ConnectorPen, new Point(myX, topY), new Point(myX, amLast ? MidY : RowHeight));

        // Horizontal leg bridging from the parent's own guide column over to this item's own
        // column, at the row's vertical midpoint - every item gets one regardless of whether it
        // also has a box. Root items have no parent column to bridge from, so they get none.
        // Drawn as explicit 1px dots rather than through ConnectorPen's DashStyle - axis-aligned
        // hairline dashes render fine vertically but blur into one solid band horizontally, so the
        // dash pattern is reproduced by hand here instead of relying on the renderer for that axis.
        if (isLastAtLevel.Count > 1)
        {
            var parentX = LineX(isLastAtLevel.Count - 2);
            DrawDottedHorizontal(context, parentX + ColumnWidth, myX + BoxWidth, MidY - YOffset);
        }
    }

    private static void DrawDottedHorizontal(DrawingContext context, double x1, double x2, double y)
    {
        var start = Math.Min(x1, x2);
        var end = Math.Max(x1, x2);
        var top = Math.Round(y) - 0.5;
        for (var x = start; x < end; x += 2) context.FillRectangle(DotBrush, new Rect(Math.Round(x), top, 1, 1));
    }

    private static bool IsLastSibling(Control control, ItemsControl parent)
    {
        if (parent.Items is IList list)
        {
            var index = list.IndexOf(control);
            return index < 0 || index == list.Count - 1;
        }

        return true;
    }

    private static bool IsFirstSibling(Control control, ItemsControl parent)
    {
        if (parent.Items is IList list)
        {
            var index = list.IndexOf(control);
            return index <= 0;
        }

        return true;
    }
}