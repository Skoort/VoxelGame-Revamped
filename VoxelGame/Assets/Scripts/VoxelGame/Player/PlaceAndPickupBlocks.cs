using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using VoxelGame.Terrain;

namespace VoxelGame
{
	public class PlaceAndPickupBlocks : MonoBehaviour
	{
		[SerializeField] private Transform _lookRoot = null;


		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Mouse0))
			{
				InteractWithBlock(pickupOrPlace: false);
			} else
			if (Input.GetKeyDown(KeyCode.Mouse1))
			{
				InteractWithBlock(pickupOrPlace: true);
			}
		}

		private void InteractWithBlock(bool pickupOrPlace)
		{
			// TODO: Check if you can actually place a block.

			var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

			var layer_mask = 1 << LayerMask.NameToLayer("Chunk");  // We only want to intersect the Chunk, otherwise we may intersect the Player.

			RaycastHit hitInfo;
			Physics.Raycast(ray, out hitInfo, 7, layer_mask);


			Debug.DrawLine(_lookRoot.position, _lookRoot.position + ray.direction.normalized * 7f, Color.blue, 1f);

			if (hitInfo.transform)
			{
				Debug.Log("HIT CHUNK!");

				// TODO: Add switching based on whether the voxel that was hit is a crafting table, chest or something similar.

				var posToAffect = Vector3Int.FloorToInt(hitInfo.point + (pickupOrPlace ? +1 : -1) * hitInfo.normal.normalized / 2f);
				var chunkToAffect = ChunkManager.Instance.GetChunk(posToAffect);  // Must get the chunk before converting to local position.

				posToAffect.x = SimpleMath.Mod(posToAffect.x, ChunkManager.Instance.ChunkSize.x);
				posToAffect.z = SimpleMath.Mod(posToAffect.z, ChunkManager.Instance.ChunkSize.y);


				//if (pickupOrPlace)
				//{
				//	chunkToAffect.CreateBlock(posToAffect, 0);  // TODO: Add support for item types (you get this info from the inventory you remove this item from).

				//	PushOutAllItemDropsInBlock(chunkToAffect, posToAffect);  // How to push items out of big areas like furniture? My suggestion is to take that furnitures bounds as the rectangular prism.
				//}
				//else
				//{
				//	chunkToAffect.DestroyBlock(posToAffect);  // TODO: Support getting destroyed voxel's information.
				//}

				if (_coroutine != null)
				{
					StopCoroutine(_coroutine);
				}
				_coroutine = StartCoroutine(DrawCube(chunkToAffect.Position + posToAffect + Vector3.one * 0.5F));
			}
		}

		private void PushOutAllItemDropsInBlock(Chunk chunk, Vector3Int localPositionToPlace)
		{
			var neighboringPositions = chunk.GetNeighboringPositions(localPositionToPlace);

			var chunkPos = chunk.gameObject.transform.position;
			var globalBlockOrigin = chunkPos + localPositionToPlace + new Vector3(0.5F, 0.5F, 0.5F);

			var colliders = Physics.OverlapBox(globalBlockOrigin, Vector3.one * 0.5F, Quaternion.identity, LayerMask.GetMask("Item Drop"));
			foreach (var collider in colliders)
			{
				var itemPos = collider.attachedRigidbody.position;

				var smallestDir = Vector3.zero;
				var smallestDeltaMag = float.MaxValue;
				foreach (var neighboringPos in neighboringPositions)
				{
					Vector3 neighborDir = neighboringPos - localPositionToPlace;
					var neighbor = chunk.GetVoxel(neighboringPos);

					if (neighbor != null)
					{ 
						continue;  // The neighboring block is occupied. We want to find an unoccupied one to push this item to. Skip.
					}

					// Calculates the distance of the item to the neighboring edge.
					// Here we rely on the alternate definition of the dot product
					// a dot b = a.x * b.x + a.y * b.y + a.z * b.z
					// to eliminate any the two axes not in the direction of the neighbor.
					var delta = (globalBlockOrigin + neighborDir * 0.5F) - itemPos;
					var deltaMag = Vector3.Dot(delta, neighborDir);
					if (smallestDeltaMag > deltaMag)
					{
						smallestDir = neighborDir;
						smallestDeltaMag = deltaMag;
					}
				}

				// Add a small constant to the smallestDeltaMag to represent the size of the ItemDrop.
				collider.transform.position += smallestDir * (smallestDeltaMag + 0.2F);
			}
		}

		private IEnumerator DrawCube(Vector3 pos)
		{
			_cubeCenter = pos;
			yield return new WaitForSeconds(1F);
			_cubeCenter = null;
		}

		private Vector3? _cubeCenter;
		private Coroutine _coroutine;
		private void OnDrawGizmos()
		{
			if (_cubeCenter != null)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawCube(_cubeCenter.Value, Vector3.one);
			}
		}
	}
}
	