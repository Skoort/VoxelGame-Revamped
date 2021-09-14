using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain
{
	[RequireComponent(typeof(Mesh))]
	public class Chunk : MonoBehaviour
	{
		#region --- Mesh Data ---
		private MeshFilter _meshFilter;

		private Mesh _mesh;
		private List<int> _faces;
		private List<Vector3> _vertices;
		private List<Vector3> _normals;
		private List<Vector2> _uvs;

		private struct MeshFace
		{ 
			public Vector3Int Position { get; set; }
			public Vector3Int Dimension { get; set; }
			public int FaceIndex { get; set; }
		}
		#endregion

		public Vector3Int Position { get; }
		
		private Voxel[,,] _voxels;

		private void Awake()
		{
			_meshFilter = GetComponent<MeshFilter>();
		}

		private void Start()
		{
			
		}

		public void Init(Vector3Int chunkSize)
		{
			_voxels = new Voxel[chunkSize.x, chunkSize.y, chunkSize.z];
			ResetMeshData();
		}

		private void ResetMeshData()
		{
			_faces = new List<int>();
			_vertices = new List<Vector3>();
			_normals = new List<Vector3>();
			_uvs = new List<Vector2>();
		}

		private void ShowMesh()
		{
			_mesh.SetIndices(_faces.ToArray(), MeshTopology.Quads, 0);
			_mesh.SetVertices(_vertices);
			_mesh.SetNormals(_normals);
			_mesh.SetUVs(0, _uvs);


			_meshFilter.mesh = _mesh;
		}

		private void CreateVoxels()
		{
			for (int z = 0; z < _voxels.GetLength(2); ++z)
			for (int y = 0; y < _voxels.GetLength(1); ++y)
			for (int x = 0; x < _voxels.GetLength(0); ++x)
			{
				// Bottom, left & back voxels are already made.
				var neighborNegX = GetVoxel(x - 1, y, z);
				var neighborNegY = GetVoxel(x, y - 1, z);
				var neighborNegZ = GetVoxel(x, y, z - 1);

				var thisVoxel = GetVoxel(x, y, z);
				if (thisVoxel == null)
				{
					neighborNegX?.AddFace(0, 0);
					neighborNegY?.AddFace(1, 0);
					neighborNegZ?.AddFace(2, 0);
				} 
				else
				{
					if (neighborNegX == null)
					{
						thisVoxel.AddFace(3, 0);
					}
					if (neighborNegY == null)
					{
						thisVoxel.AddFace(4, 0);
					}
					if (neighborNegZ == null)
					{
						thisVoxel.AddFace(5, 0);
					}
				}
			}
		}

		private Voxel GetVoxel(int x, int y, int z)
		{
			if
			(
				   x >= 0 && x < _voxels.GetLength(0)
				&& y >= 0 && y < _voxels.GetLength(1)
				&& z >= 0 && z < _voxels.GetLength(2)
			)
			{
				return _voxels[x, y, z];
			}
			else
			{
				return ChunkManager.Instance.BiomeLogic.GetVoxel(new Vector3(x, y, z) + transform.position);
			}
		}

		private void CreateFace(Voxel voxel, int faceIndex)
		{ 
			// Finds a free space or 
		}

		private void OnDrawGizmosSelected()
		{
			// Draw the faces.
		}
	}
}
