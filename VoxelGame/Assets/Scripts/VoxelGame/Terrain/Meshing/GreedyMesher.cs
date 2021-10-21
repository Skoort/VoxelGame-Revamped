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
		}

		public void GenerateMesh()
		{
			ClearMeshBuffers();

			// The heights are not fixed. We must get the bounds each time we generate the mesh.
			Size = new Vector3Int(
				ChunkManager.Instance.ChunkSize.x,
				Chunk.MaxHeight,
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

			PositionMeshSlices();
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

			var sliceSpacePos = TransformToSliceSpace(axis, position);

			// A face should only be drawn if the voxel in front/behind (relative) this one is AIR.
			var voxelBehind = Chunk.GetVoxel(TransformToLocalSpace(axis, sliceSpacePos + new Vector3Int(0, 0, dir == 0 ? +1 : -1)));
			if (voxelBehind?.DataId == VoxelData.VoxelType.AIR)
			{
				var botNeighbor = Chunk.GetVoxel(TransformToLocalSpace(axis, sliceSpacePos - Vector3Int.up));
				var lftNeighbor = Chunk.GetVoxel(TransformToLocalSpace(axis, sliceSpacePos - Vector3Int.right));

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
							var voxelToFix = Chunk.GetVoxel(TransformToLocalSpace(axis, sliceSpacePos - new Vector3Int(i, 0, 0)));  // Can probably avoid a call to TransformSpace here.
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
					usedMesh = CreateMeshFace(sliceSpacePos, axis, dir);
				}

				voxel.AddFace(faceIndex, usedMesh.MeshIndex);
			}
		}

		//    axis  |  sliceX  |  sliceY
		// ---------+----------+----------
		//    X (0) |    Y     |    Z 
		//    Y (1) |    X     |    Z   
		//    Z (2) |    X     |    Y   
		// Transforms from local space to slice space.
		Vector3Int TransformToSliceSpace(int axis, Vector3Int pos)
		{
			int x = axis != 0 ? pos.x : pos.y;
			int y = axis != 2 ? pos.z : pos.y;
			int z = axis == 0
				? pos.x
				: axis == 1
					? pos.y
					: pos.z;
			return new Vector3Int(x, y, z);
		}

		//    axis  |  localX  |  localY  |  localZ
		// ---------+----------+----------+----------
		//    X (0) |     Z    |     X    |     Y
		//    Y (1) |     X    |     Z    |     Y
		//    Z (2) |     X    |     Y    |     Z
		// Transforms from slice space to local space.
		Vector3Int TransformToLocalSpace(int axis, Vector3Int pos)
		{
			int x = axis != 0 ? pos.x : pos.z;
			int z = axis != 2 ? pos.y : pos.z;
			int y = axis == 0
				? pos.x
				: axis == 1
					? pos.z
					: pos.y;
			return new Vector3Int(x, y, z);
		}

		private MeshFace CreateMeshFace(Vector3Int sliceSpacePos, int sliceDimension, int plusOrMinus)
		{
			var voxelFaceIndex = sliceDimension + plusOrMinus * 3;

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
			meshData.SliceDimension = sliceDimension;
			meshData.SliceSpacePosition = sliceSpacePos;

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

		private void PositionMeshSlices()
		{
			foreach (var rect in _greedyMeshData)
			{
				var absPosition = TransformToLocalSpace(rect.SliceDimension, rect.SliceSpacePosition);
				var localSliceScales = TransformToLocalSpace(rect.SliceDimension, rect.Scale);

				for (int i = 0; i < 4; ++i)
				{
					var vertexIndex = rect.MeshIndex * 4 + i;

					_vertices[vertexIndex] = Vector3Int.FloorToInt(_vertices[vertexIndex]) * localSliceScales + absPosition;
				}
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
	}
}
