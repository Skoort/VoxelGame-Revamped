using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain
{ 
	public class BiomeLogic
	{
		private Vector3 _offset;

		public BiomeLogic(int seed)
		{
			Random.InitState(seed);
			_offset = Random.insideUnitSphere * 2000000;
		}
		
		public Voxel GetVoxel(Vector3 voxelWorldPosition)
		{
			var value = PerlinNoise3D.Noise(voxelWorldPosition + _offset);
			if (value < 0.5F)
			{
				return null;
			}
			else
			{
				return new Voxel(0, 0);
			}
		}
	}
}
