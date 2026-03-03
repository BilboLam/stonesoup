using UnityEngine;
using TMPro;

// Simple floating damage text that lives for a short time.
public class DamageText : MonoBehaviour {

	public TMP_Text textMesh;
	public float lifetime = 1f;
	public float floatSpeed = 1f;

	public void Init(string text) {
		if (textMesh != null) textMesh.text = text;
	}

	void Update() {
		if (lifetime > 0f) lifetime -= Time.deltaTime;
		transform.position += Vector3.up * floatSpeed * Time.deltaTime;
		if (lifetime <= 0f) Destroy(gameObject);
	}
}

