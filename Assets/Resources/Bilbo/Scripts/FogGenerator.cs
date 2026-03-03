using UnityEngine;

public class FogGenerator : Tile {

	public GameObject fogMaskPrefab;

	public override void init() {
		base.init();
		RoomFogSystem.SetPlayerMaskPrefab(fogMaskPrefab);
		RoomFogSystem.SetSourceActive(transform.parent, GetInstanceID(), true, GetGridPos());
	}

	void Update() {
		RoomFogSystem.EnsurePlayerMaskForPlayer();
	}

	void OnDestroy() {
		RoomFogSystem.SetSourceActive(transform.parent, GetInstanceID(), false, GetGridPos());
	}

	Vector2Int GetGridPos() {
		Vector2 local = transform.localPosition;
		int gx = Mathf.FloorToInt(local.x / TILE_SIZE);
		int gy = Mathf.FloorToInt(local.y / TILE_SIZE);
		gx = Mathf.Clamp(gx, 0, LevelGenerator.ROOM_WIDTH - 1);
		gy = Mathf.Clamp(gy, 0, LevelGenerator.ROOM_HEIGHT - 1);
		return new Vector2Int(gx, gy);
	}
}

