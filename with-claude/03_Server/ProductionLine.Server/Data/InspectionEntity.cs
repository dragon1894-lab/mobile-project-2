using ProductionLine.Shared;

namespace ProductionLine.Server.Data;

// [DB 표] Inspections — 검사 한 번에 한 줄씩 쌓이는 검사 결과 (불량인지 왜 불량인지 이런거 저장)
// 지금은 앱이 보낸 점수로 판정하는 시뮬레이션 방식입니다. YOLO 결과가 정해지면 칸이 추가될 수 있습니다.
public sealed class InspectionEntity
{
    public Guid Id { get; set; }                      // 검사 고유 번호
    public int LineId { get; set; }                   // 어느 라인에서 검사했는지
    public InspectionResultType Result { get; set; }  // 0 = 정상, 1 = 불량, 2 = 판정 불가
    public double ShapeScore { get; set; }            // 형태·윤곽 점수 (0~1)
    public double ColorScore { get; set; }            // 색상 점수 (0~1)
    public string Reason { get; set; } = "";          // 판정 이유 (예: 색상 점수가 기준보다 낮음)
    public DateTimeOffset InspectedAt { get; set; }   // 검사 시각
}
