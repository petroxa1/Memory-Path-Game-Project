using UnityEngine;

public class HomographyHelper
{
    // 3x3 Homography Matrix coefficients
    // h33 is assumed to be 1
    private float h11, h12, h13;
    private float h21, h22, h23;
    private float h31, h32;
    private bool isValid = false;

    public bool IsValid => isValid;

    /// <summary>
    /// Computes the homography matrix mapping src points to dst points.
    /// src and dst must contain exactly 4 points.
    /// Order of points: 0: Top-Left, 1: Top-Right, 2: Bottom-Left, 3: Bottom-Right
    /// </summary>
    public bool ComputeMatrix(Vector2[] src, Vector2[] dst)
    {
        if (src.Length != 4 || dst.Length != 4)
        {
            Debug.LogError("Homography requires exactly 4 source and 4 destination points.");
            isValid = false;
            return false;
        }

        // Set up the system of 8 linear equations: M * H = B
        // M is a 8x8 matrix, B is an 8x1 vector
        float[,] M = new float[8, 8];
        float[] B = new float[8];

        for (int i = 0; i < 4; i++)
        {
            float sx = src[i].x;
            float sy = src[i].y;
            float dx = dst[i].x;
            float dy = dst[i].y;

            // Equation 1 for point i (u mapping)
            M[i * 2, 0] = sx;
            M[i * 2, 1] = sy;
            M[i * 2, 2] = 1f;
            M[i * 2, 3] = 0f;
            M[i * 2, 4] = 0f;
            M[i * 2, 5] = 0f;
            M[i * 2, 6] = -sx * dx;
            M[i * 2, 7] = -sy * dx;
            B[i * 2] = dx;

            // Equation 2 for point i (v mapping)
            M[i * 2 + 1, 0] = 0f;
            M[i * 2 + 1, 1] = 0f;
            M[i * 2 + 1, 2] = 0f;
            M[i * 2 + 1, 3] = sx;
            M[i * 2 + 1, 4] = sy;
            M[i * 2 + 1, 5] = 1f;
            M[i * 2 + 1, 6] = -sx * dy;
            M[i * 2 + 1, 7] = -sy * dy;
            B[i * 2 + 1] = dy;
        }

        // Solve the linear system using Gaussian Elimination
        float[] H = SolveSystem(M, B);
        if (H == null)
        {
            Debug.LogError("Failed to solve linear system for Homography. Points might be collinear or invalid.");
            isValid = false;
            return false;
        }

        h11 = H[0]; h12 = H[1]; h13 = H[2];
        h21 = H[3]; h22 = H[4]; h23 = H[5];
        h31 = H[6]; h32 = H[7];

        isValid = true;
        return true;
    }

    /// <summary>
    /// Transforms a point from source space (camera pixels) to destination space (grid values).
    /// </summary>
    public Vector2 TransformPoint(Vector2 pt)
    {
        if (!isValid)
        {
            return pt;
        }

        float denominator = h31 * pt.x + h32 * pt.y + 1f;
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return Vector2.zero;
        }

        float u = (h11 * pt.x + h12 * pt.y + h13) / denominator;
        float v = (h21 * pt.x + h22 * pt.y + h23) / denominator;

        return new Vector2(u, v);
    }

    /// <summary>
    /// Solves an 8x8 system of equations using Gaussian elimination with partial pivoting.
    /// </summary>
    private float[] SolveSystem(float[,] A, float[] b)
    {
        int n = 8;
        // Create augmented matrix
        float[,] aug = new float[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                aug[i, j] = A[i, j];
            }
            aug[i, n] = b[i];
        }

        for (int i = 0; i < n; i++)
        {
            // Pivot selection
            int maxRow = i;
            float maxVal = Mathf.Abs(aug[i, i]);
            for (int k = i + 1; k < n; k++)
            {
                float val = Mathf.Abs(aug[k, i]);
                if (val > maxVal)
                {
                    maxVal = val;
                    maxRow = k;
                }
            }

            // Swap rows if necessary
            if (maxRow != i)
            {
                for (int j = 0; j <= n; j++)
                {
                    float temp = aug[i, j];
                    aug[i, j] = aug[maxRow, j];
                    aug[maxRow, j] = temp;
                }
            }

            // Check if singular
            if (Mathf.Abs(aug[i, i]) < 0.000001f)
            {
                return null;
            }

            // Eliminate column values in lower rows
            for (int k = i + 1; k < n; k++)
            {
                float factor = aug[k, i] / aug[i, i];
                for (int j = i; j <= n; j++)
                {
                    aug[k, j] -= factor * aug[i, j];
                }
            }
        }

        // Back substitution
        float[] x = new float[n];
        for (int i = n - 1; i >= 0; i--)
        {
            float sum = aug[i, n];
            for (int j = i + 1; j < n; j++)
            {
                sum -= aug[i, j] * x[j];
            }
            x[i] = sum / aug[i, i];
        }

        return x;
    }
}
