using UnityEngine;

namespace VoxelGame.Terrain
{
	public static class PerlinNoise3D
	{
		public static float Noise(Vector3 position)
		{
			float xy = Mathf.PerlinNoise(position.x, position.y);
			float xz = Mathf.PerlinNoise(position.x, position.z);
			float yz = Mathf.PerlinNoise(position.y, position.z);
			float yx = Mathf.PerlinNoise(position.y, position.x);
			float zx = Mathf.PerlinNoise(position.z, position.x);
			float zy = Mathf.PerlinNoise(position.z, position.y);

			return (xy + xz + yz + yx + zx + zy) / 6;
		}
	}
}
