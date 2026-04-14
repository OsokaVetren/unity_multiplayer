using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject menuPanel; // Панель со всеми кнопками

    private void Start()
    {
        // Вместо ручного перетаскивания в инспекторе OnClick, 
        // подписываемся прямо в коде. Так надежнее.
        hostButton.onClick.AddListener(HostGame);
        joinButton.onClick.AddListener(JoinGame);

        // Устанавливаем IP по умолчанию, если поле пустое
        if (string.IsNullOrEmpty(ipAddressInput.text))
        {
            ipAddressInput.text = "localhost";
        }
    }

    public void HostGame()
    {
        DisableButtons();
        NetworkManager.singleton.StartHost();
    }

    public void JoinGame()
    {
        string ipAddress = ipAddressInput.text;
        NetworkManager.singleton.networkAddress = ipAddress;

        DisableButtons();
        NetworkManager.singleton.StartClient();
        
        // Добавим проверку: если через 5 секунд не подключились, вернем кнопки
        Invoke(nameof(EnableButtons), 5f);
    }

    private void DisableButtons()
    {
        hostButton.interactable = false;
        joinButton.interactable = false;
        ipAddressInput.interactable = false;
    }

    private void EnableButtons()
    {
        // Если мы всё еще в меню (не загрузилась игровая сцена), включаем кнопки обратно
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            hostButton.interactable = true;
            joinButton.interactable = true;
            ipAddressInput.interactable = true;
        }
    }
}