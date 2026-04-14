using System.Collections.Generic;
using UnityEngine;

namespace Underground.World
{
    /// <summary>
    /// Mathematical spline utilities for racing-quality road generation.
    /// Used by EasyRoadsNetworkBuilder to produce smooth, readable curves
    /// suitable for high-speed driving gameplay.
    /// </summary>
    public static class SplineUtils
    {
        // ─────────────────────────────────────────────────────────────────────
        // EXISTING — preserved from original
        // ─────────────────────────────────────────────────────────────────────

        public static List<Vector3> SmoothSimple(List<Vector3> pts)
        {
            if (pts == null || pts.Count < 3) return pts;
            var result = new List<Vector3> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                result.Add((pts[i - 1] + pts[i] + pts[i + 1]) / 3f);
            }
            result.Add(pts[^1]);
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CUBIC BEZIER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate a cubic Bezier curve at parameter t ∈ [0,1].
        /// </summary>
        public static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u  = 1f - t;
            float u2 = u * u;
            float t2 = t * t;
            return u2 * u * p0
                 + 3f * u2 * t * p1
                 + 3f * u * t2 * p2
                 + t2 * t * p3;
        }

        /// <summary>
        /// Sample a cubic Bezier into a list of evenly-spaced-in-t points.
        /// </summary>
        public static List<Vector3> SampleCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int steps)
        {
            var result = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                result.Add(CubicBezier(p0, p1, p2, p3, t));
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CATMULL-ROM
        //
        // Produces a smooth curve that passes THROUGH the control points.
        // Perfect for racing roads: the driver sees each waypoint exactly,
        // and transitions between them are C¹-continuous (no kinks).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate a Catmull-Rom segment between p1 and p2, using p0 and p3 as tangent guides.
        /// </summary>
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Sample a full Catmull-Rom spline through all control points.
        /// stepsPerSegment controls the density of sampled points between each pair.
        /// </summary>
        public static List<Vector3> SampleCatmullRom(List<Vector3> controlPts, int stepsPerSegment = 8)
        {
            if (controlPts == null || controlPts.Count < 2) return controlPts;

            var result = new List<Vector3>();
            int n = controlPts.Count;

            for (int seg = 0; seg < n - 1; seg++)
            {
                // Clamp-extend endpoints so first/last segments still work
                Vector3 p0 = controlPts[Mathf.Max(seg - 1, 0)];
                Vector3 p1 = controlPts[seg];
                Vector3 p2 = controlPts[Mathf.Min(seg + 1, n - 1)];
                Vector3 p3 = controlPts[Mathf.Min(seg + 2, n - 1)];

                int steps = (seg == n - 2) ? stepsPerSegment : stepsPerSegment; // include last point only on final segment
                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)steps;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            result.Add(controlPts[^1]); // ensure we end exactly at the last point
            return result;
        }

        /// <summary>
        /// Sample a CLOSED Catmull-Rom loop (e.g. highway ring).
        /// The spline wraps around: last point connects smoothly back to first.
        /// </summary>
        public static List<Vector3> SampleCatmullRomLoop(List<Vector3> controlPts, int stepsPerSegment = 8)
        {
            if (controlPts == null || controlPts.Count < 3) return controlPts;

            var result = new List<Vector3>();
            int n = controlPts.Count;

            for (int seg = 0; seg < n; seg++)
            {
                Vector3 p0 = controlPts[((seg - 1) % n + n) % n];
                Vector3 p1 = controlPts[seg];
                Vector3 p2 = controlPts[(seg + 1) % n];
                Vector3 p3 = controlPts[(seg + 2) % n];

                for (int i = 0; i < stepsPerSegment; i++)
                {
                    float t = i / (float)stepsPerSegment;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            result.Add(result[0]); // close the loop exactly
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CHAIKIN SUBDIVISION
        //
        // Iterative corner-cutting produces smooth, racing-friendly curves
        // from rough polygonal paths. Each iteration doubles point count
        // and cuts corners by 25/75 ratio.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Apply Chaikin corner-cutting subdivision to smooth a polyline.
        /// Higher iterations = smoother curves. 2-3 iterations is usually enough.
        /// </summary>
        public static List<Vector3> ChaikinSubdivide(List<Vector3> pts, int iterations = 2)
        {
            if (pts == null || pts.Count < 3) return pts;

            var current = new List<Vector3>(pts);
            for (int iter = 0; iter < iterations; iter++)
            {
                var next = new List<Vector3> { current[0] }; // preserve start
                for (int i = 0; i < current.Count - 1; i++)
                {
                    Vector3 a = current[i];
                    Vector3 b = current[i + 1];
                    next.Add(Vector3.Lerp(a, b, 0.25f));
                    next.Add(Vector3.Lerp(a, b, 0.75f));
                }
                next.Add(current[^1]); // preserve end
                current = next;
            }
            return current;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ARC-LENGTH RESAMPLING
        //
        // Takes a variable-density point list and resamples into exactly N
        // evenly-spaced-along-arc-length points. This produces uniform road
        // segment lengths which is what EasyRoads3D expects for clean meshes.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resample a polyline into N uniformly-spaced (by arc length) points.
        /// </summary>
        public static Vector3[] ResampleUniform(List<Vector3> pts, int targetCount)
        {
            if (pts == null || pts.Count < 2 || targetCount < 2)
                return pts?.ToArray();

            // Compute cumulative arc lengths
            float[] cumLen = new float[pts.Count];
            cumLen[0] = 0f;
            for (int i = 1; i < pts.Count; i++)
                cumLen[i] = cumLen[i - 1] + Vector3.Distance(pts[i], pts[i - 1]);

            float totalLen = cumLen[^1];
            if (totalLen < 0.01f) return pts.ToArray();

            Vector3[] result = new Vector3[targetCount];
            result[0] = pts[0];
            result[targetCount - 1] = pts[^1];

            int searchStart = 0;
            for (int j = 1; j < targetCount - 1; j++)
            {
                float targetDist = j * totalLen / (targetCount - 1);

                // Find the segment containing this distance
                while (searchStart < pts.Count - 2 && cumLen[searchStart + 1] < targetDist)
                    searchStart++;

                float segStart = cumLen[searchStart];
                float segEnd   = cumLen[searchStart + 1];
                float segLen   = segEnd - segStart;
                float t = (segLen > 0.001f) ? (targetDist - segStart) / segLen : 0f;

                result[j] = Vector3.Lerp(pts[searchStart], pts[searchStart + 1], t);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // DIVIDED HIGHWAY HELPER
        //
        // Given a center-line, produces left and right carriageway lines
        // offset perpendicular to the local tangent. Handles curves correctly
        // by computing per-point tangent via central differences.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Split a centerline into two parallel carriageway lines for divided highways.
        /// </summary>
        public static void SplitDividedHighway(Vector3[] centerLine, float halfMedianWidth,
                                                out Vector3[] left, out Vector3[] right)
        {
            int n = centerLine.Length;
            left  = new Vector3[n];
            right = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent;
                if (i == 0)
                    tangent = centerLine[1] - centerLine[0];
                else if (i == n - 1)
                    tangent = centerLine[i] - centerLine[i - 1];
                else
                    tangent = centerLine[i + 1] - centerLine[i - 1]; // central difference — smoothest

                tangent.Normalize();
                if (tangent == Vector3.zero) tangent = Vector3.forward;

                Vector3 perpendicular = Vector3.Cross(tangent, Vector3.up).normalized;
                left[i]  = centerLine[i] - perpendicular * halfMedianWidth;
                right[i] = centerLine[i] + perpendicular * halfMedianWidth;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TOTAL ARC LENGTH
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Compute total polyline length.
        /// </summary>
        public static float TotalLength(Vector3[] pts)
        {
            float total = 0f;
            for (int i = 1; i < pts.Length; i++)
                total += Vector3.Distance(pts[i], pts[i - 1]);
            return total;
        }

        public static float TotalLength(List<Vector3> pts)
        {
            float total = 0f;
            for (int i = 1; i < pts.Count; i++)
                total += Vector3.Distance(pts[i], pts[i - 1]);
            return total;
        }
    }
}
