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

		public static int[] Faces { get; } = { 0, 1, 2, 3 };

		public static Vector3[][] Vertices { get; } = new Vector3[6][]
		{
			// +X
			new Vector3[4] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
			// +Y
			new Vector3[4] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
			// +Z
			new Vector3[4] { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) },
			// -X
			new Vector3[4] { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0) },
			// -Y
			new Vector3[4] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
			// -Z
			new Vector3[4] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
		};

		public static Vector2[][] UVs2 { get; } = new Vector2[6][]
		{
			// +X
			new Vector2[4] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), },
			// +Y
			new Vector2[4] { new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), },
			// +Z
			new Vector2[4] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), },
			// -X
			new Vector2[4] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), },
			// -Y
			new Vector2[4] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), },
			// -Z
			new Vector2[4] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), },
		};
		
		public static Vector3[][] Normals { get; } = new Vector3[6][]
		{
			// +X
			new Vector3[4] { new Vector3(+1,  0,  0), new Vector3(+1,  0,  0), new Vector3(+1,  0,  0), new Vector3(+1,  0,  0), },
			// +Y
			new Vector3[4] { new Vector3( 0, +1,  0), new Vector3( 0, +1,  0), new Vector3( 0, +1,  0), new Vector3( 0, +1,  0), },
			// +Z
			new Vector3[4] { new Vector3( 0,  0, +1), new Vector3( 0,  0, +1), new Vector3( 0,  0, +1), new Vector3( 0,  0, +1), },
			// -X
			new Vector3[4] { new Vector3(-1,  0,  0), new Vector3(-1,  0,  0), new Vector3(-1,  0,  0), new Vector3(-1,  0,  0), },
			// -Y
			new Vector3[4] { new Vector3( 0, -1,  0), new Vector3( 0, -1,  0), new Vector3( 0, -1,  0), new Vector3( 0, -1,  0), },
			// -Z
			new Vector3[4] { new Vector3( 0,  0, -1), new Vector3( 0,  0, -1), new Vector3( 0,  0, -1), new Vector3( 0,  0, -1), },
		};
	}
}
