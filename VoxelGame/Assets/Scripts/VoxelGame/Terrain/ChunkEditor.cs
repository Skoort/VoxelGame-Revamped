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

			var affectedChunks = new HashSet<Chunk>();
			affectedChunks.Add(thisChunk);

			var thisPosition = thisPositionGlobal.WorldToChunkLocal(thisChunk);

			var isAirToSolid = thisVoxel.VoxelType != VoxelData.VoxelType.AIR;  // Requires you to set the voxel's type to the new type before calling this method.

			int thisFaceId = 0;
			foreach (var neighborsPositionGlobal in GetNeighboringPositions(thisPositionGlobal))
			{
				var neighborsChunk = ChunkManager.Instance.GetChunk(neighborsPositionGlobal);
				var neighborsPosition = neighborsPositionGlobal.WorldToChunkLocal(neighborsChunk);
				var neighborsVoxel = neighborsChunk.GetVoxel(neighborsPosition);

				var neighborsFaceId = thisFaceId + (thisFaceId < 3 ? +3 : -3);

				int axis = thisFaceId % 3;

				if (isAirToSolid)
				{
					if (neighborsVoxel == null)
					{
						neighborsVoxel = neighborsChunk.AddVoxelStub(neighborsPosition, VoxelData.VoxelType.AIR, -1);  // TODO: Add proper biome type.
					}

					if (neighborsVoxel.VoxelType == VoxelData.VoxelType.AIR)
					{
						++neighborsVoxel.NumOfExposedFaces;

						// Create the new face and increment thisVoxel.NumOfExposedFaces.
						var face = thisChunk.Mesher.CreateMeshFace(thisVoxel.Position.ChunkLocalToChunkSlice(axis), thisFaceId, thisVoxel.VoxelType);
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
							// TODO: Make it that it forgets the voxel if it's the same as it's predicted biome block
							// type. Otherwise save it. When generating an unearthed block's block type, check to see 
							// if the voxel exists but is just not exposed. Reveal it, otherwise create it from biome.
							neighborsChunk.RemoveHeight(neighborsVoxel.Position.y);
							neighborsChunk.RemoveVoxel(neighborsPosition);  // The neighboring voxel is safe to remove here, because the only change for the neighbor is this voxel.
						}

						affectedChunks.Add(neighborsChunk);
					}
				}
				else
				{
					if (neighborsVoxel == null)
					{
						var height = neighborsChunk.GetHeightmapValue(neighborsPosition.x, neighborsPosition.z);
						var voxelType = ChunkManager.Instance.BiomeLogic.GetVoxelType(neighborsPositionGlobal, height);
						neighborsVoxel = neighborsChunk.AddVoxelStub(neighborsPosition, voxelType, -1);  // TODO: Add block type and biome selection.
						neighborsChunk.AddHeight(neighborsVoxel.Position.y);
					}

					if (neighborsVoxel.VoxelType != VoxelData.VoxelType.AIR)
					{
						++thisVoxel.NumOfExposedFaces;

						// Create the new face and increment neighborsVoxel.NumOfExposedFaces.
						var face = neighborsChunk.Mesher.CreateMeshFace(neighborsVoxel.Position.ChunkLocalToChunkSlice(axis), neighborsFaceId, neighborsVoxel.VoxelType);
						neighborsVoxel.AddFace(neighborsFaceId, face.MeshIndex);
						neighborsChunk.Mesher.PositionQuad(face);

						affectedChunks.Add(neighborsChunk);
					}
					else
					{
						// Breaks up the rect and decrements thisVoxel.NumOfExposedFaces.
						thisChunk.Mesher.BreakUpRect(thisVoxel, thisFaceId);
					}
				}

				++thisFaceId;
			}

			if (isAirToSolid && thisVoxel.NumOfExposedFaces > 0)
			{
				// We created a new solid block. As long as this solid block is still visible (we have to make
				// sure we didn't seal up a room from the inside, removing the block), we need to increment the 
				// height.
				thisChunk.AddHeight(thisPosition.y);
			} else
			if (!isAirToSolid)
			{
				// We turned a solid block into an air block, decrement the height.
				thisChunk.RemoveHeight(thisPosition.y);
			}

			// Check to see if this voxel can be removed. We have to wait until every single neighboring voxel
			// updates this voxel's number of exposed faces, otherwise we may delete it prematurely.
			if (thisVoxel.NumOfExposedFaces <= 0)
			{
				thisChunk.RemoveVoxel(thisPosition);
			}

			foreach (var chunk in affectedChunks)
			{
				chunk.ShouldRedraw = requestRedraws;
				chunk.ShouldCalculateCollisions = requestCollisions;
				chunk.Mesher.MarkDirty();
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
