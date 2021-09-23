using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace VoxelGame.Terrain
{ 
    public class ChunkLoader
    {
        private SimplePriorityQueue<Chunk> _chunkLoadQueue;

        private readonly int _loadXChunksPerSecond;
        private readonly float _timeBetweenLoads;
        private float _loadCountdown;

        public ChunkLoader(int loadXChunksPerSecond)
        {
			_chunkLoadQueue = new SimplePriorityQueue<Chunk>();

            _loadXChunksPerSecond = loadXChunksPerSecond;
            _timeBetweenLoads = 1 / (float) _loadXChunksPerSecond;
            _loadCountdown = _timeBetweenLoads;
        }

        public void ScheduleForLoad(Chunk chunk)
        {
            // Calculate the chunks distance from the nearest player.
            var chunkPriority = 0;

            _chunkLoadQueue.Enqueue(chunk, chunkPriority);
        }

        public void ScheduleForCleanup(Chunk chunk)
        { 
        
        }

        public void Update(float deltaTime)
        {
            _loadCountdown -= deltaTime;
            if (_loadCountdown <= 0)
            {
                //chunk.

                _loadCountdown = _timeBetweenLoads;
            }
        }
    }
}
