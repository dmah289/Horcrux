using UnityEngine;

namespace FelixFelicis.FelixFelicis.Utilities
{
    public static class MeshFactory
    {
        /// <summary>
        /// Generate a 1x1 quad mesh facing the negative Z-axis
        /// </summary>
        /// <param name="markNoLongerReadable">If true, frees RAM after uploading to the GPU.</param>
        /// <param name="needNormals">If true, calculates vertices normals (required for lighting).</param>
        /// <returns>The generated quad mesh</returns>
        public static Mesh GenerateUnitQuadMesh(bool markNoLongerReadable = true, bool needNormals = false)
        {
            Mesh mesh = new Mesh();
            
            mesh.SetVertices(new Vector3[]
            {
                new(-0.5f, 0.5f, 0),
                new(0.5f, 0.5f, 0),
                new(-0.5f, -0.5f, 0),
                new(0.5f, -0.5f, 0)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0, true);
            
            if(needNormals)
                mesh.RecalculateNormals();
            mesh.UploadMeshData(markNoLongerReadable);
            
            return mesh;
        }
    }
}