using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Shell
{
    [Serializable]
    public class TabDefinition
    {
        public string id;
        public string title;
        [Tooltip("Scene asset path. Add this scene to the Build Settings scene list.")]
        [ScenePath] public string scenePath;
        public bool showProfile;
        public Sprite icon;
        public Sprite background;
        public Sprite selectedBackground;
        public Sprite selectionMarker;
        public Color normalColor = new Color(0.55f, 0.58f, 0.56f);
        public Color selectedColor = new Color(0.10f, 0.17f, 0.17f);
        public Color markerColor = new Color(0.25f, 0.48f, 0.42f);
        public BackgroundPolicy backgroundPolicy = BackgroundPolicy.ContinueRunning;
    }
}
