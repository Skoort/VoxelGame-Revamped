using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain
{ 
	public class BiomeLogic
	{
		private Vector3 _offset;
		private Vector3 _offset2;
		private Vector3 _offset3;
		private Vector3 _offset4;
		private Vector3 _offset5;
		private Vector3 _offset6;

		public BiomeLogic(int seed)
		{
			Random.InitState(seed);
			_offset = Random.insideUnitSphere * 20000;
			_offset2 = Random.insideUnitSphere * 20000;
			_offset3 = Random.insideUnitSphere * 20000;
			_offset4 = Random.insideUnitSphere * 20000;
			_offset5 = Random.insideUnitSphere * 20000;

			Debug.Log(_offset);
			Debug.Log(_offset2);
			Debug.Log(_offset3);
			Debug.Log(_offset4);
			Debug.Log(_offset5);
		}

		private int GetFlatlandHeight(Vector3 voxelWorldPosition, Vector3 swappedVoxelWorldPosition)
		{
			var flatlandPos = (voxelWorldPosition + _offset3) * 0.01F;
			return Mathf.FloorToInt(Mathf.PerlinNoise(flatlandPos.x, flatlandPos.z) * 10);
		}

		private int GetHillsHeight(float biomeVal, Vector3 voxelWorldPosition, Vector3 swappedVoxelWorldPosition)
		{
			var hillsPos = Vector3.Scale(swappedVoxelWorldPosition + _offset2, new Vector3(0.02F, 0, -0.02F));

			var minHillHeight = 15F;
			var maxHillHeight = 55F;
			var delta = maxHillHeight - minHillHeight;

			var hillsHeight = Mathf.FloorToInt(Mathf.PerlinNoise(hillsPos.x, hillsPos.z) * delta + minHillHeight);
			if (biomeVal < 0.65F)
			{
				var distanceFromFlatlandRatio = (biomeVal - 0.4F) / 0.25F;
				var flatlandsHeight = GetFlatlandHeight(voxelWorldPosition, swappedVoxelWorldPosition);
				hillsHeight = Mathf.FloorToInt(hillsHeight * distanceFromFlatlandRatio + flatlandsHeight * (1 - distanceFromFlatlandRatio));
			}
			return hillsHeight;
		}

		private int GetMountainsHeight(float biomeVal, Vector3 voxelWorldPosition, Vector3 swappedVoxelWorldPosition)
		{
			var mountainsPos = (swappedVoxelWorldPosition + _offset5) * 0.005F;

			var minMountainHeight = 110F;
			var maxMountainHeight = 140F;
			var delta = maxMountainHeight - minMountainHeight;

			var mountainsHeight = Mathf.FloorToInt(Mathf.PerlinNoise(mountainsPos.x, mountainsPos.z) * delta + minMountainHeight);

			if (biomeVal < 0.90F)
			{
				var distanceFromHillsRatio = (biomeVal - 0.7F) / 0.2F;
				distanceFromHillsRatio *= distanceFromHillsRatio;
				var hillsHeight = GetHillsHeight(biomeVal, voxelWorldPosition, swappedVoxelWorldPosition);
				mountainsHeight = Mathf.FloorToInt(mountainsHeight * distanceFromHillsRatio + hillsHeight * (1 - distanceFromHillsRatio));
			}
			return mountainsHeight;
		}

		private int GetPlateauHeight(float biomeVal, Vector3 voxelWorldPosition, Vector3 swappedVoxelWorldPosition)
		{
			var mountainsPos = (swappedVoxelWorldPosition + _offset5) * 0.005F;

			var minMountainHeight = 60F;
			var maxMountainHeight = 70F;
			var delta = maxMountainHeight - minMountainHeight;

			var mountainsHeight = Mathf.FloorToInt(Mathf.PerlinNoise(mountainsPos.x, mountainsPos.z) * delta + minMountainHeight);

			if (biomeVal < 0.7F)
			{
				var distanceFromHillsRatio = (biomeVal - 0.6F) / 0.1F;
				distanceFromHillsRatio *= distanceFromHillsRatio;
				var hillsHeight = GetHillsHeight(biomeVal, voxelWorldPosition, swappedVoxelWorldPosition);
				mountainsHeight = Mathf.FloorToInt(mountainsHeight * distanceFromHillsRatio + hillsHeight * (1 - distanceFromHillsRatio));
			}
			return mountainsHeight;
		}

		public VoxelData.VoxelType GetVoxelType(Vector3Int voxelWorldPosition, int heightAtThisPosition)
		{
			var transformedPos = Vector3.Scale(voxelWorldPosition + _offset2, new Vector3(+0.0316F, -0.0356F));
			var dirtyDepth = Mathf.FloorToInt(Mathf.PerlinNoise(transformedPos.x, transformedPos.z) * 5) + 1;

			if (heightAtThisPosition == voxelWorldPosition.y)
			{
				return VoxelData.VoxelType.GRASS;
			} else
			if (heightAtThisPosition - voxelWorldPosition.y < dirtyDepth)
			{
				return VoxelData.VoxelType.DIRT;
			}
			else
			{
				return VoxelData.VoxelType.STONE;
			}
		}

		public int GetHeight(Vector3 voxelWorldPosition)
		{
			//if (voxelWorldPosition == new Vector3(0, 0, 0))
			//{
			//	return 1;
			//}
			//if (voxelWorldPosition == new Vector3(1, 0, 0))
			//{
			//	return 1;
			//}
			//return 0;

			var swappedVoxelWorldPosition = new Vector3(voxelWorldPosition.z, 0, voxelWorldPosition.x);

			var biomePos1 = Vector3.Scale(voxelWorldPosition + _offset, new Vector3(+0.005F, -0.001F));
			var biomePos2 = Vector3.Scale(swappedVoxelWorldPosition + _offset2, new Vector3(-0.004F, -0.003F));
			var cliffPos = Vector3.Scale(voxelWorldPosition + _offset2, new Vector3(-0.001F, +0.002F));


			var isCliffZone = Mathf.PerlinNoise(cliffPos.x, cliffPos.y) > 0.5F;

			var biomeVal = (Mathf.PerlinNoise(biomePos1.x, biomePos1.y) + Mathf.PerlinNoise(biomePos2.x, biomePos2.y)) * 0.5F;
			if (biomeVal < 0.40F)
			{
				return GetFlatlandHeight(voxelWorldPosition, swappedVoxelWorldPosition);
			}
			else
			if (isCliffZone)
			{
				if (biomeVal < 0.6F)
				{
					return GetHillsHeight(biomeVal, voxelWorldPosition, swappedVoxelWorldPosition);
				}
				else
				{
					return GetPlateauHeight(biomeVal, voxelWorldPosition, swappedVoxelWorldPosition);
				}
			}
			else
			{
				if (biomeVal < 0.7F)
				{
					return GetHillsHeight(biomeVal, voxelWorldPosition, swappedVoxelWorldPosition);
				}
				else
				{
					return GetMountainsHeight(biomeVal, voxelWorldPosition, swappedVoxelWorldPosition);
				}
			}
		}
	}
}
