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
		private Dictionary<Vector3Int, Chunk> _chunks;

		[SerializeField] private VoxelData[] _voxelDatas = null;

		public BiomeLogic BiomeLogic { get; private set; }

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
			for (int z = 0; z < 26; ++z)
			for (int y = 0; y < 26; ++y)
			for (int x = 0; x < 26; ++x)  // TODO: Implement way to control the number of chunks.
			{
				var _relativePos = new Vector3Int(x, y, z);
				var _absolutePos = _relativePos * _chunkSize;
				var chunk = Instantiate<Chunk>(_chunkPrefab, _absolutePos, Quaternion.identity, this.transform);
					
				chunk.Init(_chunkSize);

				_chunks.Add(_relativePos, chunk);
			}
		}
	}
}
