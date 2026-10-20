using ProductionLine.Vision;

if (args.FirstOrDefault() == "--all-models")
{
    CombinedModelChecks.Run(args[1]);
    return;
}

var defaultImagePath = Path.Combine(
    AppContext.BaseDirectory,
    "samples",
    "iphone",
    "iphone_sample.jpg");
var defaultModelPath = Path.Combine(
    AppContext.BaseDirectory,
    "Models",
    "best.onnx");

var imagePath = args.Length > 0 ? Path.GetFullPath(args[0]) : defaultImagePath;
var modelPath = args.Length > 1 ? Path.GetFullPath(args[1]) : defaultModelPath;
var threshold = args.Length > 2 && float.TryParse(args[2], out var parsedThreshold)
    ? parsedThreshold
    : 0.5f;

if (!File.Exists(imagePath))
{
    Console.WriteLine($"이미지 파일을 찾을 수 없습니다: {imagePath}");
    Console.WriteLine("사용법: ProductionLine.Vision.Tester.exe [사진 경로] [best.onnx 경로] [신뢰도 기준]");
    return;
}

if (!File.Exists(modelPath))
{
    Console.WriteLine($"ONNX 모델을 찾을 수 없습니다: {modelPath}");
    return;
}

using var inspector = new YoloOnnxInspector(modelPath, threshold);
var analysis = inspector.Inspect(await File.ReadAllBytesAsync(imagePath));

var outputDirectory = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "..", "..", "..", "..",
    "output"));
Directory.CreateDirectory(outputDirectory);
var resultPath = Path.Combine(outputDirectory, "iphone_result.jpg");
if (analysis.ResultImage.Length > 0)
    await File.WriteAllBytesAsync(resultPath, analysis.ResultImage);

Console.WriteLine("========== 휴대폰 YOLO 검출 결과 ==========");
Console.WriteLine($"모델: {modelPath}");
Console.WriteLine($"사진: {imagePath}");
Console.WriteLine($"신뢰도 기준: {threshold:P0}");
Console.WriteLine($"제품 검출: {(analysis.ObjectFound ? "성공" : "실패")}");
Console.WriteLine($"최종 판정: {analysis.Result}");
Console.WriteLine($"판정 사유: {analysis.Reason}");
foreach (var detection in analysis.Detections)
{
    Console.WriteLine(
        $"- {detection.ClassName} {detection.Confidence:P1} " +
        $"X={detection.Box.X:F0}, Y={detection.Box.Y:F0}, W={detection.Box.Width:F0}, H={detection.Box.Height:F0}");
}
Console.WriteLine($"검출 결과 이미지: {resultPath}");
