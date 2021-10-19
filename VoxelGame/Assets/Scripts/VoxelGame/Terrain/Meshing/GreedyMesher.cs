using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelGame.Terrain.Meshing
{
	public class GreedyMesher
	{
		public Chunk       Chunk { get; }

		private readonly int[] _lowerBounds = new int[3] { -1, -1, -1 };  // Inclusive
		private readonly int[] _upperBounds = new int[3] { -1, -1, -1 };  // Exclusive

		public Mesh Mesh { get; private set; }

		private Queue<MeshFace> _unusedMeshData;  // Stores indices into MeshData that says 
		private List<MeshFace>  _greedyMeshData;  // Basically stores the vertices, face order, normals and UVs.
		private List<int>     _faces;
		private List<Vector3> _vertices;
		private List<Vector3> _normals;
		private List<Vector2> _uvs;

		public GreedyMesher(Chunk chunk)
		{
			this.Chunk = chunk;
			
			// We create this because we need an indexable upper and lower bounds.
			_lowerBounds[0] = _lowerBounds[2] = 0;
			_upperBounds[0] = ChunkManager.Instance.ChunkSize.x;
			_upperBounds[2] = ChunkManager.Instance.ChunkSize.y;
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
			// The heights are not fixed. We must get the bounds each time we generate the mesh.
			_lowerBounds[1] = Chunk.MinHeight;
			_upperBounds[1] = Chunk.MaxHeight;

			ClearMeshBuffers();

			for (int axis = 0; axis <= 2; ++axis)
			{
				for (int offset = _lowerBounds[axis]; offset < _upperBounds[axis]; ++offset)
				{
					for (int dir = 0; dir <= 1; ++dir)
					{
						CreateMeshSlice(axis, offset, dir);
					}
				}
			}

			PositionMeshSlices();
		}

		//  axis | relAxisX | relAxisY
		// ------+----------+----------
		//    Z  |    X     |    Y   
		//    Y  |    X     |    Z   
		//    X  |    Z     |    Y   
		// Transforms from slice space to local space (by swapping two axes) and vice-versa.
		Vector3Int TransformSpace(int axis, Vector3Int pos)
		{
			int x = axis != 0 ? pos.x : pos.z;
			int y = (axis != 1 ? pos.y : pos.z);
			int z = axis == 0
				? pos.x
				: axis == 1
					? pos.y
					: pos.z;
			return new Vector3Int(x, y, z);
		}
		
		private void CreateMeshSlice(int axis, int offset, int dir)
		{
			var sliceDimensions = TransformSpace(axis, new Vector3Int(0, 1, 2));  // Used to find the slice-space X/Y dimensions are in local-space.

			var faceIndex = axis + dir * 3;

			for (int y = _lowerBounds[sliceDimensions.y]; y < _upperBounds[sliceDimensions.y]; ++y)
			//for (int y = _upperBounds[sliceDimensions.y] - 1; y >= _lowerBounds[sliceDimensions.y]; --y)
			for (int x = _lowerBounds[sliceDimensions.x]; x < _upperBounds[sliceDimensions.x]; ++x)
			{
				var sliceSpacePos = new Vector3Int(x, y, offset);
				var localPosition = TransformSpace(axis, sliceSpacePos);

				var voxel = Chunk.GetVoxel(localPosition);
				if (voxel != null && voxel.DataId != VoxelData.VoxelType.AIR)
				{
					//Debug.Log("Found a voxel to make faces for!");
					
					// A face should only be drawn if the voxel in front/behind (relative) this one is AIR.
					var voxelBehind = Chunk.GetVoxel(TransformSpace(axis, sliceSpacePos + new Vector3Int(0, 0, dir == 0 ? +1 : -1)));
					if (voxelBehind?.DataId == VoxelData.VoxelType.AIR)  // Should really be a test if voxelBehind is at least partially transparent.
					{
						//Debug.Log("voxel is visible!");

						var botNeighbor = Chunk.GetVoxel(TransformSpace(axis, sliceSpacePos - Vector3Int.up));
						var lftNeighbor = Chunk.GetVoxel(TransformSpace(axis, sliceSpacePos - Vector3Int.right));

						var botMesh = (botNeighbor != null && botNeighbor.FaceIndices[faceIndex] != -1)
							? _greedyMeshData[botNeighbor.FaceIndices[faceIndex]]
							: null;
						var lftMesh = (lftNeighbor != null && lftNeighbor.FaceIndices[faceIndex] != -1)
							? _greedyMeshData[lftNeighbor.FaceIndices[faceIndex]]
							: null;

						MeshFace usedMesh = null;
						if (botMesh != null && botMesh.Scale.x == 1)
						{
							// Growing a rect wider than 1 unit is handled by creating another rect and merging the two.
							//Debug.Log("The top (relative) voxel's rect is only 1 unit wide. Extending it downwards by 1!");

							++botMesh.Scale.y;
							usedMesh = botMesh;
						}

						if (usedMesh == null && lftMesh != null && lftMesh.Scale.y == 1)
						{
							// Because of the way we iterate, the rect grows to the right as much as possible
							// before exploring a way to grow downwards. Therefore, a rect that has grown
							// downwards cannot be grown to the right anymore.
							//Debug.Log("Left (relative) voxel has a rect we can use!");
							//Debug.Log("Extending the left (relative) voxel's rect to the right by 1!");

							++lftMesh.Scale.x;

							if (botMesh != null
							&& lftMesh.SliceSpacePosition.x == botMesh.SliceSpacePosition.x
							&& lftMesh.Scale.x == botMesh.Scale.x)
							{
								//Debug.Log("Extending the left voxel's rect caused it to be merged with the top voxel's rect!");
								++botMesh.Scale.y;

								//Debug.Log("Recycling the left voxel's rect!");
								RecycleMeshFace(lftMesh);
								for (int i = 1; i < lftMesh.Scale.x; ++i)
								{
									var voxelToFix = Chunk.GetVoxel(TransformSpace(axis, new Vector3Int(x - i, y, offset)));  // Can probably avoid a call to TransformSpace here.
									voxelToFix.FaceIndices[faceIndex] = botMesh.MeshIndex;  // voxelToFix can't be null.
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
							//Debug.Log("Created a rect for this voxel!");

							usedMesh = CreateMeshFace(sliceSpacePos, axis, dir);
						}

						voxel.AddFace(faceIndex, usedMesh.MeshIndex);
					}
				}
			}
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
				//var absDimensions = Rel2AbsVector(rect.SliceDimension, new Vector3Int(rect.Scale.x, rect.Scale.y, 1));
				var absPosition = TransformSpace(rect.SliceDimension, rect.SliceSpacePosition);// + new Vector3Int(0, Chunk.MinHeight, 0);

				var localSliceScales = TransformSpace(rect.SliceDimension, rect.Scale);

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
