# GPU 버퍼 종료 경고 원인 및 수정

2026-09-21 / Unity 6000.3.23f1 / 2D Animation 13.0.6 / Windows Development Player / AMD Radeon RX 6700 XT / Direct3D 12.

## 원인 확인

2D Animation 패키지의 `Runtime/BatchedDeformation/GpuDeformationSystem.cs`는 `AfterSceneLoad` 초기화에서 `s_FallbackBuffer`를 생성합니다. SpriteSkin을 사용하지 않아도 실행됩니다. 반면 `ClearFallbackBuffer()`는 GPU 변형 시스템의 `Cleanup()`에서 호출됩니다. 이 프로젝트의 씬과 프리팹에는 SpriteSkin이 없으며, 기본 버퍼만 만들어지는 경로의 종료 정리가 누락됩니다.

임시 진단 코드로 종료 직전 해당 버퍼가 여전히 유효하며 count=64, stride=64인 것을 확인했습니다. 같은 진단 빌드에서 종료 시 패키지의 `ClearFallbackBuffer()`만 호출하는 옵션을 비교했습니다.

| 실행 | 기능 검사 | ComputeBuffer 자동 정리 경고 | D3D12 미해제 자원 경고 |
|---|---|---|---|
| 기본 종료 | 49개 통과 | 발생 | 발생: 65536 B, Buffer-16-4096 |
| 기본 버퍼 해제 후 종료 | 49개 통과 | 없음 | 없음 |

두 경고는 이 기본 버퍼 정리 누락으로 발생한 것으로 확인했습니다. 버퍼의 논리적 데이터 크기는 4096바이트이고 D3D12 로그의 자원 크기는 65536바이트입니다. 두 수치를 동일한 의미로 취급하지 않습니다. 임시 진단 코드는 최종 소스에서 제거했습니다.

진단 로그: `Logs/BufferProbe/baseline/player.log`, `Logs/BufferProbe/release/player.log`.

## 최소 수정

- `SpriteFallbackBufferCleanup`이 `Application.quitting`에서 패키지의 기존 `ClearFallbackBuffer()`를 호출합니다. 그 함수는 버퍼를 해제한 뒤 static 참조를 null로 비우므로 패키지가 나중에 다시 정리해도 안전합니다.
- 패키지 내부 함수라 reflection을 사용하고, `link.xml`에 해당 함수만 보존하도록 지정했습니다. 패키지 원본 및 Library 캐시는 수정하지 않았습니다.
- 어셈블리 version define으로 2D Animation 13.0.6에만 적용하며 Windows Player에서만 실행합니다. Editor와 모바일에서는 실행하지 않습니다. 패키지 업데이트 시 이 우회 처리의 필요성을 재확인해야 합니다.
- 게임 로직, 씬, RenderTexture 처리, D3D12 사용 설정, Unity 및 패키지 버전은 변경하지 않았습니다.
- `VerifyIntegration.ps1`은 플레이어 종료 후 로그에서 해당 경고 또는 정리 함수 탐색 실패를 발견하면 실패 처리합니다. 기존 검증은 종료 전에 보고서를 기록하므로 종료 경고를 검출하지 못했습니다.

## 최종 검증

- 기존 EditMode 테스트 20개 통과.
- Windows 개발용 빌드 및 씬 참조 검사 통과.
- 최종 빌드의 D3D12 통합 검사 49개 통과, 스크린샷 6장 저장, 종료 코드 0.
- 최종 로그에서 두 종료 경고 및 정리 함수 탐색 실패 없음.
- 같은 최종 빌드를 D3D12로 다시 실행해 49개 검사 통과, 종료 코드 0, 종료 경고 0건을 재확인했습니다 (`Logs/BufferProbe/final-repeat`).
- 종료 로그 검사 조건이 기존 실패 로그를 검출하고 최종 로그는 통과시키는 것을 확인. PowerShell 문법 검사 통과.

최종 로그: `Logs/Integration/Player-20260921-201405/player.log`, 같은 폴더의 `verification.txt`.

검증 범위는 현재 Windows Development Player입니다. IL2CPP와 모바일 빌드는 검증하지 않았습니다. 이 결과는 앞선 `pr3-review-validation.md`의 종료 경고 잔존 기록을 대체합니다. 커밋·푸시·병합은 수행하지 않았습니다.
