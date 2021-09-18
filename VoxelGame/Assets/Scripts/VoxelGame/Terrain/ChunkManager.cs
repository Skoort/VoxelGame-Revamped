using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain
{
	public class ChunkManager : MonoBehaviour
	{
		public static ChunkManager Instance { get; private set; }

		[SerializeField] private int _worldSeed = 0;

		[SerializeField] private Chunk _chunkPrefab = null;

		[SerializeField] private Vector3Int _chunkSize = new Vector3Int(16, 16, 16);
		public Vector3Int ChunkSize => _chunkSize;

		private Dictionary<Vector3Int, Chunk> _chunks;

		[SerializeField] private VoxelData[] _voxelDatas = null;

		public BiomeLogic BiomeLogic { get; private set; }

		[SerializeField] private Transform _playerTransform = null;
		[SerializeField] private float _playerViewDistance = 50;

		private void Awake()
		{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
			if (Instance != null)
			{
				Debug.LogAssertion($"ChunkManager.Awake: Attempted to create multiple instances of type {typeof(ChunkManager)}!");
				Destroy(this.gameObject);
				return;
			}
#endif

			Instance = this;

			Init();
		}

		private void Init()
		{
			BiomeLogic = new BiomeLogic(_worldSeed);

			_chunks = new Dictionary<Vector3Int, Chunk>();
			_loadTimer = _loadCooldown;
		}

		private float _loadCooldown = 0.1F;
		private float _loadTimer;
		private void Update()
		{
			_loadTimer -= Time.deltaTime;
			if (_loadTimer <= 0)
			{
				//Debug.Log("Loading chunks!");
				ShowChunksWithinView();
				_loadTimer = _loadCooldown;
			}
		}

		private Vector3Int GetChunkID(Vector3 pos)
		{
			return Vector3Int.FloorToInt(pos / _chunkSize.x);
		}

		private void ShowChunksWithinView()
		{
			int ratio = Mathf.CeilToInt(_playerViewDistance / _chunkSize.x);

			//Debug.Log(ratio);

			// Get the Chunks enveloping the player.
			for (int i = -ratio; i < +ratio; ++i)
			for (int j = -ratio; j < +ratio; ++j)
			//for (int k = -ratio; k < +ratio; ++k)
			for (int k = -1; k < 1; ++k)
			{
				var chunkId = GetChunkID(new Vector3(i, j, k) * _chunkSize.x + _playerTransform.position);
				var chunkPos = chunkId * _chunkSize.x;

				if (_chunks.TryGetValue(chunkId, out var chunk))
				{
					//Debug.Log("Reactivating chunk!");
					if (chunk.gameObject.activeInHierarchy)
					{
						chunk.gameObject.SetActive(true);
					}	
				}
				else
				{  // We have to create this Chunk.
					//Debug.Log("Spawning Chunk " + chunkId);
					chunk = Instantiate(_chunkPrefab, chunkPos, Quaternion.identity, this.transform);
					chunk.Init();

					_chunks.Add(chunkId, chunk);
				}
			}
		}
	}
}
