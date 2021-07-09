// Attach to the prefab for easier component access by the UI Scripts.
// Otherwise we would need slot.GetChild(0).GetComponentInChildren<Text> etc.
using UnityEngine;
using UnityEngine.UI;

public class UITeamSelectionSlot : MonoBehaviour {
    public Dropdown teamDropdown;
    public Text playerText;
    public Dropdown heroDropdown;
    public Button statusButton;
}
