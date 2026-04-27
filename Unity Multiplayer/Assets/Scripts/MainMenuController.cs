using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu UI")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject menuPanel;

    [Header("Map Selection UI")]
    [SerializeField] private GameObject mapSelectionPanel;
    [SerializeField] private Button map1Button;
    [SerializeField] private Button map2Button;
    [SerializeField] private Button backButton;

    private void Start()
    {
        // Основные кнопки
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(JoinGame);

        // Кнопки выбора карты
        map1Button.onClick.AddListener(() => StartHostWithMap("Map1"));
        map2Button.onClick.AddListener(() => StartHostWithMap("Map2"));
        backButton.onClick.AddListener(BackToMenu);

        if (string.IsNullOrEmpty(ipAddressInput.text))
            ipAddressInput.text = "localhost";

        mapSelectionPanel.SetActive(false);
    }

    // ========================
    // HOST FLOW
    // ========================
    private void OnHostClicked()
    {
        menuPanel.SetActive(false);
        mapSelectionPanel.SetActive(true);
    }

    private void StartHostWithMap(string sceneName)
    {
        DisableButtons();

        NetworkManager.singleton.onlineScene = sceneName;

        NetworkManager.singleton.StartHost();
    }

    private void BackToMenu()
    {
        mapSelectionPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    // ========================
    // CLIENT FLOW
    // ========================
    public void JoinGame()
    {
        string ipAddress = ipAddressInput.text;
        NetworkManager.singleton.networkAddress = ipAddress;

        DisableButtons();
        NetworkManager.singleton.StartClient();

        Invoke(nameof(EnableButtons), 5f);
    }

    // ========================
    // UI STATE
    // ========================
    private void DisableButtons()
    {
        hostButton.interactable = false;
        joinButton.interactable = false;
        ipAddressInput.interactable = false;

        map1Button.interactable = false;
        map2Button.interactable = false;
        backButton.interactable = false;
    }

    private void EnableButtons()
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            hostButton.interactable = true;
            joinButton.interactable = true;
            ipAddressInput.interactable = true;

            map1Button.interactable = true;
            map2Button.interactable = true;
            backButton.interactable = true;
        }
    }
}