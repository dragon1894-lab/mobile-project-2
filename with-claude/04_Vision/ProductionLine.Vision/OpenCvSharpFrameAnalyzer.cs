using OpenCvSharp;
using ProductionLine.Shared;

namespace ProductionLine.Vision;

/// <summary>
/// 이미지 바이트를 이용한 OpenCvSharp 연결 예제입니다.
/// 촬영 환경이 확정되면 ROI, 이진화, 윤곽 선택, 색상 공간을 현장 기준으로 교체합니다.
/// </summary>
public sealed class OpenCvSharpFrameAnalyzer
{
    private readonly VisionInspector _inspector;

    public OpenCvSharpFrameAnalyzer(VisionInspector inspector) => _inspector = inspector;

    public InspectionDto Inspect(int lineId, byte[] sampleImage, byte[] referenceImage)
    {
        using var sample = Cv2.ImDecode(sampleImage, ImreadModes.Color);
        using var reference = Cv2.ImDecode(referenceImage, ImreadModes.Color);
        if (sample.Empty() || reference.Empty())
            return _inspector.Inspect(new InspectionRequest(lineId, 0, 0, false));

        using var resized = new Mat();
        Cv2.Resize(sample, resized, reference.Size());
        var shapeScore = CalculateShapeScore(resized, reference);
        var colorScore = CalculateColorScore(resized, reference);
        return _inspector.Inspect(new InspectionRequest(lineId, shapeScore, colorScore));
    }

    private static double CalculateShapeScore(Mat sample, Mat reference)
    {
        using var sampleGray = new Mat();
        using var referenceGray = new Mat();
        using var sampleBinary = new Mat();
        using var referenceBinary = new Mat();
        Cv2.CvtColor(sample, sampleGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(reference, referenceGray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(sampleGray, sampleBinary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        Cv2.Threshold(referenceGray, referenceBinary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        var sampleContour = LargestContour(sampleBinary);
        var referenceContour = LargestContour(referenceBinary);
        if (sampleContour is null || referenceContour is null) return 0;
        var distance = Cv2.MatchShapes(sampleContour, referenceContour, ShapeMatchModes.I1);
        return Math.Clamp(1.0 / (1.0 + distance), 0, 1);
    }

    private static double CalculateColorScore(Mat sample, Mat reference)
    {
        var a = Cv2.Mean(sample);
        var b = Cv2.Mean(reference);
        var distance = Math.Sqrt(Math.Pow(a.Val0 - b.Val0, 2) + Math.Pow(a.Val1 - b.Val1, 2) + Math.Pow(a.Val2 - b.Val2, 2));
        return Math.Clamp(1.0 - distance / (Math.Sqrt(3) * 255.0), 0, 1);
    }

    private static Point[]? LargestContour(Mat binary)
    {
        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        return contours.OrderByDescending(contour => Cv2.ContourArea(contour)).FirstOrDefault();
    }
}
