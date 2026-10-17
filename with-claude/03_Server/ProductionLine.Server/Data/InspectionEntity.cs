using ProductionLine.Shared;

namespace ProductionLine.Server.Data;

public sealed class InspectionEntity
{
    public Guid Id { get; set; }
    public int LineId { get; set; }
    public InspectionResultType Result { get; set; }
    public double ShapeScore { get; set; }
    public double ColorScore { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset InspectedAt { get; set; }
}