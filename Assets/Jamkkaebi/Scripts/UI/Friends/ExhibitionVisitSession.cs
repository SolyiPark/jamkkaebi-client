using System;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>요청 세대 번호로 취소·복귀 후 응답과 이전 친구의 늦은 응답을 무시합니다.</summary>
    public sealed class ExhibitionVisitSession
    {
        private int _revision;
        private string _requestedCode;
        public bool Loading { get; private set; }
        public string Error { get; private set; }
        public ExhibitionVisitScene Scene { get; private set; }
        public bool CanEdit => false;
        /// <summary>방문 요청 세대를 증가시키고 다른 친구로 전환할 때 기존 전시를 제거합니다.</summary>
        public int Begin(string code)
        {
            if (Scene != null && FriendsPreviewState.NormalizeCode(Scene.Snapshot.OwnerCode) != FriendsPreviewState.NormalizeCode(code))
                Scene = null;
            _requestedCode = code; Error = null; Loading = true; return ++_revision;
        }
        /// <summary>최신 요청 세대와 소유자를 검증한 스냅샷만 방문 장면으로 적용합니다.</summary>
        public bool Complete(int revision, ExhibitionSnapshot snapshot)
        {
            if (revision != _revision) return false;
            try
            {
                if (snapshot == null || FriendsPreviewState.NormalizeCode(snapshot.OwnerCode) != FriendsPreviewState.NormalizeCode(_requestedCode)) throw new ArgumentException("요청한 친구의 전시 정보가 아니에요.");
                var scene = new ExhibitionVisitScene(snapshot);
                Scene = scene; Loading = false; Error = null; return true;
            }
            catch (ArgumentException error) { Fail(revision, error.Message); return false; }
        }
        /// <summary>최신 요청의 로딩을 종료하고 재시도에 사용할 오류를 보관합니다.</summary>
        public void Fail(int revision, string error) { if (revision != _revision) return; Loading = false; Error = error; }
        /// <summary>요청 세대를 무효화하고 방문 상태를 제거해 늦은 응답의 적용을 막습니다.</summary>
        public void Leave() { ++_revision; Loading = false; Scene = null; Error = null; }
    }
}
