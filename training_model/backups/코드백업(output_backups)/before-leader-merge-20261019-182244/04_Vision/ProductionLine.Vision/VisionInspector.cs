using ProductionLine.Shared;

namespace ProductionLine.Vision;

/// <summary>
/// 4번 영상처리 담당자의 교체 지점입니다.
/// 현재는 형태·윤곽 점수와 색상 점수를 받아 판정하는 실행 가능한 기본 로직입니다.
/// 실제 카메라/OpenCvSharp 분석 결과를 이 메서드에 연결하면 화면·서버 코드는 바꿀 필요가 없습니다.
/// </summary>
public sealed class VisionInspector
{
    public double ShapeThreshold { get; init; } = 0.85;
    public double ColorThreshold { get; init; } = 0.80;

    public InspectionDto Inspect(InspectionRequest request)
    {
        var now = DateTimeOffset.Now;
        if (!request.ImageAcquired || !IsScore(request.ShapeScore) || !IsScore(request.ColorScore))
        {
            return new InspectionDto(
                Guid.NewGuid(), request.LineId, InspectionResultType.Undetermined,
                request.ShapeScore, request.ColorScore,
                "영상을 획득하지 못했거나 검사값이 유효하지 않습니다.", now);
        }

        if (request.ShapeScore < ShapeThreshold)
        {
            return new InspectionDto(
                Guid.NewGuid(), request.LineId, InspectionResultType.Defect,
                request.ShapeScore, request.ColorScore,
                $"형태·윤곽 점수가 기준({ShapeThreshold:P0})보다 낮습니다.", now);
        }

        if (request.ColorScore < ColorThreshold)
        {
            return new InspectionDto(
                Guid.NewGuid(), request.LineId, InspectionResultType.Defect,
                request.ShapeScore, request.ColorScore,
                $"색상 점수가 기준({ColorThreshold:P0})보다 낮습니다.", now);
        }

        return new InspectionDto(
            Guid.NewGuid(), request.LineId, InspectionResultType.Normal,
            request.ShapeScore, request.ColorScore,
            "형태·윤곽 및 색상 기준을 모두 충족했습니다.", now);
    }

    private static bool IsScore(double value) => value is >= 0 and <= 1;
}
