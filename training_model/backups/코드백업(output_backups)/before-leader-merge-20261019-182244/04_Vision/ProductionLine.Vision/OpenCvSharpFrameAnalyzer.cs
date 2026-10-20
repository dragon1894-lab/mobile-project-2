using OpenCvSharp;
using ProductionLine.Shared;

namespace ProductionLine.Vision;

/// <summary>
/// 화장품용 주황색 뚜껑의 색상과 외곽 형태를 검사합니다.
/// AI를 사용하지 않으며 OpenCV의 색상 분리와 윤곽선 계산만 사용합니다.
/// </summary>
public sealed class OpenCvSharpFrameAnalyzer
{
    private readonly VisionInspector _inspector;

    // 실제 컨베이어 조명에서 정상 샘플을 여러 장 촬영한 뒤 이 값만 조정하면 됩니다.
    private static readonly Scalar OrangeLowerHsv = new(0, 100, 70);
    private static readonly Scalar OrangeUpperHsv = new(25, 255, 255);
    private const double MinimumObjectAreaRatio = 0.01;

    public OpenCvSharpFrameAnalyzer(VisionInspector inspector) => _inspector = inspector;

    /// <summary>
    /// 기존 서버 연결용 검사입니다. 정상 기준 사진과 촬영 사진을 비교합니다.
    /// </summary>
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

    /// <summary>
    /// 주황색 뚜껑 한 개를 찾아 형태·윤곽 점수와 색상 점수를 계산합니다.
    /// 테스트 프로그램과 카메라 화면에서 공통으로 사용하는 메서드입니다.
    /// </summary>
    public CapInspectionAnalysis AnalyzeOrangeCap(byte[] imageBytes)
    {
        using var image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (image.Empty())
            return CapInspectionAnalysis.Failed("이미지를 읽지 못했습니다.");

        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, OrangeLowerHsv, OrangeUpperHsv, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(9, 9));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        var contour = LargestContour(mask);
        if (contour is null)
            return CreateFailedResult(image, mask, "주황색 뚜껑을 찾지 못했습니다.");

        var area = Cv2.ContourArea(contour);
        var frameArea = image.Width * (double)image.Height;
        if (area / frameArea < MinimumObjectAreaRatio)
            return CreateFailedResult(image, mask, "검출된 물체가 너무 작습니다. 카메라 위치를 확인하세요.");

        var perimeter = Cv2.ArcLength(contour, true);
        var circularity = perimeter <= 0 ? 0 : 4.0 * Math.PI * area / (perimeter * perimeter);
        var bounds = Cv2.BoundingRect(contour);
        var aspectRatio = Math.Min(bounds.Width, bounds.Height) / (double)Math.Max(bounds.Width, bounds.Height);

        var hull = Cv2.ConvexHull(contour);
        var hullArea = Cv2.ContourArea(hull);
        var solidity = hullArea <= 0 ? 0 : area / hullArea;

        // 촬영 각도와 뚜껑 옆면 톱니 때문에 원형도만 사용하면 오판하기 쉽습니다.
        // 가로세로 비율과 윤곽의 채움 정도를 함께 사용해 형태 점수를 계산합니다.
        var shapeScore = Math.Clamp(aspectRatio * 0.55 + solidity * 0.45, 0, 1);
        var colorScore = CalculateOrangeColorScore(hsv, mask, contour);
        var inspection = _inspector.Inspect(new InspectionRequest(1, shapeScore, colorScore));

        using var annotated = image.Clone();
        var contourColor = inspection.Result == InspectionResultType.Normal
            ? new Scalar(0, 200, 0)
            : new Scalar(0, 0, 255);
        Cv2.DrawContours(annotated, new[] { contour }, -1, contourColor, 8);
        Cv2.Rectangle(annotated, bounds, new Scalar(255, 0, 0), 6);

        var label = $"{inspection.Result}  Shape:{shapeScore:F2}  Color:{colorScore:F2}";
        var labelPoint = new Point(Math.Max(10, bounds.X), Math.Max(45, bounds.Y - 15));
        Cv2.PutText(annotated, label, labelPoint, HersheyFonts.HersheySimplex, 1.1, contourColor, 3);

        Cv2.ImEncode(".png", mask, out var maskBytes);
        Cv2.ImEncode(".png", annotated, out var resultBytes);

        return new CapInspectionAnalysis(
            true,
            inspection.Result,
            inspection.Reason,
            shapeScore,
            colorScore,
            area,
            perimeter,
            circularity,
            aspectRatio,
            solidity,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            maskBytes,
            resultBytes);
    }

    /// <summary>
    /// 빠르게 지나가는 주황색 뚜껑이 화면에 들어왔는지만 저해상도로 확인합니다.
    /// 실제 형태·색상 판정 전에 호출해 동일 제품의 중복 검사와 빈 화면 검사를 막습니다.
    /// </summary>
    public bool ContainsOrangeCap(byte[] imageBytes)
    {
        using var original = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (original.Empty()) return false;

        using var resized = new Mat();
        var scale = Math.Min(1.0, 640.0 / original.Width);
        if (scale < 1.0)
            Cv2.Resize(original, resized, new Size(), scale, scale, InterpolationFlags.Area);
        else
            original.CopyTo(resized);

        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(resized, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, OrangeLowerHsv, OrangeUpperHsv, mask);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

        var contour = LargestContour(mask);
        if (contour is null) return false;
        var areaRatio = Cv2.ContourArea(contour) / (resized.Width * (double)resized.Height);
        return areaRatio >= 0.003;
    }

    private static CapInspectionAnalysis CreateFailedResult(Mat image, Mat mask, string reason)
    {
        Cv2.ImEncode(".png", mask, out var maskBytes);
        Cv2.ImEncode(".png", image, out var resultBytes);
        return CapInspectionAnalysis.Failed(reason, maskBytes, resultBytes);
    }

    private static double CalculateOrangeColorScore(Mat hsv, Mat mask, Point[] contour)
    {
        using var contourMask = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.DrawContours(contourMask, new[] { contour }, -1, Scalar.White, -1);
        var objectPixels = Cv2.CountNonZero(contourMask);
        if (objectPixels == 0) return 0;

        using var orangeInsideObject = new Mat();
        Cv2.BitwiseAnd(mask, contourMask, orangeInsideObject);
        var orangeRatio = Cv2.CountNonZero(orangeInsideObject) / (double)objectPixels;

        // 채도가 낮으면 조명 반사나 다른 색 물체일 가능성이 높습니다.
        var meanHsv = Cv2.Mean(hsv, orangeInsideObject);
        var saturationScore = Math.Clamp(meanHsv.Val1 / 160.0, 0, 1);
        return Math.Clamp(orangeRatio * 0.75 + saturationScore * 0.25, 0, 1);
    }

    private static double CalculateShapeScore(Mat sample, Mat reference)
    {
        using var sampleBinary = CreateForegroundMask(sample);
        using var referenceBinary = CreateForegroundMask(reference);
        var sampleContour = LargestContour(sampleBinary);
        var referenceContour = LargestContour(referenceBinary);
        if (sampleContour is null || referenceContour is null) return 0;
        var distance = Cv2.MatchShapes(sampleContour, referenceContour, ShapeMatchModes.I1);
        return Math.Clamp(1.0 / (1.0 + distance), 0, 1);
    }

    private static Mat CreateForegroundMask(Mat image)
    {
        var hsv = new Mat();
        var mask = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, OrangeLowerHsv, OrangeUpperHsv, mask);
        hsv.Dispose();
        return mask;
    }

    private static double CalculateColorScore(Mat sample, Mat reference)
    {
        using var sampleHsv = new Mat();
        using var referenceHsv = new Mat();
        Cv2.CvtColor(sample, sampleHsv, ColorConversionCodes.BGR2HSV);
        Cv2.CvtColor(reference, referenceHsv, ColorConversionCodes.BGR2HSV);
        var a = Cv2.Mean(sampleHsv);
        var b = Cv2.Mean(referenceHsv);
        var hueDifference = Math.Abs(a.Val0 - b.Val0);
        var hueDistance = Math.Min(hueDifference, 180 - hueDifference);
        var saturationDistance = Math.Abs(a.Val1 - b.Val1);
        return Math.Clamp(1.0 - (hueDistance / 90.0 * 0.7 + saturationDistance / 255.0 * 0.3), 0, 1);
    }

    private static Point[]? LargestContour(Mat binary)
    {
        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        return contours.OrderByDescending(contour => Cv2.ContourArea(contour)).FirstOrDefault();
    }
}

public sealed record CapInspectionAnalysis(
    bool ObjectFound,
    InspectionResultType Result,
    string Reason,
    double ShapeScore,
    double ColorScore,
    double Area,
    double Perimeter,
    double Circularity,
    double AspectRatio,
    double Solidity,
    int X,
    int Y,
    int Width,
    int Height,
    byte[] MaskImage,
    byte[] ResultImage)
{
    public static CapInspectionAnalysis Failed(string reason, byte[]? mask = null, byte[]? result = null) =>
        new(false, InspectionResultType.Undetermined, reason, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, mask ?? Array.Empty<byte>(), result ?? Array.Empty<byte>());
}
