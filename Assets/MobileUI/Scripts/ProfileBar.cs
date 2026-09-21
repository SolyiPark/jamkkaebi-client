using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype
{
    public class ProfileBar : MonoBehaviour
    {
        public ProfileDataSource source;
        public Text nickname;
        public Text streak;
        public Image avatar;
        private void OnEnable()
        {
            if (!source) return;
            source.Changed += Display;
            Display(source.Current);
        }
        private void OnDisable() { if (source) source.Changed -= Display; }
        public void Display(ProfileData data)
        {
            nickname.text = data.nickname;
            streak.text = $"연속 {Mathf.Max(0, data.consecutiveDays)}일";
            avatar.sprite = data.avatar;
            avatar.color = data.avatar ? Color.white : new Color(0.85f, 0.88f, 0.85f);
        }
    }
}
