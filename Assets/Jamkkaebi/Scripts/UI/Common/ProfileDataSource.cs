using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Common
{
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
