using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public static class PolyominoRotator
    {
        public static Vector2Int[] Rotate(Vector2Int[] shape, int rotationCount)
        {
            int normalized = ((rotationCount % 4) + 4) % 4;
            Vector2Int[] newShape;

            if (normalized == 0)
            {
                return (Vector2Int[])shape.Clone();
            }
            
            newShape = Rotate90(shape);
            
            for (int i = 0; i < normalized - 1; i++)
            {
               newShape = Rotate90(newShape);
            }
            
            Rearrange(newShape);
            
            return newShape;
        }

        private static Vector2Int[] Rotate90(Vector2Int[] shape)
        {
            Vector2Int[] result = new Vector2Int[shape.Length];
            
            for (int i = 0; i < shape.Length; i++)
            {
                int temp = shape[i].x;
                result[i].x = -shape[i].y;
                result[i].y = temp;
            }
            
            return result;
        }

        private static void Rearrange(Vector2Int[] shape)
        {
            int minX = shape[0].x;
            int minY = shape[0].y;
            
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i].x < minX) minX = shape[i].x;
                if (shape[i].y < minY) minY = shape[i].y;
            }

            for (int i = 0; i < shape.Length; i++)
            {
                shape[i].x -= minX;
                shape[i].y -= minY;
            }
        }
    }
}