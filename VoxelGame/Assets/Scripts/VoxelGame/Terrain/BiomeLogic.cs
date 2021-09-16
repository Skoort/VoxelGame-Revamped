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

		public bool HasVoxel(Vector3 voxelWorldPosition)
		{
			var value = GetValue(voxelWorldPosition);
			if (value < 0.5F)
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		public Voxel GetVoxel(Vector3 voxelWorldPosition)
		{
			var value = GetValue(voxelWorldPosition);
			if (value < 0.5F)
			{
				return null;
			}
			else
			{
				return new Voxel(0, 0);
			}
		}

		private float GetValue(Vector3 voxelWorldPosition)
		{
			return PerlinNoise3D.Noise(voxelWorldPosition * 0.1F + _offset);
		}
	}
}
