using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelGame.Terrain.Meshing
{
	public class GreedyMesher
	{
		public Chunk Chunk { get; }

		private int _minHeight;
		public Vector3Int Size { get; private set; }
		
		public Mesh Mesh { get; private set; }

		private Queue<MeshFace> _unusedMeshData;  // Stores indices into MeshData that says 
		private List<MeshFace> _greedyMeshData;  // Basically stores the vertices, face order, normals and UVs.
		private List<int> _faces;
		private List<Vector3> _vertices;
		private List<Vector3> _normals;
		private List<Vector2> _uvs;

		public void MarkDirty()
		{
			++DirtyCount;
		}

		public int DirtyCount { get; private set; }
		public int MaxDirtyCountBeforeRegenerate { get; } = 15;

		public GreedyMesher(Chunk chunk)
		{
			this.Chunk = chunk;
		}

		public Predicate<Vector3Int> IsVisible;

		private void ClearMeshBuffers()
		{
			_unusedMeshData = new Queue<MeshFace>();
			_greedyMeshData = new List<MeshFace>();
			_faces = new List<int>();
			_vertices = new List<Vector3>();
			_normals = new List<Vector3>();
			_uvs = new List<Vector2>();
			DirtyCount = 0;
		}

		public void GenerateMesh()
		{
			ClearMeshBuffers();

			// The heights are not fixed. We must get the bounds each time we generate the mesh.
			Size = new Vector3Int(
				ChunkManager.Instance.ChunkSize.x,
				Chunk.MaxHeight - Chunk.MinHeight + 1,
				ChunkManager.Instance.ChunkSize.y);
			_minHeight = Chunk.MinHeight;

			// The iteration order here and TransformSpace must ensure that the algorithms assumptions
			// about iterating on the slice space (left to right, then bottom to top) are met.
			for (int z = 0; z < Size.z; ++z)
			for (int y = 0; y < Size.y; ++y)
			for (int x = 0; x < Size.x; ++x)
			{
				var position = new Vector3Int(x, y + _minHeight, z);
				CreateFacesAtPosition(position);
			}

			PositionQuads(_greedyMeshData);
		}

		private void CreateFacesAtPosition(Vector3Int position)
		{
			var voxel = Chunk.GetVoxel(position);
			if (voxel == null || voxel.DataId == VoxelData.VoxelType.AIR)
			{
				return;
			}

			// Iterate over each face of the voxel.
			for (int faceIndex = 0; faceIndex < 6; ++faceIndex)
			{
				CreateFaceAtPosition(voxel, position, faceIndex);
			}
		}

		private void CreateFaceAtPosition(Voxel voxel, Vector3Int position, int faceIndex)
		{
			// The faces are ordered in such a way that the axis each face spans can be calculated, as
			// well as whether each face is positive or negative.
			var axis = faceIndex % 3;
			var dir = faceIndex < 3 ? 0 : 1;

			var sliceSpacePos = position.ChunkLocalToChunkSlice(axis);

			// A face should only be drawn if the voxel in front/behind (relative) this one is AIR.
			var voxelBehind = Chunk.GetVoxel((sliceSpacePos + new Vector3Int(0, 0, dir == 0 ? +1 : -1)).ChunkSliceToChunkLocal(axis));
			if (voxelBehind?.DataId == VoxelData.VoxelType.AIR)
			{
				var botNeighbor = Chunk.GetVoxel((sliceSpacePos - Vector3Int.up).ChunkSliceToChunkLocal(axis));
				var lftNeighbor = Chunk.GetVoxel((sliceSpacePos - Vector3Int.right).ChunkSliceToChunkLocal(axis));

				var botMesh = (botNeighbor != null && botNeighbor.FaceIndices[faceIndex] != -1)
					? _greedyMeshData[botNeighbor.FaceIndices[faceIndex]]
					: null;
				var lftMesh = (lftNeighbor != null && lftNeighbor.FaceIndices[faceIndex] != -1)
					? _greedyMeshData[lftNeighbor.FaceIndices[faceIndex]]
					: null;

				MeshFace usedMesh = null;
				if (botMesh != null && botMesh.Scale.x == 1)
				{
					// The bottom (relative) voxel's rect is only 1 unit wide. Extend it upwards by 1. Extending
					// a rect wider than 1 unit upwards is handled below by creating another intermediate rect.

					++botMesh.Scale.y;
					usedMesh = botMesh;
				}

				if (lftMesh != null && lftMesh.Scale.y == 1 && usedMesh == null)
				{
					// Because of the way we iterate, the rect grows to the right as much as possible
					// before exploring a way to grow upwards. Therefore, a rect that has grown
					// upwards cannot be grown to the right anymore.
					// Extending the left (relative) voxel's rect to the right by 1.

					++lftMesh.Scale.x;

					if (botMesh != null
					&& lftMesh.SliceSpacePosition.x == botMesh.SliceSpacePosition.x
					&& lftMesh.Scale.x == botMesh.Scale.x)
					{
						// Extending the left voxel's rect caused it to be merged with the bottom voxel's rect.
						++botMesh.Scale.y;

						// Recycle the left voxel's rect.
						RecycleMeshFace(lftMesh);
						for (int i = 1; i < lftMesh.Scale.x; ++i)
						{
							var voxelToFix = Chunk.GetVoxel((sliceSpacePos - new Vector3Int(i, 0, 0)).ChunkSliceToChunkLocal(axis));  // Can probably avoid a call to TransformSpace here.
							voxelToFix.FaceIndices[faceIndex] = botMesh.MeshIndex;
						}

						usedMesh = botMesh;
					}
					else
					{
						usedMesh = lftMesh;
					}
				}

				if (usedMesh == null)
				{
					// Create a brand new rect for this voxel.
					usedMesh = CreateMeshFace(sliceSpacePos, faceIndex);
				}

				voxel.AddFace(faceIndex, usedMesh.MeshIndex);
			}
		}

		public MeshFace CreateMeshFace(Vector3Int sliceSpacePos, int voxelFaceIndex)
		{
			MeshFace meshData = null;
			if (_unusedMeshData.Count > 0)
			{
				meshData = _unusedMeshData.Dequeue();
				// Unused MeshData still has its old rect info except for zeroed out vertices to hide the mesh.
				for (int i = 0; i < 4; ++i)
				{
					int index = i + meshData.MeshIndex * 4;
					_vertices[index] = VoxelData.Vertices[voxelFaceIndex][i];
					_normals[index] = VoxelData.Normals[voxelFaceIndex][i];
					_uvs[index] = VoxelData.UVs2[voxelFaceIndex][i];
				}
			}
			else
			{
				meshData = new MeshFace() { MeshIndex = _greedyMeshData.Count };
				_greedyMeshData.Add(meshData);
				_vertices.AddRange(VoxelData.Vertices[voxelFaceIndex]);
				_normals.AddRange(VoxelData.Normals[voxelFaceIndex]);
				_uvs.AddRange(VoxelData.UVs2[voxelFaceIndex]);
				_faces.AddRange(Enumerable.Range(0, 4).Select(i => i + meshData.MeshIndex * 4));
			}
			meshData.Scale = Vector3Int.one;
			meshData.SliceDimension = voxelFaceIndex % 3;
			meshData.SliceSpacePosition = sliceSpacePos;
			meshData.FaceId = voxelFaceIndex;

			return meshData;
		}

		private void RecycleMeshFace(MeshFace face)
		{
			var vertexStartIndex = face.MeshIndex * 4;
			for (int i = vertexStartIndex; i < vertexStartIndex + 4; ++i)
			{
				_vertices[i] = Vector3.zero;
			}

			_unusedMeshData.Enqueue(face);
		}

		private void PositionQuads(IEnumerable<MeshFace> quads)
		{
			foreach (var quad in quads)
			{
				PositionQuad(quad);
			}
		}

		public void PositionQuad(MeshFace quad)
		{
			var absPosition = quad.SliceSpacePosition.ChunkSliceToChunkLocal(quad.SliceDimension);
			var localSliceScales = quad.Scale.ChunkSliceToChunkLocal(quad.SliceDimension);

			for (int i = 0; i < 4; ++i)
			{
				var vertexIndex = quad.MeshIndex * 4 + i;

				_vertices[vertexIndex] = Vector3Int.FloorToInt(_vertices[vertexIndex]) * localSliceScales + absPosition;
			}
		}

		public void ShowMesh(MeshFilter meshFilter)
		{
			Mesh = new Mesh();
			Mesh.SetVertices(_vertices);
			Mesh.SetNormals(_normals);
			Mesh.SetUVs(0, _uvs);
			Mesh.SetIndices(_faces, MeshTopology.Quads, 0);

			meshFilter.mesh = Mesh;
		}

		public void BreakUpRect(Voxel voxelToRemove, int faceIdToRemove)
		{
			var oldRect = _greedyMeshData[voxelToRemove.FaceIndices[faceIdToRemove]];

			var positionToRemoveSliceSpace = voxelToRemove.Position.ChunkLocalToChunkSlice(oldRect.SliceDimension);

			// Reassign the remaining voxels to new rectangles.
			CreateCornerRects(positionToRemoveSliceSpace, oldRect,
				out var topRect, 
				out var bottomRect, 
				out var rightRect, 
				out var leftRect);

			AssignCornerRects(positionToRemoveSliceSpace, 
				oldRect, 
				topRect, 
				bottomRect, 
				rightRect, 
				leftRect);

			// Destroy the old rectangle and remove this voxel's face index. Do this last so it isn't
			// recycled while it is still needed.
			RecycleMeshFace(oldRect);
			voxelToRemove.RemFace(faceIdToRemove);
		}

		private void CreateCornerRects(Vector3Int positionToRemoveSliceSpace, MeshFace oldRect, 
			out MeshFace topRect, 
			out MeshFace bottomRect, 
			out MeshFace rightRect, 
			out MeshFace leftRect)
		{
			topRect = null;
			leftRect = null;
			rightRect = null;
			bottomRect = null;
			if (positionToRemoveSliceSpace.y < oldRect.SliceSpacePosition.y + oldRect.Scale.y - 1)
			{
				var bottomLeftCorner = new Vector3Int(oldRect.SliceSpacePosition.x, positionToRemoveSliceSpace.y + 1, oldRect.SliceSpacePosition.z);
				topRect = CreateMeshFace(bottomLeftCorner, oldRect.FaceId);
				topRect.Scale = new Vector3Int(
					oldRect.Scale.x,
					oldRect.Scale.y - (positionToRemoveSliceSpace.y - oldRect.SliceSpacePosition.y + 1),
					1);
				PositionQuad(topRect);
			}
			if (positionToRemoveSliceSpace.y > oldRect.SliceSpacePosition.y)
			{
				var bottomLeftCorner = new Vector3Int(positionToRemoveSliceSpace.x, oldRect.SliceSpacePosition.y, oldRect.SliceSpacePosition.z);
				bottomRect = CreateMeshFace(bottomLeftCorner, oldRect.FaceId);
				bottomRect.Scale = new Vector3Int(
					1,
					positionToRemoveSliceSpace.y - oldRect.SliceSpacePosition.y,
					1);
				PositionQuad(bottomRect);
			}
			if (positionToRemoveSliceSpace.x < oldRect.SliceSpacePosition.x + oldRect.Scale.x - 1)
			{
				var bottomLeftCorner = new Vector3Int(positionToRemoveSliceSpace.x + 1, oldRect.SliceSpacePosition.y, oldRect.SliceSpacePosition.z);
				rightRect = CreateMeshFace(bottomLeftCorner, oldRect.FaceId);
				rightRect.Scale = new Vector3Int(
					oldRect.Scale.x - (positionToRemoveSliceSpace.x - oldRect.SliceSpacePosition.x + 1),
					positionToRemoveSliceSpace.y - oldRect.SliceSpacePosition.y + 1,
					1);
				PositionQuad(rightRect);
			}
			if (positionToRemoveSliceSpace.x > oldRect.SliceSpacePosition.x)
			{
				var bottomLeftCorner = oldRect.SliceSpacePosition;
				leftRect = CreateMeshFace(bottomLeftCorner, oldRect.FaceId);
				leftRect.Scale = new Vector3Int(
					positionToRemoveSliceSpace.x - oldRect.SliceSpacePosition.x,
					positionToRemoveSliceSpace.y - oldRect.SliceSpacePosition.y + 1,
					1);
				PositionQuad(leftRect);
			}
		}

		private void AssignCornerRects(Vector3Int centralSliceSpacePosition,
			MeshFace oldRect, 
			MeshFace topRect, 
			MeshFace bottomRect, 
			MeshFace rightRect, 
			MeshFace leftRect)
		{
			for (int y = 0; y < oldRect.Scale.y; ++y)
			for (int x = 0; x < oldRect.Scale.x; ++x)
			{
				var sliceSpacePosition = oldRect.SliceSpacePosition + new Vector3Int(x, y, 0);

				var voxel = Chunk.GetVoxel(sliceSpacePosition.ChunkSliceToChunkLocal(oldRect.SliceDimension));

				// Top has priority over left and right, and left and right have priority over bottom.
				if (sliceSpacePosition.y > centralSliceSpacePosition.y)
				{
					voxel.FaceIndices[oldRect.FaceId] = topRect.MeshIndex;
				} else
				if (sliceSpacePosition.x > centralSliceSpacePosition.x)
				{
					voxel.FaceIndices[oldRect.FaceId] = rightRect.MeshIndex;
				} else
				if (sliceSpacePosition.x < centralSliceSpacePosition.x)
				{
					voxel.FaceIndices[oldRect.FaceId] = leftRect.MeshIndex;
				} else
				if (sliceSpacePosition.y < centralSliceSpacePosition.y)
				{
					voxel.FaceIndices[oldRect.FaceId] = bottomRect.MeshIndex;
				}
			}
		}
	}
}
