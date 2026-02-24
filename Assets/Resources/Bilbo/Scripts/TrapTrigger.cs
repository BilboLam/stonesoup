using UnityEngine;

public class TrapTrigger : Tile {

	public enum TriggerDirection {
		Up,
		Right,
		Down,
		Left
	}

	public enum WallOrientation {
		Horizontal,
		Vertical
	}

	public GameObject wallPrefab;
	public Sprite triggeredSprite;
	public TriggerDirection triggerDirection = TriggerDirection.Up;
	public int directionOffset = 2;
	public WallOrientation orientation = WallOrientation.Horizontal;

	bool _triggered;

	void OnTriggerEnter2D(Collider2D other) {
		if (_triggered || wallPrefab == null) return;

		Tile triggeredBy = other.GetComponent<Tile>();
		if (triggeredBy == null || !triggeredBy.hasTag(TileTags.Creature)) return;

		_triggered = true;
		if (triggeredSprite != null) {
			SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			if (spriteRenderer != null) {
				spriteRenderer.sprite = triggeredSprite;
			}
		}
		SpawnWalls();
	}

	/// <summary>
	/// Spawn walls in certain places.
	/// That will kills any thing in the way.
	/// </summary>
	void SpawnWalls() {
		Transform room = transform.parent;
		if (room == null) return;

		Vector2 local = transform.localPosition;
		int tx = Mathf.FloorToInt(local.x / TILE_SIZE);
		int ty = Mathf.FloorToInt(local.y / TILE_SIZE);

		Vector2Int dir = TriggerDirectionToVector(triggerDirection);
		Vector2Int center = new Vector2Int(tx + dir.x * directionOffset, ty + dir.y * directionOffset);

		Vector2Int extendDir = orientation == WallOrientation.Horizontal ? Vector2Int.right : Vector2Int.up;
		for (int i = -1; i <= 1; i++) {
			int gx = center.x + extendDir.x * i;
			int gy = center.y + extendDir.y * i;
			if (gx < 0 || gx >= LevelGenerator.ROOM_WIDTH || gy < 0 || gy >= LevelGenerator.ROOM_HEIGHT) {
				continue;
			}

			Vector2 placeWorld = (Vector2)room.transform.position + toWorldCoord(gx, gy);
			if (!CanPlaceAndMaybeKill(placeWorld)) {
				continue;
			}
			spawnTile(wallPrefab, room, gx, gy);
		}
	}

	static Vector2Int TriggerDirectionToVector(TriggerDirection d) {
		switch (d) {
			case TriggerDirection.Left: return Vector2Int.left;
			case TriggerDirection.Right: return Vector2Int.right;
			case TriggerDirection.Down: return Vector2Int.down;
			default: return Vector2Int.up;
		}
	}

	bool CanPlaceAndMaybeKill(Vector2 worldPoint) {
		Collider2D[] hits = Physics2D.OverlapCircleAll(worldPoint, 0.1f);
		bool blocked = false;
		foreach (Collider2D c in hits) {
			if (c == null) continue;
			Tile t = c.GetComponent<Tile>();
			if (t == null) {
				blocked = true;
				break;
			}
			int beforeHealth = t.health;
			if (beforeHealth > 0) {
				t.takeDamage(this, 9999, DamageType.Explosive);
				if (t != null && t.health > 0) {
					blocked = true;
					break;
				}
			}
		}
		return !blocked;
	}
}
