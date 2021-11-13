using UnityEngine;

namespace VoxelGame.Terrain.Meshing
{
	public class MeshFace
	{
		public int MeshIndex { get; set; }  // Points to the actual 3D data.

		public int SliceDimension { get; set; }  // 0, 1, or 2 (used when you have to cut the rectangle up).
		public int FaceId { get; set; }

		public Vector3Int SliceSpacePosition;
		public Vector3Int Scale;

		public VoxelData.VoxelType VoxelType { get; set; }
	}
}
