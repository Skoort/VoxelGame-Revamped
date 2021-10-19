using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain
{
	public class ChunkManager : MonoBehaviour
	{
		public static ChunkManager Instance { get; private set; }

		[SerializeField] private int _worldSeed = 0;

		[SerializeField] private Chunk _chunkPrefab = null;

		[SerializeField] private Vector2Int _chunkSize = new Vector2Int(32, 32);
		public Vector2Int ChunkSize => _chunkSize;

		private Dictionary<Vector2Int, Chunk> _chunks;

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

			_chunks = new Dictionary<Vector2Int, Chunk>();
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

		public Vector2Int GetChunkID(Vector3 pos)
		{
			pos.Scale(new Vector3(1.0F / _chunkSize.x, 0, 1.0F / _chunkSize.y));
			return new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z));
		}

		// Get the Chunk containing this position.
		public Chunk GetChunk(Vector3 pos)
		{
			_chunks.TryGetValue(GetChunkID(pos), out var chunk);

			return chunk;
		}

		private void ShowChunksWithinView()
		{
			int ratio = Mathf.CeilToInt(_playerViewDistance / _chunkSize.x);

			//Debug.Log(ratio);

			// Get the Chunks enveloping the player.
			for (int z = -ratio; z < +ratio; ++z)
			for (int x = -ratio; x < +ratio; ++x)
			{
				//if (x < 0 || z < 0) continue;  // TODO: Remove
				var chunkId = GetChunkID(new Vector3(x * _chunkSize.x, 0, z * _chunkSize.y) + _playerTransform.position);
				var chunkPos = new Vector3Int(chunkId.x * _chunkSize.x, 0, chunkId.y * _chunkSize.y);

				if (_chunks.TryGetValue(chunkId, out var chunk))
				{
					if (!chunk.gameObject.activeInHierarchy)
					{
						//Debug.Log("Reactivating chunk!");
						chunk.gameObject.SetActive(true);
					}	
				}
				else
				{  // We have to create this Chunk.
					//Debug.Log("Spawning Chunk " + chunkId);
					chunk = Instantiate(_chunkPrefab, chunkPos, Quaternion.identity, this.transform);
					chunk.Load(false, new System.Threading.CancellationToken());

					_chunks.Add(chunkId, chunk);
				}
			}
		}
	}
}
