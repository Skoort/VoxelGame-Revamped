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

			//_tempVoxels = new Dictionary<Vector3, Voxel>();
			//_tempVoxels.Add(new Vector3(0, 0, 0), new Voxel(0, 0));  //
			//_tempVoxels.Add(new Vector3(0, 0, 1), new Voxel(0, 0));  //  X
			//_tempVoxels.Add(new Vector3(0, 0, 2), new Voxel(0, 0));  // XXX 
			//_tempVoxels.Add(new Vector3(0, 1, 1), new Voxel(0, 0));  //
		}

		//private Dictionary<Vector3, Voxel> _tempVoxels;

		public bool HasVoxel(Vector3 voxelWorldPosition)
		{
			//return _tempVoxels.ContainsKey(voxelWorldPosition);
			var value = GetValue(voxelWorldPosition);
			if (value < 0.5f)
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
			//_tempVoxels.TryGetValue(voxelWorldPosition, out Voxel voxel);
			//return voxel;
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
