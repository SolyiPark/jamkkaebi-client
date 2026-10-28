using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Shell
{
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
