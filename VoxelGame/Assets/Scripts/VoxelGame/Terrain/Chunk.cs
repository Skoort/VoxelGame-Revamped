using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VoxelGame.Terrain
{
	[RequireComponent(typeof(Mesh))]
	public class Chunk : MonoBehaviour
	{
		public enum LoadStatus
		{ 
			LOADING,
			FINISHED_LOADING
		}

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

		public Vector3Int Position { get; private set; }

		private Dictionary<Vector3Int, Voxel> _voxels;

		private int[,] _heightmap;
		private int _minHeight = int.MaxValue;
		private int _maxHeight = int.MinValue;
		private readonly int[] _dimensions = new int[3];

		public LoadStatus Status { get; private set; }

		private int _width;
		private int _depth;

		public bool HasBeenModified { get; private set; }

		private void Awake()
		{
			_meshFilter = GetComponent<MeshFilter>();
		}

		private void Start()
		{

		}

		public void Load(bool shouldLoadFromFile, CancellationToken destroyRequestedToken)
		{
			Position = Vector3Int.FloorToInt(transform.position);

			//var stopwatch = new System.Diagnostics.Stopwatch();
			//stopwatch.Start();

			Status = LoadStatus.LOADING;

			//await Task.Run(() =>
			//{
				if (shouldLoadFromFile)
				{
					LoadMapsAndMesh();
				}
				else
				{
					Initialize();
					GenerateBiomesmap();
					GenerateHeightmap();
					GenerateVoxels();
					GenerateMesh();
				}
			//}, destroyRequestedToken);

			Status = LoadStatus.FINISHED_LOADING;

			//stopwatch.Stop();
			//Debug.Log($"Chunk took {stopwatch.ElapsedMilliseconds} milliseconds to create!");

			ShowMesh();
		}

		private void LoadMapsAndMesh()
		{ 
		
		}

		private void Initialize()
		{
			var chunkSize = ChunkManager.Instance.ChunkSize;
			_dimensions[0] = _width = chunkSize.x;
			_dimensions[2] = _depth = chunkSize.y;
		}

		private void GenerateBiomesmap()
		{ 
		
		}

		private void GenerateHeightmap()
		{ 
			_heightmap = new int[_width + 2, _depth + 2];
			for (int z = 0; z < _heightmap.GetLength(1); ++z)
			for (int x = 0; x < _heightmap.GetLength(0); ++x)
			{
				var height = _heightmap[x, z] = ChunkManager.Instance.BiomeLogic.GetHeight(new Vector3(x - 1, 0, z - 1) + Position);
				if (height > _maxHeight)
				{
					_maxHeight = height;
				}
				if (height < _minHeight)
				{
					_minHeight = height;
				}
			}

			_dimensions[1] = _maxHeight - _minHeight + 1;
		}
		
		private IEnumerable<int> GetNeighboringHeights(int x, int z)
		{
			// The heightmap includes the outer layer of voxels.
			x = x + 1;
			z = z + 1;

			if (x < _heightmap.GetLength(0))
			{ 
				yield return _heightmap[x + 1, z];
			}
			if (z < _heightmap.GetLength(1))
			{ 
				yield return _heightmap[x, z + 1];
			}
			if (x > 0)
			{ 
				yield return _heightmap[x - 1, z];
			}
			if (z > 0)
			{ 
				yield return _heightmap[x, z - 1];
			}
		}

		private void GenerateVoxels()
		{
			_voxels = new Dictionary<Vector3Int, Voxel>();
			for (int z = 0; z < _depth; ++z)
			for (int x = 0; x < _width; ++x)
			{
				var biome = 0; // TODO: Set the biome type.
				var height = _heightmap[x + 1, z + 1];
				
				var min_height = height - 1;  // One less than the coordinate of the lowest ground block at this x and z coord..
				var max_height = height + 1;  // Y coordinate of the highest air block with this x and z coord..
				foreach (var h in GetNeighboringHeights(x, z))
				{
					min_height = System.Math.Min(min_height, h);
					max_height = System.Math.Max(max_height, h);
				}
				
				// Create the blocks
				for (var y = height; y > min_height; --y)
				{
					var pos = new Vector3Int(x, y, z);
					
					int voxelType = 0;  // TODO: Set the voxel type.

					var voxel = new Voxel(voxelType, biome);
					
					_voxels.Add(pos, voxel);
				}
			}
		}

		private void ClearMesh()
		{
			_meshFilter.mesh = null;

			_mesh = null;
			_unusedMeshData = new Queue<MeshFace>();
			_greedyMeshData = new List<MeshFace>();
			_faces = new List<int>();
			_vertices = new List<Vector3>(1000);
			_normals = new List<Vector3>(1000);
			_uvs = new List<Vector2>(1000);
		}
		
		private void GenerateMesh()
		{
			ClearMesh();

			for (int axis = 0; axis <= 2; ++axis)
			{
				for (int offset = 0; offset < _dimensions[axis]; ++offset)
				{ 
					for (int dir = 0; dir <= 1; ++dir)
					{
						Debug.Log($"Axis: {axis} Offset: {offset} Dir: {dir}");
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
		void GetAbsoluteDimensions(int axis, int xAbs, int yAbs, int zAbs, out int xRel, out int yRel, out int zRel)
		{
			xRel = axis != 0 ? xAbs : zAbs;
			yRel = axis != 1 ? yAbs : zAbs;
			zRel = axis == 0
				? xAbs
				: axis == 1
					? yAbs
					: zAbs;
		}

		void Rel2AbsIndex(int axis, int xRel, int yRel, int zRel, out int x, out int y, out int z)
		{
			x = axis != 0 ? xRel : zRel;
			y = (axis != 1 ? yRel : zRel) + _minHeight;
			z = axis == 0
				? xRel
				: axis == 1
					? yRel
					: zRel;
		}

		Vector3Int Rel2AbsVector(int axis, Vector3Int rel)
		{
			int absX = axis != 0 ? rel.x : rel.z;
			int absY = (axis != 1 ? rel.y : rel.z) + _minHeight;
			int absZ = axis == 0
				? rel.x
				: axis == 1
					? rel.y
					: rel.z;
			return new Vector3Int(absX, absY, absZ);
		}

		bool GetVoxelByRelativeIndex(int axis, int relX, int relY, int relZ, out Voxel voxel)
		{
			Rel2AbsIndex(axis, relX, relY, relZ, out int absX, out int absY, out int absZ);
			
			_voxels.TryGetValue(new Vector3Int(absX, absY, absZ), out voxel);
			return voxel != null;
		}

		#endregion

		private bool IsSolid(int x, int y, int z)
		{
			// The heightmap includes the outer layer of voxels.
			x = x + 1;
			z = z + 1;

			return y <= _heightmap[x, z];
		}

		private void CreateMeshSlice(int axis, int offset, int dir)
		{
			GetAbsoluteDimensions(axis, 0, 1, 2, out int relAxisX, out int relAxisY, out _);

			var faceIndex = axis + dir * 3;

			for (int y = 0; y < _dimensions[relAxisY]; ++y)
			for (int x = 0; x < _dimensions[relAxisX]; ++x)
			{
				if (GetVoxelByRelativeIndex(axis, x, y, offset, out var voxel))
				{
					Debug.Log("Found a voxel to make faces for!");
					Rel2AbsIndex(axis, x, y, offset + (dir == 0 ? +1 : -1), out int outAbsX, out int outAbsY, out int outAbsZ);

					// A face should only be drawn if the voxel in front/behind (relative) this one doesn't exist.
					if (!IsSolid(outAbsX, outAbsY, outAbsZ))
					{
						Debug.Log("Voxel is visible!");

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
							Debug.Log("The top (relative) voxel's rect is only 1 unit wide. Extending it downwards by 1!");
									
							++topMesh.Scale.y;
							usedMesh = topMesh;
						}

						if (usedMesh == null && lftMesh != null && lftMesh.Scale.y == 1)
						{
							// Because of the way we iterate, the rect grows to the right as much as possible
							// before exploring a way to grow downwards. Therefore, a rect that has grown
							// downwards cannot be grown to the right anymore.
							Debug.Log("Left (relative) voxel has a rect we can use!");
							Debug.Log("Extending the left (relative) voxel's rect to the right by 1!");

							++lftMesh.Scale.x;

							if (   topMesh != null
								&& lftMesh.SliceSpacePosition.x == topMesh.SliceSpacePosition.x
								&& lftMesh.Scale.x == topMesh.Scale.x)
							{
								Debug.Log("Extending the left voxel's rect caused it to be merged with the top voxel's rect!");
								++topMesh.Scale.y;
									
								Debug.Log("Recycling the left voxel's rect!");
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
							Debug.Log("Created a rect for this voxel!");

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
				//var absDimensions = Rel2AbsVector(rect.SliceDimension, new Vector3Int(rect.Scale.x, rect.Scale.y, 1));
				GetAbsoluteDimensions(rect.SliceDimension, rect.Scale.x, rect.Scale.y, 1, out var relX, out var relY, out var relZ);
				var absPosition = Rel2AbsVector(rect.SliceDimension, rect.SliceSpacePosition);

				for (int i = 0; i < 4; ++i)
				{
					var vertexIndex = rect.MeshIndex * 4 + i;

					_vertices[vertexIndex] = Vector3Int.FloorToInt(_vertices[vertexIndex]) * new Vector3Int(relX, relY, relZ) + absPosition;
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

		private void OnDrawGizmosSelected()
		{
			//Gizmos.color = Color.red;
			//foreach (var voxel in _voxels)
			//{
			//	Gizmos.DrawCube(voxel.Key + new Vector3(0.5F, 0.5F, 0.5F) + Position, Vector3.one);
			//}
			Gizmos.color = Color.blue;
			Gizmos.DrawWireMesh(_mesh, 0, Position);
		}
	}
}
