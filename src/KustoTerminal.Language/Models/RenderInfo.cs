namespace KustoTerminal.Language.Models;

public class RenderInfo
{
    public VisualizationKind VisualizationKind { get; set; }
}

public enum VisualizationKind
{
    anomalychart,
    areachart,
    barchart,
    columnchart,
    ladderchart,
    linechart,
    piechart,
    pivotchart,
    scatterchart,
    stackedareachart,
    timechart,
    table,
    timeline,
    timepivot,
    card,
    plotly
}