using UnityEngine;
using System.Collections.Generic;

namespace Pangaea.Utils
{
    /// <summary>
    /// Utility functions used across the project.
    /// </summary>
    public static class Helpers
    {
        // Random
        private static System.Random random = new System.Random();

        public static T RandomElement<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[random.Next(list.Count)];
        }

        public static T RandomElement<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[random.Next(array.Length)];
        }

        public static float RandomRange(float min, float max)
        {
            return (float)(random.NextDouble() * (max - min) + min);
        }

        // Vectors
        public static Vector3 RandomPointInCircle(Vector3 center, float radius)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return center + new Vector3(offset.x, 0, offset.y);
        }

        public static Vector3 RandomPointOnCircle(Vector3 center, float radius)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        public static Vector3 FlattenY(Vector3 v)
        {
            return new Vector3(v.x, 0, v.z);
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        // Math
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // String
        public static string FormatTime(float seconds)
        {
            int mins = Mathf.FloorToInt(seconds / 60);
            int secs = Mathf.FloorToInt(seconds % 60);
            return $"{mins:00}:{secs:00}";
        }

        public static string FormatNumber(int number)
        {
            if (number >= 1000000)
                return (number / 1000000f).ToString("0.#") + "M";
            if (number >= 1000)
                return (number / 1000f).ToString("0.#") + "K";
            return number.ToString();
        }

        public static string FormatDistance(float meters)
        {
            if (meters >= 1000)
                return (meters / 1000f).ToString("0.#") + "km";
            return meters.ToString("0") + "m";
        }

        // Colors
        public static Color HexToColor(string hex)
        {
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6)
                return Color.white;

            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            return new Color32(r, g, b, 255);
        }

        public static string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        // Transform
        public static T FindInParents<T>(Transform child) where T : Component
        {
            Transform current = child;
            while (current != null)
            {
                T component = current.GetComponent<T>();
                if (component != null)
                    return component;
                current = current.parent;
            }
            return null;
        }

        public static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        // Geo
        public static float HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth radius in km

            double lat1Rad = lat1 * Mathf.Deg2Rad;
            double lat2Rad = lat2 * Mathf.Deg2Rad;
            double deltaLat = (lat2 - lat1) * Mathf.Deg2Rad;
            double deltaLon = (lon2 - lon1) * Mathf.Deg2Rad;

            double a = System.Math.Sin(deltaLat / 2) * System.Math.Sin(deltaLat / 2) +
                       System.Math.Cos(lat1Rad) * System.Math.Cos(lat2Rad) *
                       System.Math.Sin(deltaLon / 2) * System.Math.Sin(deltaLon / 2);

            double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));

            return (float)(R * c);
        }

        // Coroutine helpers
        public static System.Collections.IEnumerator WaitAndDo(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        public static System.Collections.IEnumerator LerpValue(float from, float to, float duration, System.Action<float> onUpdate, System.Action onComplete = null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float value = Mathf.Lerp(from, to, t);
                onUpdate?.Invoke(value);
                yield return null;
            }
            onUpdate?.Invoke(to);
            onComplete?.Invoke();
        }
    }
}
