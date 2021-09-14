using UnityEngine;

namespace VoxelGame.Terrain
{
	public class VoxelData : ScriptableObject
	{
		[SerializeField] private BiomeData _biome = null;

		[SerializeField] private Vector2Int _faceTopAtlasPos = default;
		[SerializeField] private Vector2Int _faceBotAtlasPos = default;
		[SerializeField] private Vector2Int _faceSideAtlasPos = default;

		[SerializeField] private Vector3Int[] _vertexLayout = default;
		[SerializeField] private int[] _vertexIndicesFacePosX = default;  // right
		[SerializeField] private int[] _vertexIndicesFaceNegX = default;  // left
		[SerializeField] private int[] _vertexIndicesFacePosY = default;  // up
		[SerializeField] private int[] _vertexIndicesFaceNegY = default;  // down
		[SerializeField] private int[] _vertexIndicesFacePosZ = default;  // forward
		[SerializeField] private int[] _vertexIndicesFaceNegZ = default;  // backwards
	}
}
