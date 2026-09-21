using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobilePrototype
{
    public class ScenePathAttribute : PropertyAttribute { }
    public enum BackgroundPolicy { ContinueRunning, PauseWhileHidden, RestartOnReturn }

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

    [CreateAssetMenu(menuName = "Mobile Prototype/Tab Catalog")]
    public class TabCatalog : ScriptableObject
    {
        [Tooltip("Rendering uses one reserved layer per tab (layers 8–31): up to 24 tabs.")]
        public List<TabDefinition> tabs = new List<TabDefinition>();
        public int initialTab;
        public Font font;
        public Color surface = new Color(0.985f, 0.985f, 0.972f);
        [Range(256, 1440)] public int maximumTextureWidth = 1080;
    }
}
