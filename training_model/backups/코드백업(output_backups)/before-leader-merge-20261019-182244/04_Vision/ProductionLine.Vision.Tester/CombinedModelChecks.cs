using OpenCvSharp;
using ProductionLine.Shared;
using ProductionLine.Vision;
using System.Diagnostics;
using System.Security.Cryptography;

internal static class CombinedModelChecks
{
    public static void Run(string imagePath)
    {
        var checks = 0;
        void Check(bool value, string text)
        {
            if (!value) throw new InvalidOperationException(text);
            checks++; Console.WriteLine("PASS: " + text);
        }
        var bytes = File.ReadAllBytes(imagePath);
        using var combined = YoloOnnxInspector.CreateCombined();
        Check(combined.LoadedModelPaths.Count == 9, "all nine unique trained models are loaded");
        var hashes = combined.LoadedModelPaths.Select(path => { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }).ToList();
        Check(hashes.Distinct().Count() == 9, "duplicate file copies do not execute same learned weights twice");
        Check(hashes.Contains("C84FD688EE00A1FEB51BBE8344CFA31B2F56A4844F17E2D0B7A962484611A37B"), "new best (1).onnx is part of runtime detection");
        foreach (var name in new[] { "phone", "crack_phone", "crack", "iphone_16", "iphone_15", "iphone_15_pro",
            "iphone_12pro_graphite", "iphone_12pro_pacificblue", "iphone_12pro_silver",
            "iphone_14_white", "iphone16_white" })
            Check(combined.ClassNames.Contains(name), "retained learned class: " + name);
        var classified = new List<YoloInspectionAnalysis>();
        foreach (var path in combined.LoadedModelPaths)
        {
            using var model = new YoloOnnxInspector(path, .45f);
            var result = model.Inspect(bytes);
            Check(result.ResultImage.Length > 0, "ONNX input/output and image processing compatible: " + Path.GetFileName(Path.GetDirectoryName(path)));
            if (model.CanClassifyProducts && result.ObjectFound) classified.Add(result);
            Console.WriteLine("MODEL: " + Path.GetFileName(Path.GetDirectoryName(path)) + " | " + string.Join(", ", model.ClassNames));
        }
        var timer = Stopwatch.StartNew();
        var analysis = combined.Inspect(bytes);
        timer.Stop();
        Console.WriteLine("COMBINED_PROCESSING_MS=" + timer.ElapsedMilliseconds);
        Console.WriteLine($"COMBINED_RESULT={analysis.Result}; REASON={analysis.Reason}");
        Check(analysis.ObjectFound && analysis.Detections.Any(x => YoloOnnxInspector.IsPhoneClass(x.ClassName)), "combined models find sample phone");
        Check(analysis.Detections.All(x => combined.ClassNames[x.ClassId] == x.ClassName), "combined class IDs remain mapped to correct class names");
        Check(analysis.Result != InspectionResultType.Undetermined,
            "combined phone model and visible-surface check produce a final quality result");
        using var empty = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
        Check(!combined.Inspect(empty.ToBytes(".jpg")).ObjectFound, "empty frame does not become phone result in combined mode");
        var output = Path.Combine(AppContext.BaseDirectory, "combined-model-verification");
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "all-models-result.jpg"), analysis.ResultImage);
        Console.WriteLine($"ALL MODEL CHECKS PASSED: {checks}; classes={string.Join(", ", combined.ClassNames)}");
    }
}
