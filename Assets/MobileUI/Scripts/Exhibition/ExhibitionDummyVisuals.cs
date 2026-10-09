using System;
using UnityEngine;

namespace MobilePrototype.Exhibition
{
    // Small code-drawn placeholders; real sprites can replace these without changing occupancy.
    public sealed class ExhibitionDummyVisuals : IDisposable
    {
        private readonly Sprite _sprite;
        private readonly Material _material;

        public ExhibitionDummyVisuals(Material material)
        {
            _material = material;
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        }

        private void Part(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var part = new GameObject(name, typeof(SpriteRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = part.GetComponent<SpriteRenderer>();
            renderer.sprite = _sprite; renderer.sharedMaterial = _material;
            renderer.color = color; renderer.sortingOrder = order;
        }

        public void ExhibitionStand(Transform parent)
        {
            Part(parent, "Base", new Vector2(0, .075f), new Vector2(.27f, .15f), new Color(.45f, .27f, .15f), 0);
            Part(parent, "Top", new Vector2(0, .18f), new Vector2(.34f, .075f), new Color(.82f, .65f, .4f), 1);
            Part(parent, "Relic", new Vector2(0, .31f), new Vector2(.13f, .2f), new Color(.3f, .65f, .68f), 2);
            Part(parent, "Facing", new Vector2(.1f, .18f), new Vector2(.08f, .035f), Color.white, 3);
        }

        public void SetFacing(Transform parent, int quarterTurns)
        {
            var marker = parent.Find("Facing");
            var directions = new[] { new Vector2(.11f, -.04f), new Vector2(-.11f, -.04f),
                new Vector2(-.11f, .04f), new Vector2(.11f, .04f) };
            marker.localPosition = (Vector3)(new Vector2(0, .18f) + directions[quarterTurns % 4]);
            parent.Find("Relic").localPosition = new Vector3(directions[quarterTurns % 4].x * .4f, .31f, 0);
        }

        public void Dokkaebi(Transform parent)
        {
            Part(parent, "Body", new Vector2(0, .12f), new Vector2(.18f, .22f), new Color(.38f, .65f, .35f), 0);
            Part(parent, "Head", new Vector2(0, .28f), new Vector2(.24f, .19f), new Color(.55f, .8f, .42f), 1);
            Part(parent, "Horn", new Vector2(0, .4f), new Vector2(.065f, .09f), new Color(.96f, .8f, .4f), 2);
            Part(parent, "Left eye", new Vector2(-.065f, .29f), new Vector2(.035f, .035f), Color.black, 3);
            Part(parent, "Right eye", new Vector2(.065f, .29f), new Vector2(.035f, .035f), Color.black, 3);
            Part(parent, "Mouth", new Vector2(0, .23f), new Vector2(.07f, .02f), Color.black, 3);
        }

        public void Dispose() => UnityEngine.Object.Destroy(_sprite);
    }
}
