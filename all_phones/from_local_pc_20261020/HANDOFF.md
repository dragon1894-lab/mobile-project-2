# from_local_pc_20261020 — 인식률 개선용 인수인계 (컨베이어 PC → 학습 PC)

> **이 문서를 받은 Claude에게:** 컨베이어 PC(Intel 내장그래픽, GPU 없음)에서 Claude가 2026-10-20에 한 점검 결과입니다.
> 목표는 **`all_phones` 모델의 실제 인식률을 올리는 것**입니다. 이 폴더에는 `all_phones`에 **들어가지 않은** 옛 학습 사진, 실제 컨베이어 사진, 평가 스크립트와 결과가 있습니다.
> 아래 "4. 할 일" 순서대로 진행하고, 결과(특히 5번 표의 수치)를 사용자에게 보고하세요.
> 이 폴더는 `all_phones/images`, `all_phones/labels`와 **분리**되어 있어 `make_dataset.py`에 자동으로 섞이지 않습니다. 합칠 때는 번호를 바꿔서 직접 복사해야 합니다(3번 표).

## 1. 결론 요약

1. `all_phones` 모델은 **검증 176장에서 100%** 맞지만, **학습에 안 쓴 다른 사진에서는 정상 기종을 거의 못 맞춥니다**(아래 표). 즉 학습 사진에 과적합된 상태입니다.
2. 이유: 검증 176장 중 **116장이 학습 사진과 거의 같은 사진**(dHash 해밍거리 ≤ 4), 16장은 **완전히 같은 파일**입니다. `make_dataset.py`가 파일 이름 해시로 나누기 때문에 같은 촬영의 연속 사진이 학습·검증에 갈라져 들어갑니다. `images/` 안에 완전히 같은 파일 중복도 61개 있습니다.
3. 파손(`crack_iphone`)은 다른 출처 파손 사진도 98~100% 잡지만, **실제 컨베이어의 정상 폰 4장/8장을 파손으로 오검출**합니다(스티커·카메라 반사 부위). 정상 폰·빈 벨트 사진이 학습에 없어서입니다. 검은 화면에서도 `crack_iphone`이 나옵니다.
4. 이 PC의 옛 모델(기종별로 따로 학습, 모두 클래스 3번 하나만 학습)은 쓰레기라 앱에서 빼기로 했습니다. **옛 사진·라벨은 번호만 바꾸면 재사용 가능**하며 그게 이 폴더입니다.

## 2. 평가 결과 (`best.onnx`, 신뢰도 0.45, IoU 0.45, letterbox 114 — C# 앱과 같은 전처리)

판정 규칙: `crack_iphone`이 하나라도 있으면 불량, 아니면 **가장 큰 박스**의 기종.

| 시험 사진 | 장수 | 기대 결과 | 맞음 | 실제 결과 분포 |
|---|---:|---|---:|---|
| all_phones 검증 분할 | 176 | 각 정답 | **100%** | 전부 정답 |
| 위에서 학습 사진과 거의 같은 것 제외 | 60 | 각 정답 | 100% | 전부 정답 (그래도 같은 출처) |
| `old_datasets/iphone_12pro_silver_107` (실제 사진) | 107 | iphone_12_pro_silver | **0.9%** | 15Pro 62, 16white 12, 검출없음 25, crack 7 |
| `old_datasets/iphone_14_white_50` | 50 | iphone_14_white | **0%** | 16white 35, 15Pro 15 |
| `old_datasets/iphone_16_white_72` (중복 4장 뺀 68장) | 68 | iphon16_white | **5.9%** | 14white 41, 15Pro 19, crack 3, 검출없음 1 |
| `old_datasets/crack_phone_61` | 61 | crack_iphone | 98.4% | 1장 검출없음 |
| `old_datasets/iphone_12_pro_max_crack_60` | 60 | crack_iphone | 100% | |
| `conveyor_real_8` | 8 | 아래 표 | — | 파손 오검출 4 |
| 빈 벨트 사진 / 검은 화면 | 1 / 1 | 검출없음 | 1 / **0** | 검은 화면에서 crack_iphone |

`all_phones`의 12 Pro 실버는 **컨베이어 사진 1장을 합성한 150장**이라, 실제 다른 12 Pro 실버 사진(107장)은 거의 못 맞춥니다. 이 107장이 가장 중요한 추가 자료입니다.

### 실제 컨베이어 사진 8장 (모두 **정상** 폰, 깨진 폰 없음)

| 파일 | 실제 기종 (옛 라벨·눈으로 확인) | 모델 결과 (클래스, 신뢰도, 박스 폭 px) |
|---|---|---|
| WIN_20261017_10_57_11_Pro(_2) | 15 Pro 계열(트리플 카메라, 내추럴 티타늄) | 15Pro 0.93 / 15Pro 0.89(카메라 박스) → 정답 |
| WIN_20261017_11_08_19_Pro | 15 Pro | 15Pro 0.92 → 정답 |
| WIN_20261017_11_08_25_Pro | 15 Pro | 15Pro 0.93 + **crack 0.62** → 불량 오판 |
| WIN_20261017_11_08_36_Pro | 아이폰 16 | 16white 0.95 + **crack 0.59** → 불량 오판 |
| WIN_20261017_11_08_42_Pro | 아이폰 16 | 16white 0.88 + **crack 0.62** → 불량 오판 |
| WIN_20261017_11_15_18_Pro | 기종 미기록 (바로 다음 사진과 같은 촬영으로 추정 — 눈으로 확인 안 함) | 검출없음 |
| WIN_20261017_11_15_24_Pro | 같은 흰색 폰, 스티커 부착, 정상 | **crack 0.68** → 불량 오판 |

`conveyor_real_8`에는 라벨이 없습니다(옛 라벨은 클래스 체계가 달라 뺐음). 학습에 넣으려면 새로 라벨링하세요.

## 3. 이 폴더의 옛 데이터 → all_phones 번호 변환

all_phones 클래스: `0 crack_iphone, 1 iphon16_white, 2 iphone_12_pro_silver, 3 iphone15pro_Natural_Titanium, 4 iphone_14_white`

| 폴더 | 장수 | 옛 라벨 번호 | 바꿀 번호 | 박스 모양 |
|---|---:|---|---|---|
| `old_datasets/iphone_12pro_silver_107` | 107 | 3 | **2** | 폰 전체 + 카메라 모듈 (2개) |
| `old_datasets/iphone_14_white_50` | 50 | 3 | **4** | 폰 전체 + 카메라 모듈 (2개) |
| `old_datasets/iphone_16_white_72` | 68 | 3 | **1** | 폰 전체 + 카메라 모듈 (2개). `preview.jpg`는 빈 라벨 |
| `old_datasets/iphone_12_pro_max_crack_60` | 60 | 3 | **0** | 폰 전체 1개 (깨진 부위 박스 없음) |
| `old_datasets/crack_phone_61` | 61 | 3 | **0** | 폰 전체 1개 |
| `old_datasets/broken_phone_extra` | 10 | 3 또는 4(`broken_phone`) | **0** | 폰 전체 1개 |

- 각 폴더의 `classes.txt` / `data.yaml`이 옛 번호 체계입니다(0 target_iphone, 1 other_phone, 2 crack, 3 해당 폰).
- 모든 파일은 SHA256으로 확인해 **all_phones와 완전히 같은 파일은 이미 뺐습니다**. 거의 같은 사진은 소수 남아 있습니다(12Pro 4, 14white 2, 16white 17, 파손 4+3).
- 파손 라벨은 all_phones(폰 전체 + 깨진 부위 여러 개)와 기준이 다릅니다(폰 전체 1개). 그대로 합쳐도 학습은 되지만 기준 통일을 권장합니다.

## 4. 할 일 (순서대로)

1. **같은 결과가 나오는지 확인** (저장소 최상위 폴더에서, 경로 수정 없이 바로 실행됨. CPU로 약 2~3분):
   ```powershell
   pip install numpy opencv-python onnx onnxruntime   # 없을 때만
   python -X utf8 all_phones/from_local_pc_20261020/scripts/eval_allphones.py
   ```
   `results/all_phones_eval_20261020.txt`(컨베이어 PC 출력)와 같은 수치가 나와야 합니다. 이 저장소 구조 그대로 컨베이어 PC에서 실행해 2번 표와 같은 결과를 확인했습니다.
   새로 학습한 모델은 `--model 새모델.onnx --data 새데이터폴더`로 같은 방식으로 평가합니다(`--data` 폴더에 `images/ labels/ classes.txt` 필요).
   주의: 5번 "컨베이어 실사진 8장" 줄의 "기대 15 Pro"는 편의상 값입니다. 실제 정답은 2번의 사진별 표를 보세요.
2. **데이터 합치기**: `all_phones/images`, `labels`를 건드리지 말고 새 폴더(예: `all_phones_v2/`)에 all_phones + 이 폴더 데이터를 3번 표대로 번호를 바꿔 합칩니다.
3. **분할을 출처·촬영 단위로**: 파일 이름 해시 대신, 원래 폴더(출처) 또는 거의 같은 사진 묶음(dHash 군집) 단위로 학습/검증을 나눠 **거의 같은 사진이 양쪽에 들어가지 않게** 합니다. 완전히 같은 파일 61개 중복도 제거합니다. 가능하면 출처 하나를 통째로 테스트용으로 남겨 두세요(예: `old_datasets/iphone_12pro_silver_107` 일부).
4. **(권장) 박스 기준 통일**: 기종 사진의 카메라 모듈 박스(폰 박스 안, 면적 5~18%)를 지우고 폰 전체 박스 1개로. 파손은 폰 전체를 `crack_iphone` 1개로 하거나 깨진 부위를 **다른 번호**(예: 5 `crack_area`)로 분리. 지금은 같은 이름 박스가 2개씩 나와서 앱에서 위치·개수 처리가 복잡해집니다. 사용자와 합의 후 진행하세요.
5. **빈 라벨 사진 추가**: 빈 벨트, 검은 화면, 손 등 박스 없는 사진. `conveyor_real_8`처럼 정상 폰인데 파손으로 나온 사진은 정답 기종으로 라벨링해서 넣으면 파손 오검출이 줄어듭니다.
6. **재학습**: `all_phones/README.md` 6번의 명령과 같은 설정(yolo26n, 640). `--device`는 지정하지 않아도 Ultralytics가 GPU를 자동 사용합니다(사용자 요청: CUDA 별도 설정 불필요).
7. **평가**: 2번 표와 같은 방식으로, 특히 **학습에 안 쓴 출처 사진**과 `conveyor_real_8`에서 다시 재고 이전 수치와 비교해 보고하세요. 검증 분할 점수만으로 판단하지 마세요.

## 5. 앱(C#)에 연결할 때 주의 (컨베이어 PC에서 처리 예정)

- 현재 `04_Vision/ProductionLine.Vision/YoloOnnxInspector.cs`의 `ReadClassNames`는 정해진 이름만 허용합니다. **`crack_iphone`과 `iphon16_white`(e 빠짐)는 거부되어 모델 로딩이 실패**합니다. 클래스 이름을 바꿔 재학습할 경우 사용자와 합의하고 알려 주세요(바꾸지 않아도 앱 쪽에서 처리 가능).
- 출력 `[1, 9, 8400]`은 문제없습니다(코드가 `4 + 클래스 수`로 계산).
- 컨베이어 PC는 GPU가 없습니다(CPU 추론, Python 기준 1장 약 35ms). 모델은 `n` 크기를 유지하세요.

## 6. 폴더 구성

```
from_local_pc_20261020/
├─ HANDOFF.md                     이 문서
├─ old_datasets/                  all_phones에 없는 옛 학습 사진·라벨 (옛 번호 체계)
│  ├─ iphone_12pro_silver_107/    images/, labels/, classes.txt
│  ├─ iphone_14_white_50/
│  ├─ iphone_16_white_72/
│  ├─ iphone_12_pro_max_crack_60/
│  ├─ crack_phone_61/
│  └─ broken_phone_extra/
├─ conveyor_real_8/images/        실제 컨베이어 카메라 사진 8장 (라벨 없음)
├─ scripts/
│  ├─ eval_allphones.py           데이터 점검 + 인식률 평가 (2번 표를 만든 스크립트)
│  ├─ audit_data.py               데이터셋 라벨·중복·누수 점검
│  ├─ cross.py                    여러 모델 교차 추론 (옛 모델 문제 확인용)
│  └─ boxes.py                    박스 개수·포함 관계 분석
└─ results/
   └─ all_phones_eval_20261020.txt  eval_allphones.py 실행 원본 출력
```
