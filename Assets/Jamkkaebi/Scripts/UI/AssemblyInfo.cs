using System.Runtime.CompilerServices;

// 검증용 internal 생성자(전송 경계·기록 교체)를 UI EditMode 테스트에서 쓴다.
[assembly: InternalsVisibleTo("Jamkkaebi.UI.EditMode")]
// 실제 코루틴 수명(꺼짐·다시 켜짐)을 확인하는 UI PlayMode 테스트도 같은 전송 경계를 쓴다.
[assembly: InternalsVisibleTo("Jamkkaebi.UI.PlayMode")]
