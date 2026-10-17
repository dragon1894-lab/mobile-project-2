# 4번 - 영상처리

담당자는 `VisionInspector` 내부를 실제 카메라와 OpenCvSharp 처리로 확장합니다.

- 카메라 프레임 획득
- ROI와 조명 보정
- 형태·윤곽 유사도 계산
- 색상 범위 비교
- `Normal`, `Defect`, `Undetermined` 반환

외부에 공개하는 `Inspect(InspectionRequest)` 형식은 유지해야 다른 프로젝트와의 병합 충돌이 줄어듭니다.
