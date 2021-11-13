using UnityEngine;

namespace VoxelGame.Terrain
{
	public class Voxel
	{
		public Voxel(Vector3Int position, VoxelData.VoxelType dataId, int biomeId)
		{
			Position = position;
			VoxelType = dataId;
			BiomeId = biomeId;
		}

		public Vector3Int Position { get; private set; }

		public VoxelData.VoxelType VoxelType { get; set; }

		public int BiomeId { get; private set; }  // Cannot change biome.

		public int[] FaceIndices { get; } = new int[6] { -1, -1, -1, -1, -1, -1 };

		public int NumOfExposedFaces { get; set; }

		public void AddFace(int faceId, int faceIndex)
		{
			FaceIndices[faceId] = faceIndex;
			++NumOfExposedFaces;
		}

		public void RemFace(int faceId)
		{
			FaceIndices[faceId] = -1;
			--NumOfExposedFaces;
		}
	}
}
