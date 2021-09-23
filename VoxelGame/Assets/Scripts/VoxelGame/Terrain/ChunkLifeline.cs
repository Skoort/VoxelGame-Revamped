using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace VoxelGame.Terrain
{ 
	public class ChunkLifeline
	{
		public Chunk Chunk { get; set; }

		public float TimeSinceLastTick;

		public CancellationToken DestroyRequestedToken;
	}
}
