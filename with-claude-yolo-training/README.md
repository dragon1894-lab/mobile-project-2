# 깨진 아이폰(crack_iphone) YOLO 학습 — Claude 인수인계

> **이 문서를 받은 Claude에게:** 아래 "정해진 것"은 사용자(김하균)가 이미 결정한 내용입니다. 다시 묻지 말고 그대로 진행하세요. "시작 전 확인"의 몇 가지만 사용자에게 물어본 뒤 "진행 순서"대로 하면 됩니다.
> 작성: 2026-10-20, 다른 Claude 세션(김하균 노트북). 이 폴더의 코드는 조장 문성찬이 만든 학습 툴을 그대로 가져온 것입니다.

## 0. 사용자와 일하는 방식
- 사용자: **김하균** (2차 프로젝트 서버·DB·통신·통합 담당, 코딩 초보). **쉬운 한국어로 짧게** 설명하세요.
- 프로그램 **설치, 삭제, 덮어쓰기 전에는 무엇을 할지 말하고 허락을 받으세요.** 사용자 사진 원본은 절대 지우거나 바꾸지 마세요.
- 이번 작업은 **학습만** 합니다. 앱·서버 코드(`with-claude/` 폴더)는 건드리지 않습니다.

## 1. 정해진 것 (다시 묻지 말 것)
| 항목 | 결정 |
|---|---|
| 학습 대상 | **깨진 아이폰 사진만.** 사용자가 이 PC에 올리는 한글 폴더 `아이폰 깨짐` (약 150~200장) |
| 방식 | YOLO **검출(박스)** 학습. 조장 문성찬의 학습 툴 `06_TrainingTool` 틀을 그대로 사용 |
| 클래스 (`classes.txt`) | `0 target_iphone` / `1 other_phone` / `2 crack` / `3 crack_iphone` — 이 순서 그대로. 0·1번은 조장 틀과 맞추려고 남겨 둔 것이고 이번엔 안 씀 |
| 박스 (사진 한 장에 2종류) | **휴대폰 뒷면 전체 = `crack_iphone`**, **깨진 부위 = `crack`** (깨진 곳이 여러 군데면 `crack` 박스 여러 개) |
| 라벨링 | 사용자가 학습 툴 창에서 **20~30장을 직접** 그림 → Claude가 도우미 모델로 **나머지를 자동 라벨** → 사용자가 툴에서 훑어보며 수정 |
| 학습 횟수 | **300** (에포크 = 사진 전체를 300바퀴). 이미지 크기 640, 기본 모델 `yolo26n.pt` |
| 결과 저장 | **바탕화면\crack_iphone** 폴더 (학습 툴 프로젝트 폴더로 사용 → 사진·라벨·모델이 한곳에 남음) |
| 모델 개수 | 1개 (이번엔 폴더가 하나뿐) |

> 이름 결정 배경: 조장이 10-20에 넣은 모델 `12_아이폰12프로맥스_파손`이 `target_iphone, other_phone, crack, iphone_12_pro_max_Crack` 구조(뒷면 전체 + 깨진 부위)라서 같은 틀로 4번째 이름만 `crack_iphone`으로 정했습니다. 두 박스를 같은 이름으로 하면 모델이 헷갈리므로 깨진 부위는 `crack`입니다.

## 2. 시작 전 확인 (사용자에게 물어볼 것)
1. `아이폰 깨짐` 사진 폴더가 **이 PC 어디에** 있는지 (보통 바탕화면).
2. 사진 형식 — `.HEIC`가 섞여 있으면 JPG로 바꿔야 합니다(3단계 스크립트가 처리, `pillow-heif` 설치 필요).
3. 이 PC에 **NVIDIA 그래픽카드**가 있는지 (있으면 GPU 학습으로 훨씬 빠름. 없으면 CPU로 2~4시간 이상).
4. 설치 허락 — 이 PC에는 개발 도구가 하나도 없음(Visual Studio도 없음). 1단계 표의 Git, GitHub CLI(로그인은 사용자가 직접), .NET 8 SDK, Python 3.13, YOLO 패키지를 설치해야 함.

## 3. 이 폴더 구성
```
with-claude-yolo-training/
├─ README.md            ← 이 문서
├─ classes.txt          ← 이번 클래스 4개 (프로젝트 폴더에 복사해서 사용)
├─ fix_orientation.py   ← 사진 정리: EXIF 회전 적용 + JPG 변환 + 영어 이름 (툴에 넣기 전에 꼭)
├─ autolabel.py         ← 도우미 모델로 나머지 사진 자동 라벨
└─ 06_TrainingTool/     ← 조장 문성찬의 학습 툴 (C# WinForms + train_yolo.py), 원본 그대로
    ├─ README.md        ← 툴 원래 사용법
    └─ ProductionLine.TrainingTool/
        ├─ MainForm.cs          화면·버튼, Python 실행
        ├─ YoloProject.cs       프로젝트 폴더·라벨 저장·학습 데이터 만들기
        ├─ AnnotationCanvas.cs  사진 위에 박스 그리기
        ├─ Program.cs
        ├─ ProductionLine.TrainingTool.csproj   (.NET 8, 다른 프로젝트 참조 없음 → 혼자 빌드됨)
        └─ train_yolo.py        Ultralytics 학습 + ONNX 변환 (빌드 시 실행 폴더로 복사됨)
```
`fix_orientation.py`, `autolabel.py`는 Python이 없는 PC에서 작성해 **실행 시험은 안 했습니다.** 처음엔 몇 장으로 먼저 확인하세요.

## 4. 학습 툴 동작 (알고 있어야 할 것)
- **새 프로젝트 / 프로젝트 열기:** 폴더를 고르면 `images\`, `labels\`, `models\`, `classes.txt`를 씀. `classes.txt`가 **이미 있으면 그대로 사용**, 없으면 기본 3개(`target_iphone, other_phone, crack`)를 만듦 → **반드시 이 폴더의 `classes.txt`를 먼저 복사해 둘 것.**
- **사진 가져오기:** 고른 사진을 `images\`로 **복사**(이름이 겹치면 `_1`을 붙임). JPG·JPEG·PNG·BMP만.
- **라벨링:** 오른쪽에서 클래스를 고르고 사진 위를 드래그. **라벨 저장** 또는 `Ctrl+S` → `labels\사진이름.txt` (`클래스번호 cx cy w h`, 0~1 비율). 박스 없이 저장하면 빈 파일(= 찾을 것 없음).
- **기존 라벨 불러오기:** `labels\`에 파일이 있으면 사진을 열 때 박스를 보여 줌 → **자동 라벨을 툴에서 그대로 검토·수정 가능.**
- **학습 데이터 만들기:** 라벨 파일이 있는 사진만 모아 `dataset\`을 **지우고 새로** 만듦(검증 20%, 파일 이름 해시로 고정 분할) + `dataset\data.yaml`. 라벨 없는 사진은 빠짐.
- **YOLO 학습 + ONNX 변환:** `train_yolo.py --data dataset\data.yaml --model <기본 모델> --epochs <학습 횟수> --imgsz <이미지 크기> --output models\ [--device]` 실행. 결과 `models\best.pt`, `models\best.onnx`, `models\training_result.json`, 그래프는 `models\runs\iphone_detector\`.
- **주의 — 사진 회전:** 툴은 사진의 EXIF 회전 정보를 **무시**하고 보여 주지만(`Image.FromStream`), YOLO(OpenCV)는 **적용**해서 읽습니다. 세로 사진이면 박스가 어긋나므로 **3단계 `fix_orientation.py`로 먼저 정리**합니다.
- Python 경로: 툴은 `%LOCALAPPDATA%\Programs\Python\Python313\python.exe`, `Python314` 등 정해진 위치만 자동으로 찾음(**3.12 위치는 목록에 없음** → 그래서 3.13 설치를 권장). 못 찾으면 화면의 "Python 실행 파일" 칸에 실제 경로를 넣음. `WindowsApps\python.exe`(스토어 바로가기)는 실제 Python이 아님.

## 5. 진행 순서
아래 경로 예시의 `$desk`는 바탕화면입니다: PowerShell `$desk = [Environment]::GetFolderPath('Desktop')` (OneDrive면 `...\OneDrive\바탕 화면`).

### 1단계. 환경 준비 — 이 PC에는 아무것도 설치돼 있지 않음 (설치마다 허락)
사용자 말: 학습할 PC에는 **Visual Studio 2022를 포함해 개발 도구가 하나도 없음.** 아래를 순서대로 확인하고, 없는 것만 허락을 받아 설치합니다.

**Visual Studio 2022는 필요 없습니다.** Visual Studio는 코드 편집 프로그램일 뿐이고, 학습 툴(C#)은 **.NET 8 SDK**만 있으면 `dotnet build`로 빌드되고 `.exe`로 실행됩니다(10-20 김하균 노트북에서 Visual Studio 없이 빌드 확인). 사용자가 원하면 깔아도 되지만 학습에는 불필요합니다.

| # | 설치할 것 | 왜 필요한가 | 확인 명령 | 설치 명령 (winget) |
|---|---|---|---|---|
| 0 | Claude (데스크톱 앱 또는 Claude Code) | 이 작업을 하는 Claude 자체 | — | 사용자가 직접 |
| 1 | **Git** | 저장소 받기. Windows용 Claude Code도 Git Bash가 필요 | `git --version` | `winget install --id Git.Git -e` |
| 2 | **GitHub CLI + 로그인** | 저장소가 **비공개**라 로그인해야 받을 수 있음 | `gh auth status` | `winget install --id GitHub.cli -e` → `gh auth login` (**로그인은 사용자가 직접**, 계정 `dragon1894-lab`) |
| 3 | **.NET 8 SDK** | 학습 툴(C#) 빌드·실행 | `dotnet --list-sdks` (8.x가 있어야 함) | `winget install --id Microsoft.DotNet.SDK.8 -e` |
| 4 | **Python 3.13** | YOLO 학습(`train_yolo.py`). 툴이 3.13 설치 위치를 자동으로 찾음 | `py -3.13 --version` | `winget install --id Python.Python.3.13 -e` |
| 5 | **YOLO 패키지** | Ultralytics(PyTorch 포함, 수 GB), ONNX 변환, HEIC 변환 | 아래 확인 명령 | `py -3.13 -m pip install --upgrade ultralytics onnx pillow-heif` |
| 6 | (NVIDIA 그래픽카드가 있을 때만) 최신 드라이버 + CUDA용 PyTorch | GPU로 빠르게 학습 | `nvidia-smi` | pytorch.org 안내 명령으로 torch를 **5번보다 먼저** 설치 |

- `winget`이 없으면 각 공식 사이트 설치 파일로 설치합니다. Python 설치 파일을 쓰면 **"Add python.exe to PATH"를 체크**하도록 사용자에게 안내합니다.
- 설치 후 **새 터미널을 열어야** `git`, `dotnet`, `py` 명령이 잡힙니다.
- `python`만 치면 `WindowsApps\python.exe`(스토어 바로가기)가 잡힐 수 있습니다. 아래 명령의 `python`은 **`py -3.13`**(또는 `%LOCALAPPDATA%\Programs\Python\Python313\python.exe`)으로 바꿔 실행하세요.
- 확인: `py -3.13 -c "import torch, ultralytics; print(torch.__version__, ultralytics.__version__, torch.cuda.is_available())"`
- PyTorch가 Python 3.13용 설치 파일을 못 찾는 오류가 나면 Python 3.12로 바꾸고, 학습 툴의 "Python 실행 파일" 칸에 `...\Python312\python.exe` 경로를 직접 넣습니다.
- 저장소 받기: `gh repo clone dragon1894-lab/mobile-project-2` → 받은 폴더(`mobile-project-2`) 안에서 아래 명령을 실행합니다. 이 폴더(`with-claude-yolo-training`)만 쓰고 `with-claude/`는 건드리지 않습니다.
- 디스크: 최소 10GB 여유 권장 (PyTorch, 학습 결과).

### 2단계. 프로젝트 폴더 만들기
```powershell
New-Item -ItemType Directory "$desk\crack_iphone" | Out-Null
Copy-Item ".\with-claude-yolo-training\classes.txt" "$desk\crack_iphone\classes.txt"
```
`$desk\crack_iphone`이 이미 있으면 덮어쓰지 말고 사용자에게 물어볼 것.

### 3단계. 사진 정리 (원본은 그대로 둠)
```powershell
python .\with-claude-yolo-training\fix_orientation.py --src "$desk\아이폰 깨짐" --dst "$desk\crack_iphone_사진정리"
```
몇 장을 열어 방향이 맞는지 확인. 건너뛴 파일이 있으면 사용자에게 알림.

### 4단계. 학습 툴 빌드·실행 → 사용자가 20~30장 라벨링
```powershell
dotnet build ".\with-claude-yolo-training\06_TrainingTool\ProductionLine.TrainingTool\ProductionLine.TrainingTool.csproj"
Start-Process ".\with-claude-yolo-training\06_TrainingTool\ProductionLine.TrainingTool\bin\Debug\net8.0-windows\ProductionLine.TrainingTool.exe"
```
사용자에게 안내:
1. **프로젝트 열기** → `바탕화면\crack_iphone` (오른쪽 클래스 목록에 `crack_iphone`이 보이는지 확인)
2. **사진 가져오기** → `crack_iphone_사진정리` 폴더의 사진 전부
3. 서로 다른 모습의 사진 **20~30장**에: 휴대폰 뒷면 전체 = `crack_iphone`, 깨진 부위 = `crack` 박스 → `Ctrl+S`
4. 끝나면 알려 달라고 하기

### 5단계. 도우미 모델 학습 (짧게)
1. 사용자에게 툴의 **학습 데이터 만들기** 클릭을 부탁(라벨한 20~30장만 들어감).
2. 명령줄로 짧게 학습 (결과를 `helper_model`에 따로 저장해서 본 학습 결과와 섞이지 않게):
```powershell
python ".\with-claude-yolo-training\06_TrainingTool\ProductionLine.TrainingTool\train_yolo.py" --data "$desk\crack_iphone\dataset\data.yaml" --model yolo26n.pt --epochs 80 --imgsz 640 --output "$desk\crack_iphone\helper_model"
```
(`yolo26n.pt`를 못 받으면 `yolo11n.pt`로 바꾸고 사용자에게 알림. GPU면 `--device 0`, CPU면 생략 또는 `--device cpu`.)

### 6단계. 나머지 자동 라벨 → 사용자 검토
```powershell
python .\with-claude-yolo-training\autolabel.py --project "$desk\crack_iphone" --model "$desk\crack_iphone\helper_model\best.pt"
```
- 사람이 그린 라벨은 건드리지 않음. 못 찾은 사진은 라벨 파일을 안 만듦.
- 사용자에게: 툴에서 `autolabel_list.txt`의 사진을 **하나씩 열어** 박스를 고치고 `Ctrl+S`, 못 찾은 사진은 직접 그리기. **이 검토를 건너뛰지 말 것** (틀린 박스로 학습하면 인식률이 떨어짐).

### 7단계. 본 학습 (300회)
1. 사용자에게 **학습 데이터 만들기** 다시 클릭 부탁(이제 전체 사진).
2. 학습 (2~4시간 이상 걸릴 수 있으니 **백그라운드로 실행하고 진행을 확인**, 노트북 **절전 방지**):
```powershell
python ".\with-claude-yolo-training\06_TrainingTool\ProductionLine.TrainingTool\train_yolo.py" --data "$desk\crack_iphone\dataset\data.yaml" --model yolo26n.pt --epochs 300 --imgsz 640 --output "$desk\crack_iphone\models"
```
   - 툴의 **YOLO 학습 + ONNX 변환** 버튼(학습 횟수 300, 이미지 크기 640)과 같은 동작입니다. 사용자가 툴로 돌려도 됩니다.
   - 점수가 100회 동안 안 오르면 Ultralytics가 **자동으로 일찍 멈춤**(정상). 가장 좋았던 결과가 `best.pt`.
   - 마지막에 `TRAINING_AND_EXPORT_COMPLETED`가 나오면 성공.

### 8단계. 결과 확인·보고
- 파일: `$desk\crack_iphone\models\best.pt`, `best.onnx`, `training_result.json`, `runs\iphone_detector\` (`results.csv`, `results.png`, `confusion_matrix.png`, `val_batch0_pred.jpg`)
- `results.csv`에서 가장 좋은 줄의 **mAP50, mAP50-95, precision, recall**을 `crack_iphone`·`crack`별로 쉬운 말로 설명.
- 학습에 안 쓴 사진 몇 장으로 `best.pt`를 돌려 박스 그림을 사용자에게 보여 주기.
- 금이 가늘어서 `crack`을 잘 못 잡으면: 이미지 크기 960으로 다시 학습 제안(더 오래 걸림).
- 기록: 저장소 `docs/AI_COLLAB_LOG.md`에 날짜·사진 수·설정·점수·남은 문제를 적고, 이 README 맨 아래 "결과" 칸 채우기. **모델(.pt/.onnx)과 사진은 GitHub에 올리지 않음.**

## 6. 주의·메모 (사용자와 합의한 것)
- **정상 폰 사진이 없음:** 이번 모델은 깨진 폰만 배워서, 혼자 쓰면 **멀쩡한 폰도 `crack_iphone`으로 잡을 수 있음.** 정상 폰은 다른 조원이 따로 학습 중 → 나중에 **사진·라벨을 합쳐 하나로 다시 학습**하면 해결. 그래서 프로젝트 폴더(사진·라벨)를 통째로 보관할 것.
- **앱 연결은 나중:** 지금 앱 검사 코드(조장 `04_Vision`)는 `crack`, `crack_phone`, `iphone_12_pro_max_Crack` 등 정해진 이름만 알아들음. `crack_iphone`은 모르므로 앱에 넣으려면 조장 코드에 이름 추가가 필요(이번 범위 아님).
- **클래스 순서를 바꾸지 말 것:** 라벨 번호가 꼬임.
- **비슷한 연속 사진만 많으면** 점수만 높고 실제로는 못 잡음. 다양한 사진인지 확인하고 아니면 사용자에게 알림.

## 7. 결과 (학습 후 채우기)
| 항목 | 값 |
|---|---|
| 날짜 / 학습한 PC | |
| 사진 수 (학습 / 검증) | |
| 기본 모델 / 학습 횟수(실제 멈춘 회차) / 이미지 크기 / 장치 | |
| mAP50 / mAP50-95 (전체, crack_iphone, crack) | |
| 결과 위치 | 바탕화면\crack_iphone\models\ |
| 남은 문제 | |
