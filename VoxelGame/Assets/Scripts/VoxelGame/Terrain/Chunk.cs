using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelGame.Terrain
{
	[RequireComponent(typeof(Mesh))]
	public class Chunk : MonoBehaviour
	{
		#region --- Mesh Data ---
		private MeshFilter _meshFilter;

		private Mesh _mesh;
		private Queue<MeshFace> _unusedMeshData;  // Stores indices into MeshData that says 
		private List<MeshFace> _greedyMeshData;  // Basically stores the vertices, face order, normals and UVs.
		private List<int> _faces;
		private List<Vector3> _vertices;
		private List<Vector3> _normals;
		private List<Vector2> _uvs;

		private class MeshFace
		{ 
			public int MeshIndex { get; set; }  // Points to the actual 3D data.
			
			public int SliceDimension { get; set; }  // 0, 1, or 2 (used when you have to cut the rectangle up.
			public Vector3Int SliceSpacePosition;
			public Vector2Int Scale;
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
			ClearMesh();
			InitVoxels();
			CreateMesh();
			ShowMesh();
		}

		private void InitVoxels()
		{
			for (int z = 0; z < _voxels.GetLength(2); ++z)
			for (int y = 0; y < _voxels.GetLength(1); ++y)
			for (int x = 0; x < _voxels.GetLength(0); ++x)
			{
				_voxels[x, y, z] = ChunkManager.Instance.BiomeLogic.GetVoxel(new Vector3(x, y, z) + transform.position);
				//if (_voxels[x, y, z] != null)
				//{
				//	Debug.Log($"Created Voxel ({x},{y},{z})!");
				//}
			}
		}

		private void ClearMesh()
		{
			_meshFilter.mesh = null;

			_mesh = null;
			_unusedMeshData = new Queue<MeshFace>();
			_greedyMeshData = new List<MeshFace>();
			_faces = new List<int>();
			_vertices = new List<Vector3>();
			_normals = new List<Vector3>();
			_uvs = new List<Vector2>();
		}
		
		private void CreateMesh()
		{
			for (int axis = 0; axis <= 2; ++axis)
			{
				for (int offset = 0; offset < _voxels.GetLength(axis); ++offset)
				{ 
					for (int dir = 0; dir <= 1; ++dir)
					{
						CreateMeshSlice(axis, offset, dir);
					}
				}
			}

			PositionMeshSlices();
		}
		
		#region --- Local functions ---

		//  axis | relAxisX | relAxisY
		// ------+----------+----------
		//    Z  |    X     |    Y   
		//    Y  |    X     |    Z   
		//    X  |    Z     |    Y   
		void Rel2AbsIndex(int axis, int xRel, int yRel, int zRel, out int x, out int y, out int z)
		{
			x = axis != 0 ? xRel : zRel;
			y = axis != 1 ? yRel : zRel;
			z = axis == 0
				? xRel
				: axis == 1
					? yRel
					: zRel;
		}

		Vector3Int Rel2AbsVector(int axis, Vector3Int rel)
		{
			int absX = axis != 0 ? rel.x : rel.z;
			int absY = axis != 1 ? rel.y : rel.z;
			int absZ = axis == 0
				? rel.x
				: axis == 1
					? rel.y
					: rel.z;
			return new Vector3Int(absX, absY, absZ);
		}

		bool GetVoxelByRelativeIndex(int axis, int xRel, int yRel, int zRel, out Voxel voxel)
		{
			Rel2AbsIndex(axis, xRel, yRel, zRel, out int xAbs, out int yAbs, out int zAbs);

			if (IsWithinBounds(xAbs, yAbs, zAbs))
			{
				voxel = _voxels[xAbs, yAbs, zAbs];
				return voxel != null;
			}
			else
			{
				voxel = null;
				return false;
			}
		}

		#endregion

		private void CreateMeshSlice(int axis, int offset, int dir)
		{
			//Debug.Log($"AXIS: {axis} OFFSET: {offset} DIR: {dir}");
			Rel2AbsIndex(axis, 0, 1, 2, out int relAxisX, out int relAxisY, out _);
			//Debug.Log($"LOCAL AXIS X: {relAxisX} LOCAL AXIS Y: {relAxisY}");

			var faceIndex = axis + dir * 3;

			for (int y = 0; y < _voxels.GetLength(relAxisY); ++y)
			for (int x = 0; x < _voxels.GetLength(relAxisX); ++x)
			{
				if (GetVoxelByRelativeIndex(axis, x, y, offset, out var voxel))
				{
					var debugPos = Rel2AbsVector(axis, new Vector3Int(x, y, offset));
					//Debug.Log($"Checking voxel L({x},{y},{offset})-G({debugPos.x},{debugPos.y},{debugPos.z}).");

					Rel2AbsIndex(axis, x, y, offset + (dir == 0 ? +1 : -1), out int outAbsX, out int outAbsY, out int outAbsZ);

					// A face should only be drawn if the voxel in front/behind (relative) this one doesn't exist.
					if (!HasVoxel(outAbsX, outAbsY, outAbsZ))
					{
						//Debug.Log("Voxel is visible!");

						GetVoxelByRelativeIndex(axis, x, y - 1, offset, out var topNeighbor);
						GetVoxelByRelativeIndex(axis, x - 1, y, offset, out var lftNeighbor);

						var topMesh = (topNeighbor != null && topNeighbor.FaceIndices[faceIndex] != -1)
							? _greedyMeshData[topNeighbor.FaceIndices[faceIndex]]
							: null;
						var lftMesh = (lftNeighbor != null && lftNeighbor.FaceIndices[faceIndex] != -1)
							? _greedyMeshData[lftNeighbor.FaceIndices[faceIndex]]
							: null;

						MeshFace usedMesh = null;
						if (topMesh != null && topMesh.Scale.x == 1)
						{
							// Growing a rect wider than 1 unit is handled by creating another rect and merging the two.
							//Debug.Log("The top (relative) voxel's rect is only 1 unit wide. Extending it downwards by 1!");
									
							++topMesh.Scale.y;
							usedMesh = topMesh;
						}

						if (usedMesh == null && lftMesh != null && lftMesh.Scale.y == 1)
						{
							// Because of the way we iterate, the rect grows to the right as much as possible
							// before exploring a way to grow downwards. Therefore, a rect that has grown
							// downwards cannot be grown to the right anymore.
							//Debug.Log("Left (relative) voxel has a rect we can use!");
							//Debug.Log("Extending the left (relative) voxel's rect to the right by 1!");

							++lftMesh.Scale.x;

							if (   topMesh != null
								&& lftMesh.SliceSpacePosition.x == topMesh.SliceSpacePosition.x
								&& lftMesh.Scale.x == topMesh.Scale.x)
							{
								//Debug.Log("Extending the left voxel's rect caused it to be merged with the top voxel's rect!");
								++topMesh.Scale.y;
									
								//Debug.Log("Recycling the left voxel's rect!");
								RecycleMeshFace(lftMesh);
								for (int i = 1; i < lftMesh.Scale.x; ++i)
								{
									GetVoxelByRelativeIndex(axis, x - i, y, offset, out var voxelToFix);
									voxelToFix.FaceIndices[faceIndex] = topMesh.MeshIndex;  // voxelToFix can't be null.
								}

								usedMesh = topMesh;
							}
							else
							{
								usedMesh = lftMesh;
							}
						}

						if (usedMesh == null)
						{
							//Debug.Log("Created a rect for this voxel!");

							usedMesh = CreateMeshFace(axis, offset, dir, x, y);
						}

						voxel.AddFace(faceIndex, usedMesh.MeshIndex);
					}
				}
			}
		}

		private MeshFace CreateMeshFace(int sliceDimension, int sliceOffset, int plusOrMinus, int x, int y)
		{
			var voxelFaceIndex = sliceDimension + plusOrMinus * 3;

			MeshFace meshData = null;
			if (_unusedMeshData.Count > 0)
			{
				meshData = _unusedMeshData.Dequeue();
				// Unused MeshData still has its old rect info except for zeroed out vertices to hide the mesh.
				for (int i = 0; i < 4; ++i)
				{
					int index = i + meshData.MeshIndex * 4;
					_vertices[index] = VoxelData.Vertices[voxelFaceIndex][i];
					_normals[index] = VoxelData.Normals[voxelFaceIndex][i];
					_uvs[index] = VoxelData.UVs2[voxelFaceIndex][i];
				}
			}
			else
			{
				meshData = new MeshFace() { MeshIndex = _greedyMeshData.Count };
				_greedyMeshData.Add(meshData);
				_vertices.AddRange(VoxelData.Vertices[voxelFaceIndex]);
				_normals.AddRange(VoxelData.Normals[voxelFaceIndex]);
				_uvs.AddRange(VoxelData.UVs2[voxelFaceIndex]);
				_faces.AddRange(Enumerable.Range(0,4).Select(i => i + meshData.MeshIndex * 4));
			}
			meshData.Scale = Vector2Int.one;
			meshData.SliceDimension = sliceDimension;
			meshData.SliceSpacePosition = new Vector3Int(x, y, sliceOffset);
			
			return meshData;
		}

		private void RecycleMeshFace(MeshFace face)
		{
			var vertexStartIndex = face.MeshIndex * 4;
			for (int i = vertexStartIndex; i < vertexStartIndex + 4; ++i)
			{
				_vertices[i] = Vector3.zero;
			}

			_unusedMeshData.Enqueue(face);
		}

		private void PositionMeshSlices()
		{
			foreach (var rect in _greedyMeshData)
			{
				var absDimensions = Rel2AbsVector(rect.SliceDimension, new Vector3Int(rect.Scale.x, rect.Scale.y, 1));
				var absPosition = Rel2AbsVector(rect.SliceDimension, rect.SliceSpacePosition);

				for (int i = 0; i < 4; ++i)
				{
					var vertexIndex = rect.MeshIndex * 4 + i;

					_vertices[vertexIndex] = Vector3Int.FloorToInt(_vertices[vertexIndex]) * absDimensions + absPosition;
				}
			}
		}

		private void ShowMesh()
		{
			//Debug.Log("Showing mesh!");
			_mesh = new Mesh();
			_mesh.SetVertices(_vertices);
			_mesh.SetNormals(_normals);
			_mesh.SetUVs(0, _uvs);
			_mesh.SetIndices(_faces, MeshTopology.Quads, 0);
			
			_meshFilter.mesh = _mesh;
		}

		private bool HasVoxel(int x, int y, int z)
		{
			return
				(
					IsWithinBounds(x, y, z)
				 && _voxels[x, y, z] != null
				)
			 || ChunkManager.Instance.BiomeLogic.HasVoxel(new Vector3(x, y, z) + transform.position);
		}

		private bool IsWithinBounds(int x, int y, int z)
		{
			return x >= 0 && x < _voxels.GetLength(0)
				&& y >= 0 && y < _voxels.GetLength(1)
			  	&& z >= 0 && z < _voxels.GetLength(2);
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

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireMesh(_mesh, 0, transform.position);
		}
	}
}
