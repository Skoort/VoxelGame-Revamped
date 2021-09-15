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
			public int SliceOffset { get; set; }
			public Vector2Int SlicePosition;
			public Vector2Int RectScale;
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
		}

		private void InitVoxels()
		{
			for (int z = 0; z < _voxels.GetLength(2); ++z)
			for (int y = 0; y < _voxels.GetLength(1); ++y)
			for (int x = 0; x < _voxels.GetLength(0); ++x)
			{
				_voxels[x, y, z] = ChunkManager.Instance.BiomeLogic.GetVoxel(new Vector3(x, y, z) + transform.position);
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
			// TODO: Correctly position the created meshes in 3D space in the local coordinate system.
		}
		
		private void CreateMeshSlice(int axis, int offset, int dir)
		{
			#region --- Local functions ---

			//  axis | relAxisX | relAxisY
			// ------+----------+----------
			//    Z  |    X     |    Y   
			//    Y  |    X     |    Z   
			//    X  |    Z     |    Y   
			void Rel2AbsIndex(int xRel, int yRel, int zRel, out int x, out int y, out int z)
			{
				x = axis != 0 ? xRel : zRel;
				y = axis != 1 ? yRel : zRel;
				z = axis == 0
					? xRel
					: axis == 1
						? yRel
						: zRel;
			}

			bool GetVoxelByRelativeIndex(int xRel, int yRel, int zRel, out Voxel voxel)
			{
				Rel2AbsIndex(xRel, yRel, zRel, out int xAbs, out int yAbs, out int zAbs);

				if (IsWithinBounds(xAbs, yAbs, zAbs))
				{
					voxel = _voxels[xAbs, yAbs, zAbs];
					return true;
				}
				else
				{
					voxel = null;
					return false;
				}
			}

			#endregion
			
			Rel2AbsIndex(0, 1, 2, out int relAxisX, out int relAxisY, out _);
			
			for (int y = 0; y < _voxels.GetLength(relAxisY); ++y)
			{
				MeshFace inheritedMesh = null;
				for (int x = 0; x < _voxels.GetLength(relAxisX); ++x)
				{
					if (GetVoxelByRelativeIndex(x, y, offset, out var voxel))
					{ 
						Rel2AbsIndex(x, y, offset + (dir == 0 ? +1 : -1), out int outAbsX, out int outAbsY, out int outAbsZ);

						// A face should only be drawn if the voxel in front/behind (relative) this one doesn't exist.
						if (!HasVoxel(outAbsX, outAbsY, outAbsZ))
						{
							var faceIndex = axis * dir;

							MeshFace mesh = null;
							if (GetVoxelByRelativeIndex(x, y - 1, offset, out var neighbor) && neighbor.FaceIndices[faceIndex] != -1)
							{
								mesh = _greedyMeshData[neighbor.FaceIndices[faceIndex]];

								if (mesh.RectScale.x == 1)
								{
									// If the top rect is 1 unit wide, then we safely extend downwards.
									++mesh.RectScale.y;
								} else
								if (inheritedMesh == null)
								{
									// If the top rect is more than one unit wide, we cannot safely extend it downwards
									// until we are certain that there is room for its entire width on this level. We 
									// create a new rectangle that we might combine with the top one later.
									inheritedMesh = CreateMeshFace(axis, offset, dir, x, y);
								}
								else
								{
									if (inheritedMesh.RectScale.x != mesh.RectScale.x)
									{
										// The bottom rect is not yet equal to the top one, so we can safely grow it.
										++inheritedMesh.RectScale.x;
									}
									else
									{ 
										// The bottom rect is as long as the top rect. Grow the top one downwards by one unit 
										// and recycle the bottom one. We have to remember to reassign the bottom voxels' rect.
										RecycleMeshFace(inheritedMesh);
										inheritedMesh = null;
										++mesh.RectScale.y;

										// Reassign the face indices of the bottom rect's voxels (except for voxel, which is done below).
										for (int i = 1; i < mesh.RectScale.x; ++i)
										{
											GetVoxelByRelativeIndex(x - i, y, offset, out var voxelToFix);
											voxelToFix.FaceIndices[faceIndex] = mesh.MeshIndex;  // voxelToFix can't be null.
										}
									}
								}
							}
							else
							{
								if (inheritedMesh != null)
								{ 
									// The inherited mesh did not reach the width of the top rect. Mark the mesh as completed.
									inheritedMesh = null;
								} 
							
								if (GetVoxelByRelativeIndex(x - 1, y, offset, out neighbor) && neighbor.FaceIndices[faceIndex] != -1)
								{
									// Because we have gotten here, we can safely say that the left rect is only one unit wide, so we
									// can safely extend it to the right.
									mesh = _greedyMeshData[neighbor.FaceIndices[faceIndex]];
									++mesh.RectScale.x;
								}
								else
								{
									// This is the first rect in its immediate vicinity.
									CreateMeshFace(axis, offset, dir, x, y);
								}
							}

							voxel.AddFace(faceIndex, mesh.MeshIndex);
						} else
						if (inheritedMesh != null)
					{
						// The inherited mesh did not reach the width of the top rect. Mark the mesh as completed.
						inheritedMesh = null;
					}
					}
				}
			}
		}

		private MeshFace CreateMeshFace(int sliceDimension, int sliceOffset, int plusOrMinus, int x, int y)
		{
			var voxelFaceIndex = sliceDimension * sliceOffset;

			MeshFace meshData = null;
			if (_unusedMeshData.Count > 0)
			{
				meshData = _unusedMeshData.Dequeue();

				// Unused MeshData might have a different scale from (1,1,1) and its vertices might have been flattened to make it invisible.
				meshData.RectScale = Vector2Int.one;
				for (int i = 0; i < 4; ++i)
				{
					_vertices[i + meshData.MeshIndex * 4] = VoxelData.Vertices[voxelFaceIndex][i];
				}
			}
			else
			{ 
				meshData = new MeshFace()
				{
					MeshIndex = _greedyMeshData.Count,
					
					SliceDimension = sliceDimension,
					SliceOffset = sliceOffset,
					SlicePosition = new Vector2Int(x, y),
					RectScale = Vector2Int.one
				};

				var faces = new int[] { 0, 1, 2, 3 };
				_greedyMeshData.Add(meshData);
				_faces.AddRange(VoxelData.Faces);
				_vertices.AddRange(VoxelData.Vertices[voxelFaceIndex]);
				_normals.AddRange(VoxelData.Normals[voxelFaceIndex]);
				_uvs.AddRange(VoxelData.UVs2[voxelFaceIndex]);
			}
			
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

		private void ShowMesh()
		{
			_mesh.SetIndices(_faces.ToArray(), MeshTopology.Quads, 0);
			_mesh.SetVertices(_vertices);
			_mesh.SetNormals(_normals);
			_mesh.SetUVs(0, _uvs);
			
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
