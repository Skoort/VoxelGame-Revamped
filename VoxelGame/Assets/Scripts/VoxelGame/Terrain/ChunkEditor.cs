using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelGame.Terrain
{ 
	public static class ChunkEditor 
	{
		public static void CreateOrDestroyBlock(Chunk thisChunk, Voxel thisVoxel, Vector3Int thisPositionGlobal, bool requestRedraws, bool requestCollisions)
		{
			// Consider moving the index changing out of BreakUpRect so it is more explicit what is happening here.

			thisChunk.ShouldRedraw = requestRedraws;
			thisChunk.ShouldCalculateCollisions = requestCollisions;

			var thisPosition = thisPositionGlobal.WorldToChunkLocal(thisChunk);

			bool isAirToSolid = thisVoxel.DataId != VoxelData.VoxelType.AIR;

			int thisFaceId = 0;
			foreach (var neighborPositionGlobal in GetNeighboringPositions(thisPositionGlobal))
			{
				var neighborsChunk = ChunkManager.Instance.GetChunk(neighborPositionGlobal);
				var neighborsPosition = neighborPositionGlobal.WorldToChunkLocal(neighborsChunk);
				var neighborsVoxel = neighborsChunk.GetVoxel(neighborsPosition);

				var neighborsFaceId = thisFaceId + (thisFaceId < 3 ? +3 : -3);

				int axis = thisFaceId % 3;

				if (isAirToSolid)
				{
					if (neighborsVoxel == null)
					{
						neighborsVoxel = neighborsChunk.AddVoxelStub(neighborsPosition, VoxelData.VoxelType.AIR, -1);  // TODO: Add proper biome type.
					}

					if (neighborsVoxel.DataId == VoxelData.VoxelType.AIR)
					{
						++neighborsVoxel.NumOfExposedFaces;

						// Create the new face and increment thisVoxel.NumOfExposedFaces.
						var face = thisChunk.Mesher.CreateMeshFace(thisVoxel.Position.ChunkLocalToChunkSlice(axis), thisFaceId);
						thisVoxel.AddFace(thisFaceId, face.MeshIndex);
						thisChunk.Mesher.PositionQuad(face);
					}
					else
					{
						--thisVoxel.NumOfExposedFaces;

						// Breaks up the rect and decrements neighborsVoxel.NumOfExposedFaces.
						neighborsChunk.Mesher.BreakUpRect(neighborsVoxel, neighborsFaceId);
						if (neighborsVoxel.NumOfExposedFaces <= 0)
						{
							neighborsChunk.RemoveVoxel(neighborsPosition);  // The neighboring voxel is safe to remove here, because the only change for the neighbor is this voxel.
						}

						neighborsChunk.ShouldRedraw = requestRedraws;
						neighborsChunk.ShouldCalculateCollisions = requestCollisions;
					}
				}
				else
				{
					if (neighborsVoxel == null)
					{
						neighborsVoxel = neighborsChunk.AddVoxelStub(neighborsPosition, VoxelData.VoxelType.DIRT, -1);  // TODO: Add block type and biome selection.
					}

					if (neighborsVoxel.DataId != VoxelData.VoxelType.AIR)
					{
						++thisVoxel.NumOfExposedFaces;

						// Create the new face and increment neighborsVoxel.NumOfExposedFaces.
						var face = neighborsChunk.Mesher.CreateMeshFace(neighborsVoxel.Position.ChunkLocalToChunkSlice(axis), neighborsFaceId);
						neighborsVoxel.AddFace(neighborsFaceId, face.MeshIndex);
						neighborsChunk.Mesher.PositionQuad(face);

						neighborsChunk.ShouldRedraw = requestRedraws;
						neighborsChunk.ShouldCalculateCollisions = requestCollisions;
					}
					else
					{
						// Breaks up the rect and decrements thisVoxel.NumOfExposedFaces.
						thisChunk.Mesher.BreakUpRect(thisVoxel, thisFaceId);
					}
				}

				++thisFaceId;
			}

			// We have to wait until every single neighboring voxel is calculated, otherwise we may delete prematurely.
			if (thisVoxel.NumOfExposedFaces <= 0)
			{
				thisChunk.RemoveVoxel(thisPosition);  // Remove the voxel. Because the number of exposed faces is 0, we don't need to update its neighbors.
			}
		}

		private static IEnumerable<Vector3Int> GetNeighboringPositions(Vector3Int pos)
		{
			yield return pos + new Vector3Int(+1, 0, 0);
			yield return pos + new Vector3Int(0, +1, 0);
			yield return pos + new Vector3Int(0, 0, +1);
			yield return pos + new Vector3Int(-1, 0, 0);
			yield return pos + new Vector3Int(0, -1, 0);
			yield return pos + new Vector3Int(0, 0, -1);
		}
	}
}
