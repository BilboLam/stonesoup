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
	protected bool _stopFlashing;

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

		_stopFlashing = false;
		StartCoroutine(FlashRed());
		StartCoroutine(ExplodeAfterDelay(duration));
	}

	protected IEnumerator FlashRed() {
		float elapsed = 0f;
		while (!_stopFlashing && _buffedCreature != null && _buffedCreature.sprite != null) {
			_buffedCreature.sprite.color = Color.red;
			float t = Mathf.Clamp01(elapsed / (duration-0.5f));
			float interval = Mathf.Lerp(0.2f, 0.03f, t);
			yield return new WaitForSeconds(interval);
			elapsed += interval;
			if (_stopFlashing) break;
			_buffedCreature.sprite.color = Color.white;
			yield return new WaitForSeconds(interval);
			elapsed += interval;
		}
		if (_buffedCreature != null && _buffedCreature.sprite != null) {
			_buffedCreature.sprite.color = Color.white;
		}
	}

	protected IEnumerator ExplodeAfterDelay(float delay) {
		yield return new WaitForSeconds(delay);

		_stopFlashing = true;
		if (_buffedCreature != null) {
			if (_buffedCreature.sprite != null) _buffedCreature.sprite.color = Color.white;
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
