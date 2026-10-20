# all_phones — 아이폰 5종 통합 YOLO 모델 (인수인계, v2 · 2026-10-21)

> **이 문서를 받은 사람·Claude에게:** 폰 5종(불량 1 + 기종 4)을 **하나의 모델**로 학습한 데이터와 결과입니다.
> 이 폴더는 **v2**입니다. 10-20에 올린 첫 모델(v1)은 컨베이어 PC 점검(`from_local_pc_20261020/HANDOFF.md`)에서 과적합으로 판정되어 **v2로 교체했습니다.** v1 파일은 저장소에서 지웠습니다(Git 기록 `c2eed56`에는 남아 있음).
> 컨베이어 PC에서는 **"0. 컨베이어 PC에서 할 일"** 순서대로 진행하세요.
>
> 학습: 2026-10-21, Claude(김하균 PC, RTX 4070 Ti SUPER, Python 3.13, Ultralytics 8.4.183).

## 0. 컨베이어 PC에서 할 일 (순서대로)

1. `git pull` 로 최신 받기.
2. 앱의 기존 통합 모델을 **`all_phones/models/best.onnx`(v2)로 교체**. 클래스 이름·번호·출력 모양은 v1과 **완전히 같으므로**(아래 2번) 파일만 바꾸면 됩니다.
3. 앱이 클래스 이름 `crack_iphone`, `iphon16_white`를 거부하는 문제(`04_Vision/ProductionLine.Vision/YoloOnnxInspector.cs`의 `ReadClassNames`, `from_local_pc_20261020/HANDOFF.md` 5번)가 아직 남아 있으면 앱 쪽에서 처리.
4. **실제 검사 확인:** 컨베이어에 폰을 올려 기종·불량 판정을 확인. 특히 아래 "남은 문제" 1번(**16 화이트**)을 꼭 보세요.
5. 확인하면서 **컨베이어 카메라 사진을 기종마다 10~20장 + 빈 벨트 사진**을 모아 두세요(라벨 있으면 더 좋음). 학습 PC로 보내면 다시 학습합니다(이 PC 기준 약 15분).
6. 결과를 `docs/AI_COLLAB_LOG.md`에 기록.

## 1. 인식률 (v1 대비, C# 앱과 같은 방식)

평가 방식: ONNX, 신뢰도 0.45, IoU 0.45, letterbox 114. 판정 = `crack_iphone` 박스가 하나라도 있으면 불량, 아니면 **가장 큰 박스**의 기종. 스크립트: `scripts/eval_v2.py`(아래 6번). 원본 출력: `results_v2_20261021.txt`, `results_old_vs_new_20261021.txt`.

### A. 보류 시험 사진 78장 — v1·v2 **둘 다 학습에 쓰지 않은** 사진

| 출처 | 정답 | v1 | **v2** |
|---|---|---:|---:|
| `old_datasets/iphone_12pro_silver_107` (실제 사진) | iphone_12_pro_silver | 1/33 = 3.0% | **33/33 = 100%** |
| `old_datasets/iphone_14_white_50` | iphone_14_white | 0/12 = 0% | **12/12 = 100%** |
| `old_datasets/iphone_16_white_72` | iphon16_white | 1/7 = 14.3% | **7/7 = 100%** |
| 파손 3종 (`crack_phone_61`, `iphone_12_pro_max_crack_60`, `broken_phone_extra`) | crack_iphone | 26/26 = 100% | 26/26 = 100% |
| **전체** | | **28/78 = 35.9%** | **78/78 = 100%** |

### B. 실제 컨베이어 카메라 사진 8장 (`from_local_pc_20261020/conveyor_real_8`, 모두 **정상** 폰)

| 사진 | 실제 | v1 판정 | **v2 판정** |
|---|---|---|---|
| WIN_20261017_10_57_11_Pro | 15 Pro | 15 Pro ✅ | 15 Pro ✅ |
| WIN_20261017_10_57_11_Pro_2 | 15 Pro | 15 Pro ✅ | 15 Pro ✅ |
| WIN_20261017_11_08_19_Pro | 15 Pro | 15 Pro ✅ | 15 Pro ✅ |
| WIN_20261017_11_08_25_Pro | 15 Pro | **불량** ❌ | 15 Pro ✅ |
| WIN_20261017_11_08_36_Pro | 16 화이트 | **불량** ❌ | **15 Pro** ❌ |
| WIN_20261017_11_08_42_Pro | 16 화이트 | **불량** ❌ | **15 Pro** ❌ |
| WIN_20261017_11_15_18_Pro | 정상 (기종 미기록) | 검출없음 | 15 Pro (확인 필요) |
| WIN_20261017_11_15_24_Pro | 정상, 스티커 (기종 미기록) | **불량** ❌ | 15 Pro (확인 필요) |
| **요약** | | 기종 3/6, 정상→불량 오판 **4/8** | 기종 **4/6**, 정상→불량 오판 **0/8** |

### C. 검은 화면: v1 `crack_iphone`(오판) → **v2 검출없음**

### 남은 문제 (v2)

1. **컨베이어 위 16 화이트를 15 Pro로 착각 (2장 중 2장).** 컨베이어 배경에서 찍은 16 화이트 사진이 학습에 없어서로 보입니다. 컨베이어 사진은 8장뿐이라 판단 근거도 적습니다. → 0번 5단계 사진으로 다시 학습.
2. 시험 사진에 **15 Pro 실제 사진이 없음**(옛 데이터에 없었음).
3. 보류 시험 78장은 같은 출처(같은 촬영 묶음)에서 뗀 것이라 100%는 실제보다 높을 수 있습니다.
4. **실제로 깨진 폰을 컨베이어에서 찍은 사진으로는 시험하지 않았습니다.**
5. 박스 기준 통일(`from_local_pc_20261020/HANDOFF.md` 4번)은 사용자 합의가 필요해 **하지 않았습니다**(박스는 원래대로).

### 학습 중 검증 점수 (val 199장, 학습과 거의 같은 사진 0장)

P 0.980, R 0.847, mAP50 0.967, mAP50-95 0.767 (79회째). v1(P 0.985, R 0.990, mAP50-95 0.853)보다 **낮아 보이지만**, v1 검증 176장 중 116장이 학습 사진과 거의 같은 사진이었기 때문입니다. v2 점수가 실제에 가깝습니다. 모델 비교는 위 A·B 표로 하세요.

## 2. 모델 파일과 앱 연결

| | 이름 | 모양 | 내용 |
|---|---|---|---|
| 입력 | `images` | `[1, 3, 640, 640]` float32 | RGB, 0~1, NCHW |
| 출력 | `output0` | `[1, 9, 8400]` | 후보 8400개 × (cx, cy, w, h + 클래스 점수 5). **NMS는 앱에서** |

- 파일: `models/best.onnx`(앱용, 약 10MB), `models/best.pt`(다시 학습할 때 출발점).
- 전처리: 비율 유지로 640에 맞추고 남는 곳을 회색 114로 채우는 letterbox.
- v1과 클래스·번호·출력 모양이 같습니다(실제 파일 메타데이터로 확인).

### 클래스 (번호 순서를 바꾸지 마세요)

| 번호 | 이름 | 뜻 |
|---|---|---|
| 0 | `crack_iphone` | **불량(깨진 폰)**. 기종과 상관없이 불량 검출용 (김하균님이 일부러 정한 이름) |
| 1 | `iphon16_white` | 아이폰16 화이트 (**e 빠진 철자 그대로**) |
| 2 | `iphone_12_pro_silver` | 아이폰12 Pro 실버 |
| 3 | `iphone15pro_Natural_Titanium` | 아이폰15 Pro 내추럴 티타늄 |
| 4 | `iphone_14_white` | 아이폰14 화이트 |

- 예전 기종별 모델(조장 툴로 만든 것)은 클래스 4개에 자기 폰이 **3번**이었습니다. 이 모델은 0~4번이고 `target_iphone / other_phone / crack` 클래스가 없습니다.

### 라벨 기준 (박스는 모두 그 폰의 클래스 번호)

| 클래스 | 큰 박스 | 작은 박스 |
|---|---|---|
| 기종 1~4 | 휴대폰 뒷면 전체 | 카메라 모듈 (뒷면 면적의 약 5~18%) |
| `crack_iphone` (all_phones 원래 사진) | 휴대폰 뒷면 전체 | 깨진 부위 (여러 개 가능) |
| `crack_iphone` (옛 데이터에서 온 사진, `old_` 로 시작) | 휴대폰 뒷면 전체 1개만 | 없음 |

그래서 한 폰에 같은 이름 박스가 2개 나오는 게 정상입니다. 앱은 가장 큰 박스를 폰 위치로 쓰면 됩니다.

### 속도

- 학습 PC GPU 기준 1장 약 2ms. 컨베이어 PC(CPU)는 1장 약 35ms(`from_local_pc_20261020/HANDOFF.md`). 모델은 가장 작은 `n` 크기 유지.

## 3. 데이터 (v2)

| 클래스 | train | val | test | 출처 |
|---|---:|---:|---:|---|
| 0 crack_iphone | 195 | 69 | 26 | all_phones v1 `crack_iphone` 225 + 옛 파손 3종 |
| 1 iphon16_white | 174 | 36 | 7 | all_phones v1 153 + 옛 `iphone_16_white_72` |
| 2 iphone_12_pro_silver | 176 | 48 | 33 | all_phones v1 150(컨베이어 사진 1장 합성) + 옛 `iphone_12pro_silver_107`(실제 사진) |
| 3 iphone15pro_Natural_Titanium | 163 | 27 | 0 | all_phones v1 191 |
| 4 iphone_14_white | 178 | 19 | 12 | all_phones v1 159 + 옛 `iphone_14_white_50` |
| **합계 1,163** | 886 | 199 | 78 | |

만든 방법 (`from_local_pc_20261020/HANDOFF.md` 4번의 2·3단계):
1. v1 878장 + `from_local_pc_20261020/old_datasets` 의 라벨 있는 사진을 합침. 옛 라벨 번호(3, `broken_phone_extra`는 3·4)를 HANDOFF 3번 표대로 0~4로 바꿈. **박스 위치는 그대로.**
2. **완전히 같은 파일 61장 제거**(SHA256, v1 쪽을 남김). 라벨 없는/빈 라벨 사진 10장 제외.
3. **거의 같은 사진(dHash 해밍거리 ≤ 4)을 한 묶음**으로 묶고, 묶음째로 나눔. 검증·시험 사진 중 학습 사진과 거의 같은 것 **0장**(v1은 검증 176장 중 116장).
4. **test**: 옛 데이터로만 된 묶음에서 출처마다 약 30%를 떼어 둠 → v1·v2 둘 다 본 적 없음.
5. **val**: 나머지에서 클래스·출처마다 약 20%(큰 연속 촬영 묶음은 train에 둠).

- 사진 이름: v1 사진은 그대로(`crack_iphone__...`, `iphone_16_white__...` 등), 옛 데이터는 `old_<폴더>__<원래이름>`.
- 분할 목록: `split.txt` (`파일이름<TAB>train|val|test`).
- `from_local_pc_20261020/old_datasets` 는 컨베이어 PC가 올린 원본이라 **그대로 두었습니다**(내용은 이제 `images/`에도 들어 있음). 그 폴더의 `eval_allphones.py`를 지금 `all_phones`에 돌리면 옛 사진이 학습에 들어 있어서 5번 결과가 "생략"으로 나오고, 4번 검증은 옛 방식 분할이라 의미가 없습니다. 평가는 `scripts/eval_v2.py`를 쓰세요.

## 4. 학습 설정

`yolo26n.pt`, 이미지 640, 300 에포크(patience 100 → **179회째 자동 종료, 79회째가 최고**), 배치 16, workers 8, 사진 RAM 캐시. 13분 걸림. v1과 다른 점은 데이터 로딩 속도 설정(workers, cache)뿐입니다.

## 5. 폴더 구성

```
all_phones/
├─ README.md                     이 문서
├─ classes.txt                   클래스 5개
├─ split.txt                     사진별 train/val/test
├─ make_dataset.py               split.txt 대로 dataset/ 만들기
├─ images/  labels/              사진·라벨 1,163장
├─ models/
│  ├─ best.onnx  best.pt         v2 모델
│  ├─ train_log.txt
│  └─ runs/iphone_detector/      results.csv, 그래프, weights/
├─ scripts/eval_v2.py            인식률 평가 (1번 표)
├─ results_v2_20261021.txt       eval_v2.py 출력
├─ results_old_vs_new_20261021.txt  v1·v2 비교 출력
├─ from_local_pc_20261020/       컨베이어 PC 점검 자료 (HANDOFF.md, 옛 데이터, 컨베이어 사진 8장)
└─ .gitignore                    dataset/ 제외
```

## 6. 다시 학습·평가하는 방법

```powershell
cd all_phones
py -3.13 make_dataset.py                                  # dataset/ (split.txt 그대로, 학습 때와 파일까지 같음 확인)
py -3.13 -c "from ultralytics import YOLO; YOLO('yolo26n.pt').train(data='dataset/data.yaml', epochs=300, imgsz=640, batch=16, device=0, workers=8, cache='ram', project='models_new/runs', name='iphone_detector')"
cd ..
py -3.13 -X utf8 all_phones/scripts/eval_v2.py --model all_phones/models/best.onnx --model <새 모델>.onnx   # 같은 시험 사진으로 비교
```

- Windows에서 `workers`를 쓰려면 위처럼 `-c` 또는 `if __name__ == "__main__":` 안에서 실행하세요.
- 새 사진은 `images/`, `labels/`에 넣으면 자동으로 train에 들어갑니다. 시험용으로 쓸 사진은 `split.txt`에 `test`로 적으세요.
- 새 기종은 `classes.txt` 맨 아래에 추가(5번부터). 위 순서는 바꾸지 마세요.
- `scripts/eval_v2.py`와 `make_dataset.py`는 이 저장소에서 실행해 위 표와 **같은 결과**가 나오는 것을 확인했습니다.
