# all_phones — 아이폰 5종 통합 YOLO 모델 (인수인계, **v3** · 2026-10-21)

> **이 문서를 받은 사람·Claude에게:** 폰 5종(불량 1 + 기종 4)을 **하나의 모델**로 학습한 데이터와 결과입니다.
> 이 폴더는 **v3 (`v3_merged_default`)** 입니다. v2는 컨베이어 PC에서 "컨베이어에 올리면 거의 15 Pro로 나옴" 문제가 있었고(`docs/AI_COLLAB_LOG.md` 10-21 컨베이어 PC 기록), 김하균님이 **우드락으로 덮은 컨베이어 환경**에서 사진을 찍어 라벨링해서 v2 데이터에 더해 다시 학습했습니다. **앱 모델을 v2 → v3로 교체하세요.**
> 컨베이어 PC에서는 **"0. 컨베이어 PC에서 할 일"** 순서대로 진행하세요.
>
> 학습: 2026-10-21, Claude(학습 PC, RTX 4070 Ti SUPER, Python 3.13, Ultralytics 8.4.183).

## 0. 컨베이어 PC에서 할 일 (순서대로)

1. `git pull` 로 최신 받기.
2. **앱 모델을 v2 → v3로 교체.** `04_Vision/models/best.onnx` 를 `all_phones/models/best.onnx`(v3, = `training_model/v3_merged_default/v3_merged_default.onnx`)로 바꾸고, `model_info.json`의 `sha256`을 아래 값으로 고칩니다. 빌드 출력 폴더에 복사된 옛 `best.onnx`가 있으면 그것도 바꾸세요.
   - v3 `best.onnx` SHA256: `ba0144a98da97dc9565f1deae905c429df6641265b38e9d68273da20de6c25c1`
   - **클래스 이름·번호·입출력 모양이 v2와 완전히 같습니다**(아래 2번). 앱 코드와 판정 규칙(10-21에 컨베이어 PC가 고친 것)은 그대로 쓰면 됩니다.
3. **실제 검사 확인:** 우드락 환경에서 폰을 기종마다 여러 번(가능하면 10번 이상) 올려 기종·불량 판정을 확인. 특히 **16 화이트, 14 화이트, 15 Pro**가 15 Pro로 쏠리지 않는지, **빈 벨트**에서 아무것도 안 잡히는지, **깨진 폰**이 불량으로 나오는지.
4. 틀린 장면이 있으면 그 화면을 캡처해 두고(기종 기록), 학습 PC로 보내 주세요.
5. 결과를 `docs/AI_COLLAB_LOG.md`에 기록.

## 1. 인식률 비교 (C# 앱과 같은 방식)

평가 방식: ONNX, 신뢰도 0.45, IoU 0.45, letterbox 114. 판정 = `crack_iphone` 박스가 하나라도 있으면 불량, 아니면 **가장 큰 박스**의 기종. 스크립트: `scripts/eval_v3.py`(아래 6번). 원본 출력: `results_v3_20261021.txt`, 후보 4개 비교 `../training_model/v3_merged_default/compare_result_20261021.txt`.

v3 후보 3개를 함께 학습해 비교했고 **`v3_merged_default`를 골랐습니다.**

| 시험 사진 (모두 학습·검증에 안 쓴 사진) | v2 | **v3_merged_default (채택)** | v3_merged_conveyor | v3_conveyor_only |
|---|---|---|---|---|
| **우드락 컨베이어 시험 74장 (전체)** | 21/74 = **28.4%** | **74/74 = 100%** | 74/74 | 74/74 |
| ├ 깨진 폰 20 (아이폰8 10 + 12 Pro Max 10) | 0/20 (검출없음) | 20/20 | 20/20 | 20/20 |
| ├ 16 화이트 20 | 7/20 (나머지 12 Pro로) | 20/20 | 20/20 | 20/20 |
| ├ 15 Pro 10 | 0/10 (12 Pro로) | 10/10 | 10/10 | 10/10 |
| ├ 12 Pro 실버 8 | 8/8 | 8/8 | 8/8 | 8/8 |
| ├ 14 화이트 10 | 0/10 (12 Pro로) | 10/10 | 10/10 | 10/10 |
| └ 빈 벨트 6 (검출없음이 정답) | 6/6 | 6/6 | 6/6 | 6/6 |
| 평균 최고 신뢰도 (기종별) | 0.71~0.89 | 0.92~0.94 | 0.92~0.94 | 0.94~0.96 |
| 예전 보류 시험 78장 (옛 데이터 출처) | 78/78 | 77/78 | 77/78 | **2/78** |
| 우드락 전 컨베이어 8장: 기종 맞음 | 4/6 | **6/6** | 5/6 | 3/6 |
| 우드락 전 컨베이어 8장: 정상→불량 오판 | 0 | 2 | 1 | 0 |
| 검은 화면 | 검출없음 | 검출없음 | 검출없음 | 검출없음 |
| 검증 점수 P / R / mAP50-95 | — | 0.979 / 0.973 / 0.834 | 0.987 / 0.962 / 0.830 | 0.999 / 1.000 / 0.915 |

- 후보 차이: `merged_default` = v2 데이터 + 컨베이어, Ultralytics 기본 증강(v2와 같음). `merged_conveyor` = 같은 데이터, 색 흔들기 작게(hsv_s 0.3, hsv_v 0.2)·좌우 뒤집기 끔. `conveyor_only` = 컨베이어 사진만. 후보 두 개는 학습 PC 바탕화면에만 있습니다.
- `conveyor_only`는 컨베이어 밖 사진을 거의 못 맞혀(2/78) 제외. `merged_default`와 `merged_conveyor`는 거의 같고, 우드락 전 컨베이어 사진 기종이 6/6인 `merged_default`를 채택. 실제 컨베이어에서 문제가 있으면 `merged_conveyor`가 예비 후보입니다.
- 우드락 전 사진 8장 중 `11_15_18`, `11_15_24`(카메라 2개짜리 흰 폰, 스티커, 기종 미기록)를 v3가 불량으로 판정합니다. 우드락 환경이 아니고 스티커가 붙은 폰이라 현재 환경과는 다르지만, 실제 검사에서 스티커 붙은 폰이 불량으로 나오는지 확인하세요.

### ⚠️ 주의
- 컨베이어 시험 사진은 김하균님이 학습용과 같은 촬영에서 고른 사진입니다. **같은 파일은 학습에서 뺐지만, 연속 촬영 사진(1초 간격)이 학습에 남아 있어** 100%는 실제보다 높을 수 있습니다. **실제 컨베이어 확인(0번 3단계)이 최종 판단 기준입니다.**
- 시험한 깨진 폰은 아이폰8, 12 Pro Max 두 대뿐입니다.

## 2. 모델 파일과 앱 연결

| | 이름 | 모양 | 내용 |
|---|---|---|---|
| 입력 | `images` | `[1, 3, 640, 640]` float32 | RGB, 0~1, NCHW |
| 출력 | `output0` | `[1, 9, 8400]` | 후보 8400개 × (cx, cy, w, h + 클래스 점수 5). **NMS는 앱에서** |

- 파일: `models/best.onnx`(앱용, 약 10MB), `models/best.pt`(다시 학습할 때 출발점). `training_model/v3_merged_default/`에 같은 파일이 `v3_merged_default.onnx/.pt` 이름으로 있습니다.
- 전처리: 비율 유지로 640에 맞추고 남는 곳을 회색 114로 채우는 letterbox.

### 클래스 (v1·v2·v3 모두 같음, 번호 순서를 바꾸지 마세요)

| 번호 | 이름 | 뜻 |
|---|---|---|
| 0 | `crack_iphone` | **불량(깨진 폰)**. 기종과 상관없음 — 아이폰8·12 Pro Max·기타 깨진 폰 모두 이 이름 |
| 1 | `iphon16_white` | 아이폰16 화이트 (**e 빠진 철자 그대로**) |
| 2 | `iphone_12_pro_silver` | 아이폰12 Pro 실버 |
| 3 | `iphone15pro_Natural_Titanium` | 아이폰15 Pro 내추럴 티타늄 |
| 4 | `iphone_14_white` | 아이폰14 화이트 |

### 라벨 기준 (박스는 모두 그 폰의 클래스 번호, 사진 1장에 박스 2개)

| 클래스 | 큰 박스 | 작은 박스 |
|---|---|---|
| 기종 1~4 | 휴대폰 뒷면 전체 | 카메라 모듈 (뒷면 면적의 약 5~18%) |
| `crack_iphone` | 휴대폰 뒷면 전체 | 깨진 부위 (옛 데이터에서 온 `old_` 사진 일부는 폰 전체 1개만) |
| 빈 벨트 | 없음 (빈 라벨) | |

### 속도
- 컨베이어 PC(CPU)에서 v2 기준 1장 약 60~70ms(C# 앱). v3는 같은 크기(`yolo26n`)라 같습니다.

## 3. 데이터 (v3)

| 클래스 | train | val | test(옛 출처) | 컨베이어 시험 (`test_conveyor/`) |
|---|---:|---:|---:|---:|
| 0 crack_iphone | 342 | 96 | 26 | 20 |
| 1 iphon16_white | 250 | 50 | 7 | 20 |
| 2 iphone_12_pro_silver | 268 | 64 | 33 | 8 |
| 3 iphone15pro_Natural_Titanium | 241 | 41 | 0 | 10 |
| 4 iphone_14_white | 246 | 31 | 12 | 10 |
| 빈 벨트 (박스 없음) | 20 | 4 | — | 6 |
| **합계 1,731장 + 시험 74장** | 1,367 | 286 | 78 | 74 |

- **v2 데이터 1,163장**(이전 README·`split.txt` 그대로) + **우드락 컨베이어 사진 568장**(`conv_` 로 시작).
- 컨베이어 사진 출처와 번호 변환 (김하균님 zip, 라벨은 옛 체계 `target_iphone, other_phone, crack, <기종>` 의 3번):

  | 이름 앞부분 | 원래 | → 번호 | 학습에 쓴 장수 |
  |---|---|---|---:|
  | `conv_16__` | 학습용 iphone_16_white | 1 | 90 |
  | `conv_15__` | 학습용 iphon15_pro_NaturalTitanium | 3 | 92 |
  | `conv_14__` | 학습용 iphone14_white | 4 | 80 (라벨 없는 10장 제외) |
  | `conv_12pro__` | 학습용 iphone_12_pro_silver | 2 | 108 |
  | `conv_crack8__` | 학습용 crack_iphone (깨진 아이폰8) | 0 | 90 |
  | `conv_12promax__` | 학습용 iphone 12_pro_max (깨진 12 Pro Max) | 0 | 84 (라벨 없는 6장 제외) |
  | `conv_empty__` | 빈 컨베이어 벨트 | 빈 라벨 | 24 (시험 6장 별도) |

- **시험용 사진과 완전히 같은 파일은 학습에서 뺐습니다**(16 화이트 20, 15 Pro 10, 14 화이트 10, 12 Pro 8(시험 10장 중 중복 2), 깨진 폰 20).
- 컨베이어 사진의 val: 출처마다 **촬영 시간 순 마지막 약 15%**(연속 묶음째).
- 박스 위치는 라벨 그대로, 번호만 바꿈.

## 4. 학습 설정

`yolo26n.pt`, 이미지 640, 300 에포크(patience 100 → **248회째 자동 종료, 148회째가 최고**), 배치 16, Ultralytics 기본 증강(hsv_h 0.015, hsv_s 0.7, hsv_v 0.4, fliplr 0.5, mosaic 1.0), workers 6, RAM 캐시. 세 후보를 GPU 하나로 동시에 학습해 약 66분. `models/settings.json` 참고.

## 5. 폴더 구성

```
all_phones/
├─ README.md                     이 문서
├─ classes.txt                   클래스 5개
├─ split.txt                     사진별 train/val/test
├─ make_dataset.py               split.txt 대로 dataset/ 만들기 (빈 라벨 = 배경 사진 포함)
├─ images/  labels/              사진·라벨 1,731장 (v2 1,163 + 컨베이어 568)
├─ test_conveyor/<클래스>/        우드락 컨베이어 시험 사진 74장 (라벨 없음, 폴더 이름이 정답)
├─ models/
│  ├─ best.onnx  best.pt         v3 (v3_merged_default)
│  ├─ settings.json  train_log.txt
│  └─ runs/iphone_detector/      results.csv, 그래프, weights/
├─ scripts/eval_v2.py            (v2 때 평가, Detector·판정 함수 제공)
├─ scripts/eval_v3.py            v2 ↔ v3 비교 (1번 표)
├─ results_v3_20261021.txt       eval_v3.py 출력
├─ results_v2_20261021.txt, results_old_vs_new_20261021.txt   v2 때 기록
├─ from_local_pc_20261020/       컨베이어 PC 점검 자료 (v1 때)
└─ .gitignore                    dataset/ 제외
```

## 6. 다시 학습·평가하는 방법

```powershell
cd all_phones
py -3.13 make_dataset.py        # dataset/ (split.txt 그대로 — 이번 학습과 사진·라벨까지 같은 것 확인)
py -3.13 -c "from ultralytics import YOLO; YOLO('yolo26n.pt').train(data='dataset/data.yaml', epochs=300, imgsz=640, batch=16, device=0, workers=6, cache='ram', project='models_new/runs', name='iphone_detector')"
cd ..
py -3.13 -X utf8 all_phones/scripts/eval_v3.py --model all_phones/models/best.onnx --model <새 모델>.onnx
```

- 새 사진은 `images/`, `labels/`에 넣으면 train으로 들어갑니다. 검증·시험용은 `split.txt`에 적거나 `test_conveyor/<클래스>/`에 넣으세요. **시험 사진은 학습 사진과 다른 촬영(폰을 다시 올려서)** 으로 찍는 게 좋습니다.
- 새 기종은 `classes.txt` 맨 아래에 추가(5번부터). 위 순서는 바꾸지 마세요.
- `make_dataset.py`와 `scripts/eval_v3.py`는 이 저장소에서 실행해 위 표와 **같은 결과**가 나오는 것을 확인했습니다.

## 남은 문제

1. 실제 컨베이어 실물 시험 미실시 (0번 3단계).
2. 컨베이어 시험 사진이 학습 사진과 같은 촬영 → 점수 낙관적.
3. 우드락 전 사진의 스티커 붙은 흰 폰 2장을 불량으로 판정.
4. 15 Pro는 옛 출처 시험 사진이 없음.
5. 박스 기준 통일(`from_local_pc_20261020/HANDOFF.md` 4번)은 합의가 필요해 하지 않았음.
