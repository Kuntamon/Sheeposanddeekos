using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class UITeamSelection : MonoBehaviour {
    public NetworkManagerMOBA manager; // singleton is null until update
    public GameObject panel;
    public UITeamSelectionSlot slotPrefab;
    public Transform content;
    public Button quitButton;

    // available players (set after receiving the message from the server)
    [HideInInspector] public LobbyUpdateMsg lobbyMsg;

    void Update() {
        // only update if visible
        if (!panel.activeSelf) return;

        // hide if disconnected or if a local player is in the game world
        if (!NetworkClient.active || Player.localPlayer != null) Hide();

        // instantiate/destroy enough slots
        UIUtils.BalancePrefabs(slotPrefab.gameObject, lobbyMsg.players.Length, content);

        // refresh all
        var prefabs = manager.GetPlayerClasses();
        var teams = Enum.GetValues(typeof(Team)).Cast<Team>().ToList();
        for (int i = 0; i < lobbyMsg.players.Length; ++i) {
            LobbyPlayer player = lobbyMsg.players[i];
            //var prefab = prefabs.Find(p => p.name == charactersMsg.characters[i].className);
            UITeamSelectionSlot slot = content.GetChild(i).GetComponent<UITeamSelectionSlot>();

            // copy teams to team selection
            slot.teamDropdown.interactable = manager.loginName == player.name && !player.locked;
            slot.teamDropdown.onValueChanged.SetListener((val) => {}); // avoid callback while setting values
            slot.teamDropdown.options = teams.Select(
                team => new Dropdown.OptionData(team.ToString())
            ).ToList();
            slot.teamDropdown.value = teams.IndexOf(player.team);
            slot.teamDropdown.onValueChanged.SetListener(
                (value) => {
                    // send message to server
                    NetworkClient.Send(new ChangeTeamMsg{team=teams[value]});
                    Debug.LogWarning(value);
                }
            );

            // player name
            slot.playerText.text = player.name;

            // copy heroes to hero selection
            slot.heroDropdown.interactable = manager.loginName == player.name && !player.locked;
            slot.heroDropdown.onValueChanged.SetListener((val) => {}); // avoid callback while setting values
            slot.heroDropdown.options = prefabs.Select(
                p => new Dropdown.OptionData(p.name)
            ).ToList();
            slot.heroDropdown.value = player.heroIndex;
            slot.heroDropdown.onValueChanged.SetListener(
                (value) => {
                    // send message to server
                    NetworkClient.Send(new ChangeHeroMsg{heroIndex=value});
                    Debug.LogWarning(value);
                }
            );

            // status
            if (player.name == manager.loginName) {
                if (!player.locked) {
                    bool canLock = player.team != Team.Neutral;
                    slot.statusButton.interactable = canLock;
                    slot.statusButton.GetComponentInChildren<Text>().text = canLock ? "Lock!" : "Selecting";
                    slot.statusButton.onClick.SetListener(() => {
                        // send message to server
                        NetworkClient.Send(new LockMsg());
                    });
                } else {
                    slot.statusButton.interactable = false;
                    slot.statusButton.GetComponentInChildren<Text>().text = "Locked";
                }
            } else {
                slot.statusButton.interactable = false;
                slot.statusButton.GetComponentInChildren<Text>().text = player.locked ? "Locked" : "Selecting";
            }
        }

        quitButton.onClick.SetListener(() => { NetworkManagerMOBA.Quit(); });
    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
}
