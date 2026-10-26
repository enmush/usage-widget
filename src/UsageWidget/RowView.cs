using Avalonia;
using Avalonia.Media;
using UsageWidget.Core;

namespace UsageWidget;

/// <summary>One Row's cells plus the bar brushes for their usage bands.</summary>
sealed class RowView(RowCells cells)
{
    public RowCells Cells { get; } = cells;
    public IBrush SessionBrush { get; } = BandBrush(cells.SessionBand);
    public IBrush WeeklyBrush { get; } = BandBrush(cells.WeeklyBand);
    public double SessionBarOpacity => Cells.ShowSessionBar ? 1 : 0;

    static IBrush BandBrush(Band band) => (IBrush)Application.Current!.Resources[band switch
    {
        Band.Danger => "BandDanger",
        Band.Warning => "BandWarning",
        _ => "BandNormal",
    }]!;
}
