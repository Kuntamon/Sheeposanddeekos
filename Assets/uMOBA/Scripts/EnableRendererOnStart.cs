// Useful for Fog of War plane, which we don't want to show when trying to
// modify the game in the Scene etc.
using UnityEngine;

public class EnableRendererOnStart : MonoBehaviour {
	void Start() {
		GetComponent<MeshRenderer>().enabled = true;
	}
}
