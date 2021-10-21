using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelGame.Terrain.Meshing
{
	public class MeshFace
	{
		public int MeshIndex { get; set; }  // Points to the actual 3D data.

		public int SliceDimension { get; set; }  // 0, 1, or 2 (used when you have to cut the rectangle up).
		public Vector3Int SliceSpacePosition;
		public Vector3Int Scale;
	}
}
