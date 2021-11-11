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
		public GreedyMesher Mesher { get; private set; }
		public bool ShouldRedraw { get; set; }

		public bool ShouldCalculateCollisions { get; set; }
		private MeshCollider _meshCollider;

		private void Awake()
		{
			_meshFilter = GetComponent<MeshFilter>();
		}

		private void Start()
		{

		}

		private void Update()
		{
			if (ShouldRedraw)
			{
				Mesher.ShowMesh(_meshFilter);
				ShouldRedraw = false;
			}

			if (ShouldCalculateCollisions)
			{
				CalculateCollisions();
				ShouldCalculateCollisions = false;
			}
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
					Mesher = new GreedyMesher(this);
					Mesher.GenerateMesh();
				}
			}, destroyRequestedToken);

			Status = LoadStatus.FINISHED_LOADING;

			Mesher.ShowMesh(_meshFilter);
			CalculateCollisions();

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

				var neighboringHeights = GetNeighboringHeights(x, z);
				var minHeight = height - 1;  // One less than the coordinate of the lowest ground block at this x and z coord..
				var maxHeight = height + 1;  // Y coordinate of the highest air block with this x and z coord..
				foreach (var h in neighboringHeights)
				{
					minHeight = System.Math.Min(minHeight, h);
					maxHeight = System.Math.Max(maxHeight, h);
				}

				if (height >= MaxHeight)
				{
					MaxHeight = height;
				}
				if (height < MinHeight)
				{
					MinHeight = height;
				}

				// Create the Air Blocks.
				for (var y = maxHeight; y > height; --y)
				{
					var pos = new Vector3Int(x, y, z);
					var voxel = new Voxel(pos, VoxelData.VoxelType.AIR, biome);

					// Using the assumption that the map is always a heightmap, we can simplify the mesh
					// generation process by a lot. We know that each air block has a connection in any
					// direction if that neighboring height is equal to it's height. And only the bottom
					// air block has a connection downwards.
					foreach (var h in neighboringHeights)
					{ 
						if (y == h)
						{
							++voxel.NumOfExposedFaces;
						}
					}
					if (y == height + 1)
					{
						++voxel.NumOfExposedFaces;
					}

					_voxels.Add(pos, voxel);
				}

				// Create the ground blocks.
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

					var voxelType = VoxelData.VoxelType.DIRT;  // TODO: Set the voxel type.

					var voxel = new Voxel(pos, voxelType, biome);

					_voxels.Add(pos, voxel);
				}
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
		
		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.blue;
			Gizmos.DrawWireMesh(Mesher.Mesh, 0, Position);
		}

		public Voxel AddVoxelStub(Vector3Int position, VoxelData.VoxelType voxelType = VoxelData.VoxelType.AIR, int biomeId = -1)
		{
			var voxel = new Voxel(position, voxelType, biomeId);
			_voxels.Add(position, voxel);
			return voxel;
		}

		// Should only be called when the actual voxel at this position has no visible faces.
		public void RemoveVoxel(Vector3Int position)
		{
			_voxels.Remove(position);
		}

		public Voxel GetVoxel(Vector3Int pos)
		{
			_voxels.TryGetValue(pos, out var voxel);

			return voxel;
		}

		private void CalculateCollisions()
		{
			if (_meshCollider == null)
			{
				_meshCollider = gameObject.AddComponent<MeshCollider>();
			}
			_meshCollider.sharedMesh = Mesher.Mesh;
		}
	}
}
