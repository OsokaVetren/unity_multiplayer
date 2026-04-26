using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Главное меню: Host / Join + выбор игровой сцены.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Location Selection UI")]
    [SerializeField] private GameObject locationSelectionPanel;
    [SerializeField] private Button location1Button;
    [SerializeField] private Button location2Button;
    [SerializeField] private Button location3Button;
    [SerializeField] private Button backToMainMenuButton;

    [Header("Scene Names")]
    [SerializeField] private string scene1 = "Demo_1";
    [SerializeField] private string scene2 = "Demo_02";
    [SerializeField] private string scene3 = "Demo_1";

    private void Start()
    {
        if (hostButton != null) hostButton.onClick.AddListener(OnHostButtonClicked);
        if (joinButton != null) joinButton.onClick.AddListener(JoinGame);

        if (location1Button != null) location1Button.onClick.AddListener(() => StartHostAndLoadScene(scene1));
        if (location2Button != null) location2Button.onClick.AddListener(() => StartHostAndLoadScene(scene2));
        if (location3Button != null) location3Button.onClick.AddListener(() => StartHostAndLoadScene(scene3));
        if (backToMainMenuButton != null) backToMainMenuButton.onClick.AddListener(OnBackToMainMenuFromLocationSelection);

        if (ipAddressInput != null && string.IsNullOrEmpty(ipAddressInput.text))
            ipAddressInput.text = "localhost";

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (locationSelectionPanel != null) locationSelectionPanel.SetActive(false);
    }

    public void OnHostButtonClicked()
    {
        DisableMainMenuButtons();

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (locationSelectionPanel != null) locationSelectionPanel.SetActive(true);
    }

    public void StartHostAndLoadScene(string sceneName)
    {
        if (NetworkManager.singleton == null)
        {
            Debug.LogError("[MainMenu] NetworkManager.singleton is null");
            OnBackToMainMenuFromLocationSelection();
            return;
        }

        NetworkManager.singleton.StartHost();

        if (NetworkServer.active)
        {
            NetworkManager.singleton.ServerChangeScene(sceneName);
            if (locationSelectionPanel != null) locationSelectionPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("[MainMenu] Failed to start host.");
            OnBackToMainMenuFromLocationSelection();
        }
    }

    public void JoinGame()
    {
        if (NetworkManager.singleton == null) return;

        if (ipAddressInput != null)
            NetworkManager.singleton.networkAddress = ipAddressInput.text;

        DisableMainMenuButtons();
        NetworkManager.singleton.StartClient();

        // Если за 5 сек не подключились — вернём кнопки
        Invoke(nameof(EnableMainMenuButtons), 5f);
    }

    public void OnBackToMainMenuFromLocationSelection()
    {
        if (locationSelectionPanel != null) locationSelectionPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        EnableMainMenuButtons();
    }

    private void DisableMainMenuButtons()
    {
        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;
        if (ipAddressInput != null) ipAddressInput.interactable = false;
    }

    private void EnableMainMenuButtons()
    {
        if (NetworkClient.isConnected || NetworkServer.active) return;

        if (hostButton != null) hostButton.interactable = true;
        if (joinButton != null) joinButton.interactable = true;
        if (ipAddressInput != null) ipAddressInput.interactable = true;
    }
}
