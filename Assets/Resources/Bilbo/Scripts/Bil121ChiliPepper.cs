using System.Collections;
using UnityEngine;

public class bil121ChiliPepper : Tile {

	public float duration = 2f;
	public float explosionRadius = 3f;
	public int explosionDamage = 2;
	public GameObject explosionEffect;

	protected Tile _buffedCreature;
	protected float _origMoveSpeed;
	protected float _origMoveAcceleration;
	protected bool _applied;

	public override void useAsItem(Tile tileUsingUs) {
		if (_applied || tileUsingUs == null || !tileUsingUs.hasTag(TileTags.Creature)) return;
		_applied = true;
		
		Collider2D col = GetComponent<Collider2D>();
		if (col != null) col.enabled = false;
		if (sprite != null) sprite.enabled = false;
		_buffedCreature = tileUsingUs;
		transform.SetParent(_buffedCreature.transform);
		transform.localPosition = new Vector3(0, 0, -0.1f);

		tileUsingUs.tileWereHolding = null;
		_tileHoldingUs = null;

		// Apply Chili effect to the creature
		Player p = _buffedCreature.GetComponent<Player>();
		if (p != null) {
			_origMoveSpeed = p.moveSpeed;
			_origMoveAcceleration = p.moveAcceleration;
			p.moveSpeed *= 2f;
			p.moveAcceleration *= 2f;
		} else {
			BasicAICreature b = _buffedCreature.GetComponent<BasicAICreature>();
			if (b != null) {
				_origMoveSpeed = b.moveSpeed;
				_origMoveAcceleration = b.moveAcceleration;
				b.moveSpeed *= 2f;
				b.moveAcceleration *= 2f;
			}
		}

		StartCoroutine(ExplodeAfterDelay(duration));
	}

	protected IEnumerator ExplodeAfterDelay(float delay) {
		yield return new WaitForSeconds(delay);

		if (_buffedCreature != null) {
			Player p = _buffedCreature.GetComponent<Player>();
			if (p != null) {
				p.moveSpeed = _origMoveSpeed;
				p.moveAcceleration = _origMoveAcceleration;
			} else {
				BasicAICreature b = _buffedCreature.GetComponent<BasicAICreature>();
				if (b != null) {
					b.moveSpeed = _origMoveSpeed;
					b.moveAcceleration = _origMoveAcceleration;
				}
			}
		}

		Vector2 explosionCenter = transform.position;
		if (explosionEffect != null) {
			Instantiate(explosionEffect, explosionCenter, Quaternion.identity);
		}
		Collider2D[] colliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius);
		foreach (Collider2D c in colliders) {
			Tile tile = c.GetComponent<Tile>();
			if (tile == null || tile == this || tile == _buffedCreature) continue;
			tile.takeDamage(this, explosionDamage, DamageType.Explosive);
		}

		Destroy(gameObject);
	}
}
