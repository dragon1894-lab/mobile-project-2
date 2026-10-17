namespace ProductionLine.Server.Data;

public sealed class LineEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsReal { get; set; }
    public string OperationStatus { get; set; } = "";
    public string InspectionStatus { get; set; } = "";
    public int NormalCount { get; set; }
    public int DefectCount { get; set; }
    public int UndeterminedCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}