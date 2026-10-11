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
        /// <summary>Starts a new request, clearing errors and discarding the prior scene only when its normalized owner differs.</summary>
        /// <returns>The revision to pass to Complete or Fail; earlier revisions are invalidated.</returns>
        public int Begin(string code)
        {
            if (Scene != null && FriendsPreviewState.NormalizeCode(Scene.Snapshot.OwnerCode) != FriendsPreviewState.NormalizeCode(code))
                Scene = null;
            _requestedCode = code; Error = null; Loading = true; return ++_revision;
        }
        /// <summary>Accepts a snapshot only for the current revision and requested owner.</summary>
        /// <returns>True when the new scene is installed; false for stale responses or caught validation errors. Validation errors update Error and preserve any retained scene.</returns>
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
        /// <summary>Records an error and stops loading for the current revision, retaining any existing scene; stale failures are ignored.</summary>
        public void Fail(int revision, string error) { if (revision != _revision) return; Loading = false; Error = error; }
        /// <summary>Invalidates pending responses and clears the scene, error, and loading state.</summary>
        public void Leave() { ++_revision; Loading = false; Scene = null; Error = null; }
    }
}
