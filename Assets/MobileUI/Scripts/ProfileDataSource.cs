using System;
using UnityEngine;

namespace MobilePrototype
{
    [Serializable]
    public class ProfileData
    {
        public string nickname = "닉네임";
        public int consecutiveDays = 12;
        public Sprite avatar;
    }

    // Replace the dummy publisher with a network/save-data adapter calling SetProfile.
    public class ProfileDataSource : MonoBehaviour
    {
        [SerializeField] private ProfileData profile = new ProfileData();
        public ProfileData Current => profile;
        public event Action<ProfileData> Changed;
        public void SetProfile(ProfileData value)
        {
            profile = value ?? new ProfileData();
            Changed?.Invoke(profile);
        }
        private void OnValidate() { if (Application.isPlaying) Changed?.Invoke(profile); }
    }
}
