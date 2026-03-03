using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dice that behaves like a rock but deals random damage and special effects.
public class DiceRock : Tile {

	public AudioClip throwSound;
	public float throwForce = 3000f;

	// How slow we need to be going before we consider ourself "on the ground" again
	public float onGroundThreshold = 0.8f;
	public float damageThreshold = 14;
	public float damageForce = 1000;

	[Header("Dice Behavior")]
	public Pool enemyPool;
	public Pool rewardPool;
	public GameObject damageTextPrefab;

	protected Tile _tileThatThrewUs = null;

	// Keep track of whether we're in the air and whether we were JUST throw
	protected bool _isInAir = false;
	protected float _afterThrowCounter;
	public float afterThrowTime = 0.2f;
	
	protected virtual void Update() {
		UpdateAirborne();
		UpdateHeldSprite();
		updateSpriteSorting();
	}
	public override void takeDamage(Tile tileDamagingUs, int amount, DamageType damageType) {
		if (damageType == DamageType.Explosive) {
			base.takeDamage(tileDamagingUs, amount, damageType);
		}
	}

	public override void useAsItem(Tile tileUsingUs) {
		if (_tileHoldingUs != tileUsingUs) return;
		if (onTransitionArea()) return;
		AudioManager.playAudio(throwSound);
		PrepareThrow(tileUsingUs);
		SetupThrowPhysics(tileUsingUs);
		ApplyThrowForce();
	}

	// Set up references before the throw.
	void PrepareThrow(Tile tileUsingUs) {
		_sprite.transform.localPosition = Vector3.zero;
		_tileThatThrewUs = tileUsingUs;
		_isInAir = true;
		Collider2D otherCol = _tileThatThrewUs.GetComponent<Collider2D>();
		if (otherCol != null) {
			Physics2D.IgnoreCollision(otherCol, _collider, true);
		}
	}

	// Set up physics and parenting before we start the throw.
	void SetupThrowPhysics(Tile tileUsingUs) {
		_body.bodyType = RigidbodyType2D.Dynamic;
		transform.parent = tileUsingUs.transform.parent;
		_tileHoldingUs.tileWereHolding = null;
		_tileHoldingUs = null;
		_collider.isTrigger = false;
		_body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
	}

	// Applies throw force.
	void ApplyThrowForce() {
		Vector2 throwDir = _tileThatThrewUs.aimDirection.normalized;
		_body.AddForce(throwDir*throwForce);
		_afterThrowCounter = afterThrowTime;
	}



	// Try to land if we've been in the air for a while.
	void UpdateAirborne() {
		if (!_isInAir) return;
		if (_afterThrowCounter > 0) {
			_afterThrowCounter -= Time.deltaTime;
			return;
		}
		TryLand();
	}

	// Try to land and back to can be held.
	void TryLand() {
		if (_body == null) return;
		if (_body.linearVelocity.magnitude > onGroundThreshold) return;
		_body.linearVelocity = Vector2.zero;
		RestoreCollisionWithThrower();
		_body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
		_collider.isTrigger = true;
		addTag(TileTags.CanBeHeld);
		_isInAir = false;
	}

	// Restores collision with the tile that threw us.
	void RestoreCollisionWithThrower() {
		if (_tileThatThrewUs == null) return;
		Collider2D otherCol = _tileThatThrewUs.GetComponent<Collider2D>();
		if (otherCol == null) return;
		Physics2D.IgnoreCollision(otherCol, _collider, false);
	}

	// Updates sprite while being held.
	void UpdateHeldSprite() {
		if (_tileHoldingUs != null) {
			_sprite.transform.localPosition = new Vector3(-0.5f, 0, 0);
			float aimAngle = Mathf.Atan2(_tileHoldingUs.aimDirection.y, _tileHoldingUs.aimDirection.x)*Mathf.Rad2Deg;
			transform.localRotation = Quaternion.Euler(0, 0, aimAngle);
			return;
		}
		_sprite.transform.localPosition = Vector3.zero;
	}

	// Handles collision to apply damage and special roll.
	void OnCollisionEnter2D(Collision2D collision) {
		if (!_isInAir) return;
		Tile otherTile = collision.gameObject.GetComponent<Tile>();
		if (otherTile == null || !otherTile.hasTag(TileTags.Creature)) return;
		float impact = collisionImpactLevel(collision);
		if (impact <= damageThreshold) return;
		int roll = Random.Range(1, 7);
		otherTile.takeDamage(this, roll);
		if (_body != null) otherTile.addForce(_body.linearVelocity.normalized*damageForce);
		ShowDamageText(otherTile, roll);
		HandleSpecialRoll(roll);
	}

	// Spawns floating text for the damage.
	void ShowDamageText(Tile target, int roll) {
		if (damageTextPrefab == null || target == null) return;
		Vector3 spawnPos = target.transform.position + Vector3.up;
		GameObject obj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
		DamageText damageText = obj.GetComponent<DamageText>();
		string text = roll == 6 ? "666" : (roll == 1 ? "JACKPOT!" : roll.ToString());
		if (damageText != null) damageText.Init(text);
	}

	// Special roll when rolling minimum or maximum damage.
	void HandleSpecialRoll(int roll) {
		if (roll == 6) {
			SpawnFromPool(enemyPool.pool, 2);
			die();
		}
		else if (roll == 1) {
			SpawnFromPool(rewardPool.pool, 2);
			die();
		}
	}

	// Spawns tiles around the dice.
	void SpawnFromPool(GameObject[] pool, int count) {
		if (pool == null || pool.Length == 0) return;
		Transform parent = transform.parent != null ? transform.parent : transform;
		Vector2 baseGrid = Tile.toGridCoord(transform.localPosition.x, transform.localPosition.y);
		for (int i = 0; i < count; i++) {
			GameObject prefab = GlobalFuncs.randElem(pool);
			Vector2Int cell;
			TryFindFreeCell(baseGrid, out cell);
			Tile.spawnTile(prefab, parent, cell.x, cell.y);
		}
	}

	// Tries to find a nearby empty grid cell for spawning.
	void TryFindFreeCell(Vector2 baseGrid, out Vector2Int cell) {
		cell = new Vector2Int((int)baseGrid.x, (int)baseGrid.y);
		for (int attempt = 0; attempt < 8; attempt++) {
			int dx = Random.Range(-1, 2);
			int dy = Random.Range(-1, 2);
			int gridX = Mathf.Clamp((int)baseGrid.x + dx, 0, LevelGenerator.ROOM_WIDTH - 1);
			int gridY = Mathf.Clamp((int)baseGrid.y + dy, 0, LevelGenerator.ROOM_HEIGHT - 1);
			if (!CellOccupied(gridX, gridY)) {
				cell = new Vector2Int(gridX, gridY);
				return;
			}
		}
	}

	// Checks if a grid cell already has a tile.
	bool CellOccupied(int gridX, int gridY) {
		Vector2 roomPos = Tile.toWorldCoord(gridX, gridY);
		ContactFilter2D filter = new ContactFilter2D();
		filter.useLayerMask = false;
		filter.useTriggers = true;
		Vector2 worldPos = transform.parent.TransformPoint(roomPos);
		int num = Physics2D.OverlapPoint(worldPos, filter, _maybeColliderResults);
		for (int i = 0; i < num && i < _maybeColliderResults.Length; i++) {
			Tile tile = _maybeColliderResults[i].GetComponent<Tile>();
			if (tile != null) return true;
		}
		return false;
	}
}

