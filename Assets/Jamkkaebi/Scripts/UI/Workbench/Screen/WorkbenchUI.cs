using System;
using Jamkkaebi.Scripts.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>작업대 화면의 색과 UI 생성 도우미. 시안(흑백 와이어프레임)의 색을 따른다.</summary>
    public static class WorkbenchUI
    {
        public static readonly Color Backdrop = new Color32(230, 227, 224, 255); // 상단 배경
        public static readonly Color Panel = Color.white;                      // 하단·상세 패널
        public static readonly Color Ink = new Color32(20, 20, 20, 255);
        public static readonly Color Muted = new Color32(110, 110, 110, 255);
        public static readonly Color Card = new Color32(217, 217, 217, 255);
        public static readonly Color Silhouette = Color.black;
        public static readonly Color FormTone = new Color32(70, 70, 70, 255);   // 봉인 해제 후 흑백 형태
        public static readonly Color NamedTone = new Color32(120, 96, 70, 255); // 기억 회복 후 풀컬러 자리
        public static readonly Color Faded = new Color32(255, 214, 120, 255);
        public static readonly Color Resealed = new Color32(255, 160, 160, 255);
        public static readonly Color Clear = new Color32(170, 220, 190, 255);

        /// <summary>부모의 한 점(anchor)에 pivot을 맞춰 크기와 위치를 정한 사각형.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Box(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            Image image = UIFactory.Image(name, parent, color, Vector2.zero, Vector2.zero);
            Place(image.rectTransform, anchor, pivot, position, size);
            return image;
        }

        /// <summary>부모를 꽉 채우는 글자(여백 padding).</summary>
        public static Text Fill(string name, Transform parent, Font font, string text, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, float padding = 8)
        {
            return UIFactory.Label(name, parent, font, text, size, color, Vector2.zero, Vector2.one,
                new Vector2(padding, padding), new Vector2(-padding, -padding), alignment);
        }

        public static Text Label(string name, Transform parent, Font font, string text, int size, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 boxSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text label = UIFactory.Label(name, parent, font, text, size, color, Vector2.zero, Vector2.zero,
                default, default, alignment);
            Place(label.rectTransform, anchor, pivot, position, boxSize);
            return label;
        }

        /// <summary>배경 상자에 Button을 붙인다. 글자는 호출 측이 자식으로 넣는다.</summary>
        public static Button Button(Image background, Action click)
        {
            background.raycastTarget = true;
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => click());
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.disabledColor = new Color(.85f, .85f, .85f, .6f);
            button.colors = colors;
            return button;
        }

        /// <summary>오브젝트를 비활성화한 뒤 파괴한다. 같은 프레임에 다시 그려도 겹쳐 보이지 않는다.</summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                // EditMode 테스트에서는 Destroy를 쓸 수 없다.
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
