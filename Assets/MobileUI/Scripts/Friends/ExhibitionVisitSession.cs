using System;

namespace MobilePrototype.Friends
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
        public int Begin(string code)
        {
            if (Scene != null && FriendsPreviewState.NormalizeCode(Scene.Snapshot.OwnerCode) != FriendsPreviewState.NormalizeCode(code))
                Scene = null;
            _requestedCode = code; Error = null; Loading = true; return ++_revision;
        }
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
        public void Fail(int revision, string error) { if (revision != _revision) return; Loading = false; Error = error; }
        public void Leave() { ++_revision; Loading = false; Scene = null; Error = null; }
    }
}
