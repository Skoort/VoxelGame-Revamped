using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain
{ 
    public static class VectorExtensions
    {
        public static Vector3Int WorldToChunkLocal(this Vector3Int position, Chunk chunk)
        {
            return position - chunk.Position;
        }

        public static Vector3Int ChunkLocalToWorld(this Vector3Int position, Chunk chunk)
        {
            return position + chunk.Position;
        }

        //    axis  |  sliceX  |  sliceY
        // ---------+----------+----------
        //    X (0) |    Y     |    Z 
        //    Y (1) |    X     |    Z   
        //    Z (2) |    X     |    Y   
        // Transforms from local space to slice space.
        public static Vector3Int ChunkLocalToChunkSlice(this Vector3Int position, int axis)
        {
            int x = axis != 0 ? position.x : position.y;
            int y = axis != 2 ? position.z : position.y;
            int z = axis == 0
                ? position.x
                : axis == 1
                    ? position.y
                    : position.z;
            return new Vector3Int(x, y, z);
        }

        //    axis  |  localX  |  localY  |  localZ
        // ---------+----------+----------+----------
        //    X (0) |     Z    |     X    |     Y
        //    Y (1) |     X    |     Z    |     Y
        //    Z (2) |     X    |     Y    |     Z
        // Transforms from slice space to local space.
        public static Vector3Int ChunkSliceToChunkLocal(this Vector3Int position, int axis)
        {
            int x = axis != 0 ? position.x : position.z;
            int z = axis != 2 ? position.y : position.z;
            int y = axis == 0
                ? position.x
                : axis == 1
                    ? position.z
                    : position.y;
            return new Vector3Int(x, y, z);
        }
    }
}
