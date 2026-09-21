# PR #3 리뷰 수정 및 로컬 검증

검증일: 2026-09-21. Unity 6000.3.23f1, Windows x64 Development Player.
기준 로컬 커밋: `eb17959`. 원격 PR의 검토 기준 커밋: `a73d569`.

## 처리 내용

- `IntegratedSetup`: 검증 컴포넌트를 씬 안에서 재사용하고, 비활성 오브젝트까지 포함해 중복 컴포넌트만 제거합니다. 다른 컴포넌트나 오브젝트는 보존합니다.
- `PrototypeSetup`: 빌드 씬 동기화 시 기존 씬의 활성/비활성 설정을 보존합니다. 새로 추가하는 씬은 활성화하고, 중복 경로는 하나로 정리합니다.
- `SafeArea`: RectTransform을 필수 컴포넌트로 지정하고 활성화 시 검증·캐싱합니다. 재활성화하면 화면 영역을 다시 계산합니다.
- 구형 `PrototypeVerification`은 기존 로컬 커밋 `a76715e`에서 이미 삭제되어 추가 변경하지 않았습니다. 원격 PR에는 아직 반영되지 않았습니다.
- `WorkshopPresenter`는 변경하지 않았습니다. 리뷰에서 지적한 활성 오브젝트에 AddComponent 후 참조를 할당하는 순서를 EditMode에서 검사했으며, 참조 미할당 상태의 생성·제거에서 오류가 발생하지 않았습니다. 이 클래스에는 ExecuteAlways가 없습니다. 이는 런타임에서 참조 없이 동적으로 추가하는 임의의 사용까지 안전하다는 뜻은 아닙니다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| 추가 회귀 검사 | 10개 통과 |
| 기존 EditMode 테스트 | 20개 통과, 실패 및 건너뜀 0 |
| 빌드 씬의 스크립트·오브젝트 참조 검사 | 통과 |
| Windows 개발용 빌드 | 성공 |
| 실제 실행 파일 통합 검사 | 49개 통과, 종료 코드 0 |
| 검증 스크린샷 | PNG 6장 저장 |
| git diff --check | 통과 |

회귀 검사는 검증 컴포넌트 반복 생성 방지, 비활성 중복 제거, 중복 소유 오브젝트 보존, 씬 경로 중복 제거, 비활성 씬 보존, 시작 씬 순서 보존, 새 카탈로그 씬 활성화, SafeArea 필수 컴포넌트 추가, 에디터의 Presenter 생성 및 오류 유무를 확인합니다. 전체 Prepare의 씬 재생성은 실행하지 않고 수정된 중복 정리 경로를 직접 반복 검사했습니다.

실행 중 오류는 없었습니다. 종료 시 기존 ComputeBuffer 해제 및 D3D12 리소스 정리 경고는 남아 있습니다. Android/iOS 실기기 검증은 수행하지 않았습니다.

후속 작업에서 종료 경고의 원인을 확인하고 수정했습니다. 최신 결과는 [GPU 버퍼 종료 검증](gpu-buffer-shutdown-validation.md)을 참고하세요.

## 재현

Unity Editor를 닫은 상태에서 저장소 루트에서 실행합니다.

```powershell
& 'D:\UnityEditors\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -executeMethod ReviewRegressionChecks.Run -logFile "$PWD\Logs\ReviewRegression.log"
.\Tools\VerifyIntegration.ps1 -EditorPath 'D:\UnityEditors\6000.3.23f1\Editor\Unity.exe'
```

로그: `Logs/ReviewRegression.log`, `Logs/Integration/EditMode.xml`, `Logs/Integration/build.log`, `Logs/Integration/Player-20260921-200634/verification.txt`.

이번 작업은 로컬 수정과 검증만 수행했습니다. 새 커밋, 푸시, PR 댓글 게시 및 병합은 수행하지 않았습니다.
