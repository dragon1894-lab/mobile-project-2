# v5 모델 인수인계 (학습 PC → 컨베이어 PC, 2026-10-22)

> **이 문서를 받은 Claude에게:** 최종 학습 모델은 **`training_model/v5_merged_default/v5_merged_default.onnx`** 입니다.
> v4에서 **클래스 이름이 바뀌었고 불량이 2개 클래스로 나뉘었습니다.** 지금 앱 코드(`04_Vision/ProductionLine.Vision/YoloOnnxInspector.cs`)는 `crack_iphone`이라는 이름으로 불량을 찾기 때문에, **코드를 먼저 고치지 않고 v5를 넣으면 깨진 폰이 정상으로 판정됩니다.** 아래 "2. 앱 고치기"를 먼저 하세요.
> 성능 그래프와 지표는 `training_model/data_set/`에 있습니다.

## 1. 무엇이 바뀌었나 (v4 → v5)

| 번호 | v4 이름 | **v5 이름** | 판정 |
|---|---|---|---|
| 0 | `crack_iphone` (모든 깨진 폰) | **`iphone12_promax_crack`** (깨진 12 Pro Max) | 불량 |
| 1 | `iphon16_white` | **`iphone16_white`** | 정상 |
| 2 | `iphone_12_pro_silver` | **`iphone12_pro_silver`** | 정상 |
| 3 | `iphone15pro_Natural_Titanium` | **`iphone15_pro_Natural_Titanium`** | 정상 |
| 4 | `iphone_14_white` | **`iphone14_white`** | 정상 |
| 5 | (없음) | **`iphone8_crack`** (깨진 아이폰8) | 불량 |

- ONNX 출력: `[1, 9, 8400]` → **`[1, 10, 8400]`** (앱은 `4 + 클래스 수`로 계산하므로 그대로 동작).
- 라벨 기준(사진 1장에 박스 2개: 폰 전체 + 카메라 모듈 / 깨진 폰은 폰 전체 + 깨진 부위)은 그대로입니다. 두 박스 모두 같은 이름이 붙습니다.
- 이름은 김하균님이 정한 그대로입니다. **`report_from/README.md`에 적힌 `iphone12_pro_max_crack`, `iphone_8_crack`이 아니라 `iphone12_promax_crack`, `iphone8_crack`** 입니다(보고서·DB 기종 칸도 이 이름).
- 8·12 Pro Max가 아닌 깨진 폰 사진은 학습에서 뺐습니다. 다른 기종의 깨진 폰은 약 69%만 불량으로 잡습니다.

## 2. 앱 고치기 (`04_Vision/ProductionLine.Vision/YoloOnnxInspector.cs`, final 20261022 기준)

지금 코드의 문제:
- `IsUnifiedModel => _classNames.Contains(CrackIphone) && ...` → v5에는 `crack_iphone`이 없어서 **통합 모델로 인식되지 않음**.
- `InspectUnified`의 `detections.Any(x => x.ClassName == CrackIphone)` → 불량을 못 찾음.
- `IsSpecificPhoneClass`는 `iphone` + 숫자로 시작하면 기종으로 봄 → **`iphone8_crack`, `iphone12_promax_crack`이 정상 기종으로 취급됨.**

고칠 내용 (예시 — 실제 코드에 맞게 적용하고 빌드·시험하세요):

```csharp
// 불량 클래스: v4의 crack_iphone + v5의 *_crack
public static bool IsDefectClass(string className) =>
    className == CrackIphone || className.EndsWith("_crack", StringComparison.Ordinal);

// 통합 모델 판별
private bool IsUnifiedModel => _classNames.Any(IsDefectClass) && _classNames.Any(IsSpecificPhoneClass);

// 기종 클래스에서 불량 클래스는 뺌
private static bool IsSpecificPhoneClass(string className)
{
    if (IsDefectClass(className)) return false;
    ... (기존 코드 그대로)
}

// InspectUnified 안
var defect = detections.Where(x => IsDefectClass(x.ClassName)).MaxBy(x => x.Box.Width * x.Box.Height);
if (defect != null)
{
    // 불량 이름(예: iphone8_crack)을 그대로 기종 칸·화면에 사용
    DrawDetection(annotated, wholePhone with { ClassName = defect.ClassName }, defect.ClassName);
    return new(true, InspectionResultType.Defect,
        $"{defect.ClassName}(파손)이 검출되어 불량으로 판정했습니다.", detections, annotated.ToBytes(".jpg"));
}
```

함께 확인할 곳:
- `IsPhoneClass`, 색 지정(`CrackPhone or CrackIphone => 빨강`, 313행 근처)에 `IsDefectClass`도 포함.
- 불량일 때 DB `PhoneModel`(기종 칸)에 **불량 클래스 이름**(`iphone8_crack` 등)이 들어가게 할지 김하균님 보고서 기준(`report_from/README.md` 2번 표: 기종별 분석에 `iphone12_..._crack`, `iphone_8_crack` 줄이 있음)과 맞추세요.
- `DisplayClassName`의 `"iphone16_white" or "iphon16_white" => "iphone_16_white"` 변환: v5 이름은 `iphone16_white`라 이 변환 때문에 화면·DB에 `iphone_16_white`로 바뀌어 들어갑니다. 보고서에 클래스 이름 그대로 쓰기로 했으므로 **이 변환을 지울지 김하균님께 확인**하세요.
- `ProductionLine.Vision.Tester/CombinedModelChecks.cs`, `01_AdminApp/.../VirtualInspectionSamples.cs`에도 옛 이름이 있습니다(시험·가상 샘플용).

## 3. 모델 교체 (코드 수정 후)

1. `git pull`
2. 앱 `04_Vision/models/`의 v4 `best.onnx`를 **지우고** `training_model/v5_merged_default/v5_merged_default.onnx`를 `best.onnx` 이름으로 넣기.
3. 같은 폴더에 `training_model/v5_merged_default/classes.txt`와 **`model_info.json`(이미 v5용으로 작성해 둠)** 을 덮어쓰기.
4. 빌드 출력 폴더마다 옛 `.onnx`가 남지 않게 정리하고 다시 빌드 → 앱 실행 폴더마다 **v5 하나만**.
5. 실물 시험: 깨진 8, 깨진 12 Pro Max → **불량 + 맞는 이름**, 정상 4종 → 맞는 기종, 빈 벨트 → 검출 없음. 결과를 `docs/AI_COLLAB_LOG.md`에 기록.

- v5 SHA256: `9fb6d967b016862953b3f5e9521da4b55850d4f97120bcf954f6f8ea1d92038b`
- 되돌리기: v4는 `training_model/v4_merged_default/` (앱 코드는 v4·v5 둘 다 되게 고치면 파일만 바꿔 끼울 수 있음)

## 4. 성능 (학습 PC 측정, 앱과 같은 판정: 신뢰도 0.45, IoU 0.45, letterbox 114)

| 시험 사진 (모두 학습·검증에 안 씀) | v4 | **v5** |
|---|---|---|
| 컨베이어 깨진 12 Pro Max 10 | 불량 10/10 (이름 `crack_iphone`) | **10/10 `iphone12_promax_crack`** |
| 컨베이어 깨진 아이폰8 10 | 불량 10/10 (이름 `crack_iphone`) | **10/10 `iphone8_crack`** |
| 옛 깨진 12 Pro Max 8 | 불량 8/8 | **8/8 `iphone12_promax_crack`** |
| 컨베이어 16 화이트 20 / 15 Pro 25 / 14 화이트 10 / 12 Pro 실버 8 | 전부 맞음 | 전부 맞음 |
| 빈 벨트 6 | 6/6 | 6/6 |
| 옛 12 Pro 33 / 14 화이트 12 / 16 화이트 7 | 33/33, 12/12, 6/7 | 33/33, 12/12, **7/7** |
| 우드락 전 컨베이어 6장 기종 / 정상→불량 오판 | 5/6 / 1 | 4/6 / **0** |
| 검은 화면 | 검출 없음 | 검출 없음 |
| **전체 판정** | **153/155** | **153/155** |
| 검증 P / R / mAP50 / mAP50-95 | 0.989 / 0.968 / 0.980 / 0.837 | **0.993 / 0.993 / 0.991 / 0.861** |

- 클래스별 검증 mAP50-95: 12 Pro Max 깨짐 0.869, 16 화이트 0.873, 12 Pro 실버 0.778, 15 Pro 0.887, 14 화이트 0.861, 8 깨짐 0.873 (`data_set/v5_metrics_summary.json`).
- ⚠️ 컨베이어 시험 사진은 학습 사진과 같은 날·같은 환경 촬영이라 점수가 낙관적입니다. 실물 시험이 최종 기준입니다.

## 5. 다시 만들기

- 학습 데이터: 학습 PC 바탕화면 `v5_merged_default/dataset/` (all_phones 사진·라벨을 번호만 바꿔 복사). `all_phones/`(GitHub)는 아직 v4 이름 그대로입니다.
- 학습 설정: `v5_merged_default/settings.json` (yolo26n, 640, 300 에포크, 배치 16, workers 6, RAM 캐시, 기본 증강).
