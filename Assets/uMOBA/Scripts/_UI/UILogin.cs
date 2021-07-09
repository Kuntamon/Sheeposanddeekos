// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class UILogin : MonoBehaviour {
    public NetworkManagerMOBA manager; // singleton=null in Start/Awake
    public GameObject panel;
    public Text statusText;
    public InputField nameInput;
    public InputField serverInput;
    public Button loginButton;
    public Button hostButton;
    public Button dedicatedButton;
    public Button cancelButton;
    public Button quitButton;

    void Update() {
        // only update while visible
        if (!panel.activeSelf) return;

        // status
        statusText.text = manager.IsConnecting() ? "Connecting..." : "";

        // buttons. interactable while network is not active
        // (using IsConnecting is slightly delayed and would allow multiple clicks)
        loginButton.interactable = !manager.isNetworkActive;
        loginButton.onClick.SetListener(() => { manager.StartClient(); });
        hostButton.interactable = !manager.isNetworkActive;
        hostButton.onClick.SetListener(() => { manager.StartHost(); });
        cancelButton.gameObject.SetActive(manager.isNetworkActive);
        cancelButton.onClick.SetListener(() => { manager.StopClient(); });
        dedicatedButton.interactable = !manager.isNetworkActive;
        dedicatedButton.onClick.SetListener(() => { manager.StartServer(); });
        quitButton.onClick.SetListener(() => { NetworkManagerMOBA.Quit(); });

        // inputs
        manager.loginName = nameInput.text;
        manager.networkAddress = serverInput.text;
    }

    public void Show() { panel.SetActive(true); }
    public void Hide() { panel.SetActive(false); }
}
