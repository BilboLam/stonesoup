using System.Collections.Generic;
using UnityEngine;

public static class RoomFogSystem {

	sealed class RoomState {
		public Transform room;
		public readonly Dictionary<int, Vector2Int> sourceCells = new Dictionary<int, Vector2Int>();
		public readonly HashSet<Vector2Int> clearCells = new HashSet<Vector2Int>();
		public Transform root;
		public FogCell[,] cells;
	}

	sealed class FogCell {
		public readonly Transform tr;
		public readonly SpriteRenderer sr;

		public FogCell(Transform tr, SpriteRenderer sr) {
			this.tr = tr;
			this.sr = sr;
		}

		public void SetEnabled(bool enabled) {
			if (sr != null) sr.enabled = enabled;
		}
	}

	static readonly Dictionary<int, RoomState> _rooms = new Dictionary<int, RoomState>();
	static Sprite _fogSprite;
	static GameObject _playerMaskPrefab;
	static Transform _playerMask;
	static readonly Color _fogColor = new Color(0f, 0f, 0f, 0.95f);

	public static void SetSourceActive(Transform room, int sourceId, bool active, Vector2Int sourceCell) {
		if (room == null) return;
		RoomState state = GetOrCreateRoom(room);

		if (!active) {
			if (!state.sourceCells.Remove(sourceId)) return;
			RebuildClearCells(state);
			Apply(state);
			return;
		}

		state.sourceCells[sourceId] = sourceCell;
		RebuildClearCells(state);
		Apply(state);
	}

	static RoomState GetOrCreateRoom(Transform room) {
		int id = room.GetInstanceID();
		if (_rooms.TryGetValue(id, out RoomState state)) {
			if (state.room == null) state.room = room;
			return state;
		}
		state = new RoomState();
		state.room = room;
		_rooms[id] = state;
		return state;
	}

	static void RebuildClearCells(RoomState state) {
		state.clearCells.Clear();
		foreach (Vector2Int v in state.sourceCells.Values) state.clearCells.Add(v);
	}

	/// <summary> Apply the fog to the room </summary>
	static void Apply(RoomState state) {
		if (state.sourceCells.Count <= 0) {
			if (state.root != null) Object.Destroy(state.root.gameObject);
			state.root = null;
			state.cells = null;
			return;
		}

		if (state.root == null || state.cells == null) EnsureRoomRoot(state.room);
		if (state.cells == null) return;

		for (int x = 0; x < LevelGenerator.ROOM_WIDTH; x++) {
			for (int y = 0; y < LevelGenerator.ROOM_HEIGHT; y++) {
				bool enabled = !state.clearCells.Contains(new Vector2Int(x, y));
				state.cells[x, y].SetEnabled(enabled);
			}
		}
	}

	static Sprite CreateFogSprite() {
		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.filterMode = FilterMode.Point;
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.SetPixel(0, 0, Color.white);
		tex.Apply(false, true);
		return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
	}

	public static void SetPlayerMaskPrefab(GameObject prefab) {
		if (prefab == null || _playerMaskPrefab != null) return;
		_playerMaskPrefab = prefab;
	}

	/// <summary> Ensure the player has a fog mask created from the prefab. </summary>
	public static void EnsurePlayerMaskForPlayer() {
		if (_playerMask != null) return;
		if (_playerMaskPrefab == null) return;
		if (Player.instance == null) return;
		Transform tr = Player.instance.transform;
		GameObject maskObj = Object.Instantiate(_playerMaskPrefab);
		_playerMask = maskObj.transform;
		_playerMask.SetParent(tr, false);
	}

	public static void EnsureRoomRoot(Transform room) {
		if (room == null) return;
		RoomState state = GetOrCreateRoom(room);
		if (state.root != null && state.cells != null) return;

		if (_fogSprite == null) _fogSprite = CreateFogSprite();

		var rootObj = new GameObject("room_fog");
		rootObj.transform.SetParent(room, false);
		rootObj.transform.localPosition = Vector3.zero;
		state.root = rootObj.transform;
		state.cells = new FogCell[LevelGenerator.ROOM_WIDTH, LevelGenerator.ROOM_HEIGHT];

		int airLayerId = SortingLayer.NameToID("Air");
		for (int x = 0; x < LevelGenerator.ROOM_WIDTH; x++) {
			for (int y = 0; y < LevelGenerator.ROOM_HEIGHT; y++) {
				var cellObj = new GameObject($"fog_{x}_{y}");
				cellObj.transform.SetParent(state.root, false);
				cellObj.transform.localPosition = Tile.toWorldCoord(x, y);

				var sr = cellObj.AddComponent<SpriteRenderer>();
				sr.sprite = _fogSprite;
				sr.color = _fogColor;
				sr.drawMode = SpriteDrawMode.Sliced;
				sr.size = new Vector2(Tile.TILE_SIZE, Tile.TILE_SIZE);
				sr.sortingLayerID = airLayerId;
				sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
				state.cells[x, y] = new FogCell(cellObj.transform, sr);
			}
		}
	}
}

