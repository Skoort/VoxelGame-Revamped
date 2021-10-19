using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VoxelGame.Terrain.Meshing;

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

		public LoadStatus Status { get; private set; }
		
		public Vector3Int Position { get; private set; }

		private Dictionary<Vector3Int, Voxel> _voxels;

		//private List<int> _heightDensities;  // A list of length (MaxHeight - MinHeight + 1), where each element represents the amount of visible blocks at that height.
		private Dictionary<int, int> _heightDensities;
		private int[,] _heightmap;
		public int MinHeight { get; private set; } = int.MaxValue;
		public int MaxHeight { get; private set; } = int.MinValue;

		public bool HasBeenModified { get; private set; }
		
		private MeshFilter _meshFilter;
		private GreedyMesher _mesher;

		private bool _shouldActivateCollisions = false;
		private MeshCollider _meshCollider;

		private void Awake()
		{
			_meshFilter = GetComponent<MeshFilter>();
		}

		private void Start()
		{

		}

		public async Task Load(bool shouldLoadFromFile, CancellationToken destroyRequestedToken)
		{
			Position = Vector3Int.FloorToInt(transform.position);

			//var stopwatch = new System.Diagnostics.Stopwatch();
			//stopwatch.Start();

			Status = LoadStatus.LOADING;

			await Task.Run(() =>
			{
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
					_mesher = new GreedyMesher(this);
					_mesher.GenerateMesh();
				}
			}, destroyRequestedToken);

			Status = LoadStatus.FINISHED_LOADING;

			_mesher.ShowMesh(_meshFilter);
			ActivateCollisions();

			//stopwatch.Stop();
			//Debug.Log($"Chunk took {stopwatch.ElapsedMilliseconds} milliseconds to create!");
		}

		private void LoadMapsAndMesh()
		{

		}

		private void Initialize()
		{

		}

		private void GenerateBiomesmap()
		{

		}

		private void GenerateHeightmap()
		{
			var chunkSize = ChunkManager.Instance.ChunkSize;

			_heightmap = new int[chunkSize.x + 2, chunkSize.y + 2];
			for (int z = 0; z < _heightmap.GetLength(1); ++z)
			for (int x = 0; x < _heightmap.GetLength(0); ++x)
			{
				_heightmap[x, z] = ChunkManager.Instance.BiomeLogic.GetHeight(new Vector3(x - 1, 0, z - 1) + Position);
			}
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
			var chunkSize = ChunkManager.Instance.ChunkSize;

			_voxels = new Dictionary<Vector3Int, Voxel>();
			_heightDensities = new Dictionary<int, int>();
			for (int z = 0; z < chunkSize.y; ++z)
			for (int x = 0; x < chunkSize.x; ++x)
			{
				var biome = 0; // TODO: Set the biome type.
				var height = _heightmap[x + 1, z + 1];

				var minHeight = height - 1;  // One less than the coordinate of the lowest ground block at this x and z coord..
				var maxHeight = height + 1;  // Y coordinate of the highest air block with this x and z coord..
				foreach (var h in GetNeighboringHeights(x, z))
				{
					minHeight = System.Math.Min(minHeight, h);
					maxHeight = System.Math.Max(maxHeight, h);
				}

				if (height >= MaxHeight)
				{
					MaxHeight = height + 1;  // MaxHeight is actually one higher than the highest voxel.
				}
				if (height < MinHeight)
				{
					MinHeight = height;
				}

				// Create the blocks
				for (var y = height; y > minHeight; --y)
				{
					if (_heightDensities.ContainsKey(y))
					{
						_heightDensities[y]++;
					}
					else
					{
						_heightDensities.Add(y, 1);
					}

					var pos = new Vector3Int(x, y, z);

					int voxelType = 0;  // TODO: Set the voxel type.

					var voxel = new Voxel(voxelType, biome);

					_voxels.Add(pos, voxel);
				}
			}
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.blue;
			Gizmos.DrawWireMesh(_mesher.Mesh, 0, Position);
		}

		//public IEnumerable<Voxel> GetNeighbors(Vector3Int pos)
		//{
		//	yield return GetVoxel(pos + new Vector3Int(+1, 0, 0));
		//	yield return GetVoxel(pos + new Vector3Int(0, +1, 0));
		//	yield return GetVoxel(pos + new Vector3Int(0, 0, +1));
		//	yield return GetVoxel(pos + new Vector3Int(-1, 0, 0));
		//	yield return GetVoxel(pos + new Vector3Int(0, -1, 0));
		//	yield return GetVoxel(pos + new Vector3Int(0, 0, -1));
		//}

		public IEnumerable<Vector3Int> GetNeighboringPositions(Vector3Int pos)
		{
			yield return pos + new Vector3Int(+1,  0,  0);
			yield return pos + new Vector3Int( 0, +1,  0);
			yield return pos + new Vector3Int( 0,  0, +1);
			yield return pos + new Vector3Int(-1,  0,  0);
			yield return pos + new Vector3Int( 0, -1,  0);
			yield return pos + new Vector3Int( 0,  0, -1);
		}

		public void CreateBlock(Vector3Int localPos, int dataId)
		{
			if (_voxels.ContainsKey(localPos))
			{
				return;
			}

			Debug.Log("Creating a new voxel!");

			var voxel = new Voxel(dataId, 0);  // TODO: Add biome ID obtained from x, z coordinate.
			_voxels.Add(localPos, voxel);

			//CreateOrDestroyBlock(localPos, voxel, shouldCreate: true);

			_mesher.ShowMesh(_meshFilter);
		}

		public void DestroyBlock(Vector3Int localPos)
		{
			if (!_voxels.TryGetValue(localPos, out var voxel))
			{
				return;
			}

			Debug.Log("Destroying a voxel!");

			//CreateOrDestroyBlock(localPos, voxel, shouldCreate: false);

			_mesher.ShowMesh(_meshFilter);
		}
		/*
		// This one can be inside of Chunk and it calls BreakUpRect on the appropriate GreedyMeshers (if voxel is out of bounds, then on neighboring chunk's one).
		private void CreateOrDestroyBlock(Vector3Int localPos, Voxel voxel, bool shouldCreate)
		{
			var neighboringPositions = GetNeighboringPositions(localPos);

			Debug.Log($"Local pos: {localPos}");

			int i = 0;
			foreach (var position in neighboringPositions)
			{
				var neighbor = GetVoxel(position);
				var axis = i % 3;

				var localPosRel = Rel2AbsVector2(axis, localPos);

				Debug.Log($"Local pos rel: {localPosRel}");

				var positionRel = Rel2AbsVector2(axis, position);
				var localPosPlusMinus = i < 3 ? 0 : 1;
				var positionPlusMinus = i < 3 ? 1 : 0;  // If this voxel's face is in the 1 direction, then that face's neighboring face is in the 0 direction.
				if (neighbor != null)
				{
					var neighborsFaceId = i + (i < 3 ? +3 : -3);
					if (shouldCreate)
					{
						var faceIndex = neighbor.FaceIndices[neighborsFaceId];
						if (faceIndex != -1)
						{ 
							// Break up this rect for its neighbor's opposite face (rectId +/- 3).
							BreakUpRect(_greedyMeshData[faceIndex], localPosRel, positionPlusMinus);
						}
					}
					else
					{
						Debug.Log($"i: {i}");
						Debug.Log($"Face index: {neighborsFaceId}");
						Debug.Log($"Should be: {i + (i < 3 ? +3 : -3)}");
						// Create a new rect for its neighbor's opposite face (rectId +/- 3).
						var face = CreateMeshFace(axis, positionRel.z, positionPlusMinus, positionRel.x, positionRel.y);
						neighbor.FaceIndices[neighborsFaceId] = face.MeshIndex;
					}
				}
				else
				{
					if (shouldCreate)
					{
						// Create a new rect.
						var face = CreateMeshFace(axis, localPosRel.z, localPosPlusMinus, localPosRel.x, localPosRel.y);
						voxel.FaceIndices[i] = face.MeshIndex;
					}
					else
					{
						var faceIndex = voxel.FaceIndices[i];
						if (faceIndex != -1)
						{ 
							// Break up this rect.
							BreakUpRect(_greedyMeshData[faceIndex], localPosRel, localPosPlusMinus);
						}
					}
				}
				++i;
			}
		}

		// Rect is the rectangle that contains the voxel we want to remove. RelPosition is the position of that voxel. PlusOrMinus is whether it is the positive or negative face.
		private void BreakUpRect(MeshFace rect, Vector3Int relPosition, int plusOrMinus)
		{
			// Because this rect exists, each voxel within its bounds should also exist.
			var axis = rect.SliceDimension;
			
			var faceIndex = axis + plusOrMinus * 3;

			RecycleMeshFace(rect);
			var thisAbsPos = Rel2AbsVector2(axis, relPosition);

			Debug.Log($"This position in rel: {relPosition}");
			Debug.Log($"This position in abs: {thisAbsPos}");
			_voxels[thisAbsPos].RemFace(faceIndex);

			MeshFace topRect = null;
			MeshFace leftRect = null;
			MeshFace rightRect = null;
			MeshFace bottomRect = null;

			for (int x = 0; x < rect.Scale.x; ++x)
			{
				for (int y = 0; y < rect.Scale.y; ++y)
				{
					var position = Rel2AbsVector(axis, rect.SliceSpacePosition + new Vector3Int(x, y, 0));
					var voxel = _voxels[position];

					if (y > relPosition.y)
					{  // The top rect.
						if (topRect == null)
						{ 
							topRect = CreateMeshFace(axis, rect.SliceSpacePosition.z, plusOrMinus, rect.SliceSpacePosition.x, rect.SliceSpacePosition.y);
							topRect.Scale = new Vector2Int(rect.Scale.x, rect.Scale.y - relPosition.y);
						}
						voxel.FaceIndices[faceIndex] = topRect.MeshIndex;
					} else
					if (x < relPosition.x)
					{  // The left rect.
						if (leftRect == null)
						{
							leftRect = CreateMeshFace(axis, rect.SliceSpacePosition.z, plusOrMinus, rect.SliceSpacePosition.x, relPosition.y);
							leftRect.Scale = new Vector2Int(relPosition.x - rect.SliceSpacePosition.x, rect.Scale.y - relPosition.y);
						}
						voxel.FaceIndices[faceIndex] = leftRect.MeshIndex;
					} else
					if (x > relPosition.x)
					{  // The right rect.
						if (rightRect == null)
						{
							rightRect = CreateMeshFace(axis, rect.SliceSpacePosition.z, plusOrMinus, relPosition.x + 1, relPosition.y);
							rightRect.Scale = new Vector2Int(rect.Scale.x - (relPosition.x + 1), rect.Scale.y - relPosition.y);
						}
						voxel.FaceIndices[faceIndex] = rightRect.MeshIndex;
					} else
					if (y < relPosition.y)
					{  // The bottom rect.
						if (bottomRect == null)
						{
							bottomRect = CreateMeshFace(axis, rect.SliceSpacePosition.z, plusOrMinus, relPosition.x, relPosition.y + 1);
							bottomRect.Scale = new Vector2Int(1, rect.Scale.y - (relPosition.y + 1));
						}
						voxel.FaceIndices[faceIndex] = bottomRect.MeshIndex;
					}
				}
			}
		}  // This doesn't depend on other chunks. So this will be entirely in the Greedy Mesher.
		*/

		public Voxel GetVoxel(Vector3Int pos)
		{
			_voxels.TryGetValue(pos, out var voxel);

			return voxel;
		}

		public void RequestCollisions()
		{
			_shouldActivateCollisions = true;
		}

		private void ActivateCollisions()
		{
			if (_meshCollider == null)
			{
				_meshCollider = gameObject.AddComponent<MeshCollider>();
				_meshCollider.sharedMesh = _mesher.Mesh;
			}
		}
	}
}
