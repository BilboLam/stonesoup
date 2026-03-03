using System.Collections.Generic;
using UnityEngine;

// Flying sword: wanders as creature, detects nearby creatures, steps back → charges → dashes (one damage per enemy), then pickable; timeout returns to wandering.
public class FlyingSword : BasicAICreature {

	public enum State {
		Wandering,
		StepBack,
		Charge,
		Dash,
		Pickable,
		CarrierDash
	}

	[Header("Detection")]
	public float attackTriggerDistance = 4f;

	[Header("StepBack")]
	public float stepBackDuration = 0.35f;
	public float stepBackSpeed = 6f;

	[Header("Charge")]
	public float chargeDuration = 0.5f;

	[Header("Dash")]
	public float dashSpeed = 20f;
	public float dashDuration = 0.4f;

	[Header("Damage")]
	public int damageAmount = 1;
	public float damageForce = 1000f;

	[Header("Anchor")]
	public Vector2 anchorLocalOffset = new Vector2(0f, -0.6f);
	public float anchorAngleDeg = 0f;

	[Header("Use as item")]
	public float carrierDashSpeed = 22f;
	public float carrierDashDuration = 0.35f;
	public float carrierDashDamageRadius = 1.25f;
	public int carrierDashDamage = 2;
	public float carrierDashForce = 1200f;
	public LayerMask carrierDashLayerMask = 0x1 + 0x200;

	[Header("Pickable")]
	public float pickableTime = 5f;

	[Header("Wandering")]
	public float timeBetweenMovesMin = 1.5f;
	public float timeBetweenMovesMax = 3f;

	[Header("Visual")]
	public float swordAngleOffsetDeg = 0f;

	protected State _state = State.Wandering;
	protected Tile _targetCreature;
	protected HashSet<Tile> _hitThisDash = new HashSet<Tile>();
	protected float _nextMoveCounter;
	protected float _chargeTimer;
	protected float _dashTimer;
	protected Vector2 _dashDirection;
	protected float _pickableTimer;
	protected float _stepBackTimer;
	protected Vector2 _stepBackDirection;

	protected Tile _heldCarrier;
	protected float _heldOrigMoveSpeed;
	protected float _heldOrigMoveAcceleration;
	protected bool _heldMoveApplied;

	protected bool _carrierDashActive;
	protected float _carrierDashTimer;
	protected Vector2 _carrierDashDir;
	protected Tile _formerCarrier;
	protected HashSet<Tile> _hitThisCarrierDash = new HashSet<Tile>();

	public override void Start() {
		base.Start();
		SetStateColor(_state);
	}

	void Update() {
		if (_tileHoldingUs != null) {
			UpdateHeld();
			updateSpriteSorting();
			return;
		}

		// timers
		switch (_state) {
			case State.Wandering:
				if (_nextMoveCounter > 0) _nextMoveCounter -= Time.deltaTime;
				if (_nextMoveCounter <= 0) takeStep();
				break;
			case State.StepBack:
				_stepBackTimer -= Time.deltaTime;
				if (_targetCreature != null) FacePosition(_targetCreature.transform.position);
				if (_stepBackTimer <= 0) EnterCharge();
				break;
			case State.Charge:
				_chargeTimer -= Time.deltaTime;
				if (_targetCreature != null) FacePosition(_targetCreature.transform.position);
				if (_chargeTimer <= 0) EnterDash();
				break;
			case State.Dash:
				_dashTimer -= Time.deltaTime;
				if (_dashTimer <= 0) EnterPickable();
				break;
			case State.Pickable:
				_pickableTimer -= Time.deltaTime;
				if (_pickableTimer <= 0) EnterWandering();
				break;
			case State.CarrierDash:
				_carrierDashTimer -= Time.fixedDeltaTime;
				if (_carrierDashTimer <= 0f) {
					_carrierDashActive = false;
					_hitThisCarrierDash.Clear();
					EnterWandering();
					return;
				}
				break;
		}
		updateSpriteSorting();
		ClampInRoom();
	}

	public override void FixedUpdate() {
		if (_tileHoldingUs != null) {
			FixedUpdateHeld();
			return;
		}

		switch (_state) {
			case State.Wandering:
				MoveToTargetGrid();
				break;
			case State.StepBack:
				if (_targetCreature == null) {
					EnterWandering();
					break;
				}
				moveViaVelocity(_stepBackDirection, stepBackSpeed, moveAcceleration);
				break;
			case State.Charge:
				if (_body != null) _body.linearVelocity = Vector2.zero;
				break;
			case State.Dash:
				if (_body != null) _body.linearVelocity = _dashDirection * dashSpeed;
				break;
			case State.Pickable:
				if (_body != null) _body.linearVelocity = Vector2.zero;
				break;
			case State.CarrierDash:
				if (_body != null) _body.linearVelocity = _carrierDashDir * carrierDashSpeed;
				break;
		}
	}

	void MoveToTargetGrid() {
		Vector2 targetGlobalPos = toWorldCoord(_targetGridPos.x, _targetGridPos.y);
		if (Vector2.Distance(transform.position, targetGlobalPos) >= GRID_SNAP_THRESHOLD) 
		{
			Vector2 dir = (targetGlobalPos - (Vector2)transform.position).normalized;
			FacePosition(targetGlobalPos);
			moveViaVelocity(dir, moveSpeed, moveAcceleration);
		} 
		else 
		{
			moveViaVelocity(Vector2.zero, 0, moveAcceleration);
		}
	}
	
	public override void tileDetected(Tile otherTile) {
		if (otherTile == this) return;
		if (!otherTile.hasTag(TileTags.Creature)) return;
		if (_state != State.Wandering) return;
		float d = Vector2.Distance(transform.position, otherTile.transform.position);
		if (d > attackTriggerDistance) return;
		if (_targetCreature == null || d < Vector2.Distance(transform.position, _targetCreature.transform.position)) {
			_targetCreature = otherTile;
			EnterStepBack();
		}
	}

	#region State Transitions
	void EnterStepBack() {
		_state = State.StepBack;
		SetStateColor(_state);
		if (_targetCreature == null) {
			EnterWandering();
			return;
		}
		_stepBackDirection = ((Vector2)transform.position - (Vector2)_targetCreature.transform.position).normalized;
		_stepBackTimer = stepBackDuration;
	}

	void EnterCharge() {
		_state = State.Charge;
		SetStateColor(_state);
		_chargeTimer = chargeDuration;
		if (_body != null) _body.linearVelocity = Vector2.zero;
	}

	void EnterDash() {
		_state = State.Dash;
		if (mainCollider != null) mainCollider.isTrigger = true;
		SetStateColor(_state);
		_dashDirection = (Vector2)transform.up;
		_hitThisDash.Clear();
		_dashTimer = dashDuration;
		if (_body != null) {
			_body.linearVelocity = _dashDirection * dashSpeed;
		}
	}

	void EnterPickable() {
		_state = State.Pickable;
		SetStateColor(_state);
		if (_body != null) {
			_body.linearVelocity = Vector2.zero;
			_body.bodyType = RigidbodyType2D.Kinematic;
		}
		removeTag(TileTags.Creature);
		addTag(TileTags.CanBeHeld);
		addTag(TileTags.Weapon);
		_pickableTimer = pickableTime;
		_targetCreature = null;
	}

	void EnterWandering() {
		_state = State.Wandering;
		SetStateColor(_state);
		_targetCreature = null;
		if (_body != null) _body.bodyType = RigidbodyType2D.Dynamic;
		if (mainCollider != null) mainCollider.isTrigger = false;
		removeTag(TileTags.CanBeHeld);
		removeTag(TileTags.Weapon);
		addTag(TileTags.Creature);
		_targetGridPos = toGridCoord(globalX, globalY);
		_nextMoveCounter = Random.Range(timeBetweenMovesMin, timeBetweenMovesMax);
	}
	void EnterCarrierDash() {
		_formerCarrier = _tileHoldingUs != null ? _tileHoldingUs : _formerCarrier;
		_state = State.CarrierDash;
	}
	#endregion
	void FacePosition(Vector3 worldPos) {
		Vector2 dir = (worldPos - transform.position).normalized;
		float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + swordAngleOffsetDeg;
		transform.rotation = Quaternion.Euler(0, 0, angle);
	}

	void FaceDirection(Vector2 dir) {
		if (dir.sqrMagnitude < 0.0001f) return;
		float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + swordAngleOffsetDeg;
		transform.rotation = Quaternion.Euler(0, 0, angle);
	}

	protected override void takeStep() {
		_neighborPositions.Clear();
		Vector2 up = new Vector2(_targetGridPos.x, _targetGridPos.y + 1);
		if (pathIsClear(toWorldCoord(up))) _neighborPositions.Add(up);
		Vector2 right = new Vector2(_targetGridPos.x + 1, _targetGridPos.y);
		if (pathIsClear(toWorldCoord(right))) _neighborPositions.Add(right);
		Vector2 down = new Vector2(_targetGridPos.x, _targetGridPos.y - 1);
		if (pathIsClear(toWorldCoord(down))) _neighborPositions.Add(down);
		Vector2 left = new Vector2(_targetGridPos.x - 1, _targetGridPos.y);
		if (pathIsClear(toWorldCoord(left))) _neighborPositions.Add(left);
		if (_neighborPositions.Count > 0) {
			_targetGridPos = GlobalFuncs.randElem(_neighborPositions);
			_nextMoveCounter = Random.Range(timeBetweenMovesMin, timeBetweenMovesMax);
		}
	}
	void SetStateColor(State s) 
	{
		if (_sprite == null) return;
		if (s == State.Charge || s == State.Dash) _sprite.color = Color.red;
		else if (s == State.Wandering || s == State.StepBack) _sprite.color = Color.orange;
		else _sprite.color = Color.white;
	}

	void UpdateHeld() {
		if (_tileHoldingUs == null) return;

		FaceDirection(_tileHoldingUs.aimDirection);

		if (_carrierDashActive) {
			_carrierDashTimer -= Time.deltaTime;
			if (_carrierDashTimer <= 0f) {
				_carrierDashActive = false;
				_hitThisCarrierDash.Clear();
				if (_tileHoldingUs.body != null) _tileHoldingUs.body.linearVelocity = Vector2.zero;
			}
		}
	}

	void FixedUpdateHeld() {
		if (_tileHoldingUs == null) return;

		if (_carrierDashActive) {
			if (_tileHoldingUs.body != null) _tileHoldingUs.body.linearVelocity = _carrierDashDir * carrierDashSpeed;
			ContactFilter2D filter = new ContactFilter2D();
            filter.useLayerMask = true;
            filter.layerMask = carrierDashLayerMask;
            filter.useTriggers = true;
			
            int n = Physics2D.OverlapCircle(_tileHoldingUs.transform.position, carrierDashDamageRadius, filter, _maybeColliderResults);
            for (int i = 0; i < n && i < _maybeColliderResults.Length; i++) {
                Collider2D c = _maybeColliderResults[i];
                if (c == null) continue;
                Tile t = c.GetComponent<Tile>();
                if (t == null || t == this || t == _tileHoldingUs) continue;

                // Hit wall will drop the sword
                if (t.hasTag(TileTags.Wall)) {
                    EnterCarrierDash();
                    if (_tileHoldingUs != null) dropped(_tileHoldingUs);
                }
				CarrierDashDealDamage(t);
            }
		}
	}

	void ClampInRoom() {
		if (GameManager.instance == null) return;
		if (GameManager.gameMode == GameManager.GameMode.SingleRoom) {

			float maxX = (LevelGenerator.ROOM_WIDTH+0.5f) * Tile.TILE_SIZE;
			float maxY = (LevelGenerator.ROOM_HEIGHT+0.5f) * Tile.TILE_SIZE;
			localX = Mathf.Clamp(localX, -0.5f, maxX);
			localY = Mathf.Clamp(localY, -0.5f, maxY);
			return;
		}
	}

	void OnTriggerEnter2D(Collider2D other) {
		Tile t = other.GetComponent<Tile>();
		if (t == null || t == this) return;
		if ((carrierDashLayerMask.value & (1 << other.gameObject.layer)) == 0) return;

		if (_state == State.Dash) {
			if (t == null || _hitThisDash.Contains(t)) return;
			_hitThisDash.Add(t);
			t.takeDamage(this, damageAmount, DamageType.Explosive);
			Vector2 toOther = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
			t.addForce(toOther * damageForce);
		}
		// Sword flying by itself in CarrierDash state.
		if (_state == State.CarrierDash) {
			if (t == _formerCarrier) return;
			CarrierDashDealDamage(t);
		}
	}

	void CarrierDashDealDamage(Tile t)
	{
		if (_hitThisCarrierDash.Contains(t)) return;
		_hitThisCarrierDash.Add(t);
		t.takeDamage(this, carrierDashDamage, DamageType.Explosive);
		Vector2 dirFromSword = ((Vector2)t.transform.position - (Vector2)transform.position).normalized;
		t.addForce(dirFromSword * carrierDashForce);

	}
	public override void pickUp(Tile tilePickingUsUp) {
		base.pickUp(tilePickingUsUp);
		_targetCreature = null;

		transform.localPosition = new Vector3(anchorLocalOffset.x, anchorLocalOffset.y, -0.1f);
		transform.localRotation = Quaternion.Euler(0, 0, anchorAngleDeg);
		if (mainCollider != null) mainCollider.isTrigger = true;
		SetStateColor(State.Pickable);
	}

	public override void dropped(Tile tileDroppingUs) {
		base.dropped(tileDroppingUs);
		if (!_carrierDashActive) EnterPickable();
		if (tileDroppingUs != null && tileDroppingUs.body != null) tileDroppingUs.body.linearVelocity = Vector2.zero;
	}

	public override void useAsItem(Tile tileUsingUs) {
		if (tileUsingUs == null || _tileHoldingUs != tileUsingUs) return;

		Vector2 dir = tileUsingUs.aimDirection;
		dir.Normalize();

		_carrierDashActive = true;
		_carrierDashTimer = carrierDashDuration;
		_carrierDashDir = dir;
		_hitThisCarrierDash.Clear();
	}

}
