using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype.Collection
{
    // Vector placeholders can be replaced by catalog sprites without changing collection state.
    public sealed class CollectionArtwork : MaskableGraphic
    {
        private CollectionArtworkKind _kind;

        public void Configure(CollectionArtworkKind kind, Color tint)
        {
            _kind = kind;
            color = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            switch (_kind)
            {
                case CollectionArtworkKind.Crown:
                    Box(vertices, -.36f, -.28f, .72f, .13f);
                    Box(vertices, -.28f, -.15f, .09f, .48f);
                    Box(vertices, -.045f, -.15f, .09f, .63f);
                    Box(vertices, .19f, -.15f, .09f, .48f);
                    Box(vertices, -.43f, .12f, .31f, .07f);
                    Box(vertices, .12f, .12f, .31f, .07f);
                    Disc(vertices, new Vector2(0, .48f), .065f);
                    Disc(vertices, new Vector2(-.24f, .34f), .06f);
                    Disc(vertices, new Vector2(.24f, .34f), .06f);
                    break;
                case CollectionArtworkKind.Vessel:
                    Polygon(vertices, new Vector2(-.14f, .4f), new Vector2(.14f, .4f),
                        new Vector2(.11f, .2f), new Vector2(.29f, -.07f), new Vector2(.22f, -.32f),
                        new Vector2(-.22f, -.32f), new Vector2(-.29f, -.07f), new Vector2(-.11f, .2f));
                    Box(vertices, -.19f, -.38f, .38f, .06f);
                    break;
                case CollectionArtworkKind.Buddha:
                    Disc(vertices, new Vector2(0, .3f), .11f);
                    Polygon(vertices, new Vector2(-.14f, .17f), new Vector2(.14f, .17f),
                        new Vector2(.21f, -.3f), new Vector2(-.21f, -.3f));
                    Box(vertices, -.22f, .02f, .44f, .06f);
                    Box(vertices, -.3f, -.39f, .6f, .07f);
                    break;
                case CollectionArtworkKind.IncenseBurner:
                    Disc(vertices, new Vector2(0, .4f), .045f);
                    Polygon(vertices, new Vector2(-.27f, .08f), new Vector2(0, .34f), new Vector2(.27f, .08f));
                    Polygon(vertices, new Vector2(-.29f, .02f), new Vector2(.29f, .02f),
                        new Vector2(.19f, -.16f), new Vector2(-.19f, -.16f));
                    Box(vertices, -.045f, -.32f, .09f, .17f);
                    Box(vertices, -.23f, -.38f, .46f, .06f);
                    break;
                case CollectionArtworkKind.Lacquerware:
                    Box(vertices, -.36f, -.24f, .065f, .4f);
                    Box(vertices, .295f, -.24f, .065f, .4f);
                    Box(vertices, -.36f, -.24f, .72f, .06f);
                    Box(vertices, -.4f, .16f, .8f, .075f);
                    Polygon(vertices, new Vector2(0, .07f), new Vector2(.11f, -.04f),
                        new Vector2(0, -.15f), new Vector2(-.11f, -.04f));
                    break;
                case CollectionArtworkKind.RoyalSeal:
                    Disc(vertices, new Vector2(0, .2f), .13f);
                    Box(vertices, -.06f, -.14f, .12f, .28f);
                    Box(vertices, -.35f, -.3f, .7f, .16f);
                    Box(vertices, -.43f, -.38f, .86f, .06f);
                    break;
                case CollectionArtworkKind.HorseMedallion:
                    Ring(vertices, .36f, .045f);
                    Box(vertices, -.045f, .35f, .09f, .08f);
                    Box(vertices, -.2f, -.04f, .34f, .14f);
                    Box(vertices, .08f, .07f, .07f, .18f);
                    Box(vertices, .08f, .2f, .15f, .07f);
                    Box(vertices, -.15f, -.2f, .045f, .16f);
                    Box(vertices, .07f, -.2f, .045f, .16f);
                    Polygon(vertices, new Vector2(-.2f, .1f), new Vector2(-.28f, .03f), new Vector2(-.25f, -.02f));
                    break;
                case CollectionArtworkKind.Painting:
                    Box(vertices, -.4f, -.37f, .065f, .77f);
                    Box(vertices, .335f, -.37f, .065f, .77f);
                    Box(vertices, -.4f, .335f, .8f, .065f);
                    Box(vertices, -.4f, -.37f, .8f, .065f);
                    Disc(vertices, new Vector2(-.07f, -.02f), .09f);
                    Polygon(vertices, new Vector2(-.15f, .02f), new Vector2(-.17f, .13f), new Vector2(-.08f, .06f));
                    Polygon(vertices, new Vector2(-.06f, .06f), new Vector2(.02f, .13f), new Vector2(.01f, .02f));
                    Polygon(vertices, new Vector2(-.12f, -.1f), new Vector2(-.02f, -.1f),
                        new Vector2(.03f, -.25f), new Vector2(-.18f, -.25f));
                    Disc(vertices, new Vector2(.17f, .18f), .045f);
                    Polygon(vertices, new Vector2(.12f, .17f), new Vector2(.02f, .23f), new Vector2(.1f, .11f));
                    break;
                case CollectionArtworkKind.Building:
                    Polygon(vertices, new Vector2(-.47f, .06f), new Vector2(0, .37f), new Vector2(.47f, .06f));
                    Box(vertices, -.34f, -.28f, .09f, .31f);
                    Box(vertices, -.045f, -.28f, .09f, .31f);
                    Box(vertices, .25f, -.28f, .09f, .31f);
                    Box(vertices, -.45f, -.35f, .9f, .07f);
                    break;
                case CollectionArtworkKind.Spirit:
                    Disc(vertices, new Vector2(0, .07f), .3f);
                    Polygon(vertices, new Vector2(-.29f, .08f), new Vector2(.29f, .08f),
                        new Vector2(.32f, -.34f), new Vector2(.12f, -.23f), new Vector2(0, -.35f),
                        new Vector2(-.12f, -.23f), new Vector2(-.32f, -.34f));
                    Polygon(vertices, new Vector2(-.24f, .26f), new Vector2(-.2f, .48f), new Vector2(-.06f, .34f));
                    Polygon(vertices, new Vector2(.06f, .34f), new Vector2(.2f, .48f), new Vector2(.24f, .26f));
                    break;
                case CollectionArtworkKind.Lock:
                    Box(vertices, -.23f, -.31f, .46f, .35f);
                    Box(vertices, -.16f, .04f, .07f, .21f);
                    Box(vertices, .09f, .04f, .07f, .21f);
                    Box(vertices, -.16f, .25f, .32f, .07f);
                    break;
            }
        }

        private Vector2 Position(Vector2 point)
        {
            Rect rect = rectTransform.rect;
            float scale = Mathf.Min(rect.width, rect.height);
            return rect.center + point * scale;
        }

        private void Polygon(VertexHelper vertices, params Vector2[] points)
        {
            int start = vertices.currentVertCount;
            foreach (Vector2 point in points) vertices.AddVert(Position(point), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vertices.AddTriangle(start, start + i, start + i + 1);
        }

        private void Box(VertexHelper vertices, float x, float y, float width, float height)
        {
            Polygon(vertices, new Vector2(x, y), new Vector2(x + width, y),
                new Vector2(x + width, y + height), new Vector2(x, y + height));
        }

        private void Disc(VertexHelper vertices, Vector2 center, float radius)
        {
            const int Segments = 32;
            int start = vertices.currentVertCount;
            vertices.AddVert(Position(center), color, Vector2.zero);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                vertices.AddVert(Position(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius), color, Vector2.zero);
            }
            for (int i = 0; i < Segments; i++) vertices.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
        }

        private void Ring(VertexHelper vertices, float radius, float thickness)
        {
            const int Segments = 32;
            for (int i = 0; i < Segments; i++)
            {
                float firstAngle = i * Mathf.PI * 2 / Segments;
                float secondAngle = (i + 1) * Mathf.PI * 2 / Segments;
                var firstDirection = new Vector2(Mathf.Cos(firstAngle), Mathf.Sin(firstAngle));
                var secondDirection = new Vector2(Mathf.Cos(secondAngle), Mathf.Sin(secondAngle));
                Polygon(vertices, firstDirection * radius, secondDirection * radius,
                    secondDirection * (radius - thickness), firstDirection * (radius - thickness));
            }
        }
    }
}
