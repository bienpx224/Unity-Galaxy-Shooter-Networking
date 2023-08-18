using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using Tashi.NetworkTransport;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class MenuManager : Singleton<MenuManager>
{
    [SerializeField] private Animator m_menuAnimator;

    [SerializeField] private CharacterDataSO[] m_characterDatas;

    [SerializeField] private AudioClip m_confirmClip;

    private bool m_pressAnyKeyActive = true;
    private const string k_enterMenuTriggerAnim = "enter_menu";

    [SerializeField] private SceneName nextScene = SceneName.CharacterSelection;

    [Header("Sign in")] [SerializeField] private GameObject _signInGroup;
    [SerializeField] private Button _signInButton;
    [SerializeField] private TMP_InputField _nameInputText;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("Create Room/Lobby")] [SerializeField]
    private TMP_InputField _maxPlayerInRoomInputField;

    public Lobby lobby;
    public bool isHost = false;
    public float nextHeartbeat;
    public float nextLobbyRefresh;
    private TashiNetworkTransport NetworkTransport => NetworkManager.Singleton.NetworkConfig.NetworkTransport as TashiNetworkTransport;

    private void Awake()
    {
        UnityServicesInit();
    }

    private async void UnityServicesInit()
    {
        await UnityServices.InitializeAsync();
    }

    private IEnumerator Start()
    {
        // -- To test with latency on development builds --
        // To set the latency, jitter and packet-loss percentage values for develop builds we need
        // the following code to execute before NetworkManager attempts to connect (changing the
        // values of the parameters as desired).
        //
        // If you'd like to test without the simulated latency, just set all parameters below to zero(0).
        //
        // More information here:
        // https://docs-multiplayer.unity3d.com/netcode/current/tutorials/testing/testing_with_artificial_conditions#debug-builds
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().
            SetDebugSimulatorParameters(
                packetDelay: 50,
                packetJitter: 5,
                dropRate: 3);
#endif

        ClearAllCharacterData();

        // Wait for the network Scene Manager to start
        yield return new WaitUntil(() => NetworkManager.Singleton.SceneManager != null);

        // Set the events on the loading manager
        // Doing this because every time the network session ends the loading manager stops
        // detecting the events
        LoadingSceneManager.Instance.Init();

        if (AuthenticationService.Instance.IsSignedIn)
        {
            _signInGroup.SetActive(false);
            TriggerMainMenuTransitionAnimation();
            m_pressAnyKeyActive = false;
        }
        else
        {
            _signInGroup.SetActive(true);
        }

        UpdateProfileNameText();
    }

    public void UpdateProfileNameText()
    {
        _nameText.text = AuthenticationService.Instance.IsSignedIn
            ? AuthenticationService.Instance.Profile
            : "-Not Sign In-";
    }

    public async void SignInButtonClicked()
    {
        if (string.IsNullOrEmpty(_nameInputText.text))
        {
            Debug.Log($"Signing in with the default profile");
            // await UnityServices.InitializeAsync();
        }
        else
        {
            Debug.Log($"Signing in with profile '{_nameInputText.text}'");
            /* Init Unity Services. But now no need cause inited in Awake() */
            // var options = new InitializationOptions();
            // options.SetProfile(_nameTextField.text);
            // await UnityServices.InitializeAsync(options);

            /* Switch to new Profile name. Profile init in awake() is default */
            AuthenticationService.Instance.SwitchProfile(_nameInputText.text);
        }

        try
        {
            _signInButton.interactable = false;
            _nameText.text = $"Signing in .... ";
            AuthenticationService.Instance.SignedIn += delegate
            {
                Debug.Log("SignedIn OK!");
                _signInButton.interactable = true;
                UpdateProfileNameText();
                _signInGroup.SetActive(false);
                TriggerMainMenuTransitionAnimation();
                m_pressAnyKeyActive = false;
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            _signInButton.interactable = true;
            _nameText.text = $"Sign in failed : {e.ToString()} ";
            Debug.LogException(e);
            throw;
        }
    }

    private void Update()
    {
        CheckLobbyUpdate();
        if (m_pressAnyKeyActive)
        {
            if (Input.anyKey)
            {
                // TriggerMainMenuTransitionAnimation();
                //
                // m_pressAnyKeyActive = false;
            }
        }
    }

    public async void CheckLobbyUpdate()
    {
        if (lobby == null) return;
        if (Time.realtimeSinceStartup >= nextHeartbeat && isHost)
        {
            nextHeartbeat = Time.realtimeSinceStartup + 15;
            /* Keep connection to lobby alive */
            await LobbyService.Instance.SendHeartbeatPingAsync(lobby.Id);
        }

        if (Time.realtimeSinceStartup >= nextLobbyRefresh)
        {
            this.nextLobbyRefresh = Time.realtimeSinceStartup + 2; /* Update after every 2 seconds */
            this.LobbyUpdate();
            this.ReceiveIncomingDetail();
        }
    }
    /* Tashi setup/update PlayerDataObject */
    public async void LobbyUpdate()
    {
        var outgoingSessionDetails = NetworkTransport.OutgoingSessionDetails;

        var updatePlayerOptions = new UpdatePlayerOptions();
        if (outgoingSessionDetails.AddTo(updatePlayerOptions))
        {
            // Debug.Log("= PlayerData outgoingSessionDetails AddTo TRUE so can UpdatePLayerAsync");
            lobby = await LobbyService.Instance.UpdatePlayerAsync(lobby.Id,
                AuthenticationService.Instance.PlayerId,
                updatePlayerOptions);
        }

        if (isHost)
        {
            var updateLobbyOptions = new UpdateLobbyOptions();
            if (outgoingSessionDetails.AddTo(updateLobbyOptions))
            {
                // Debug.Log("= Lobby outgoingSessionDetails AddTo TRUE and Update Lobby Async.");
                lobby = await LobbyService.Instance.UpdateLobbyAsync(lobby.Id, updateLobbyOptions);
            }
        }
    }
    /* Tashi Update/get lobby session details */
    public async void ReceiveIncomingDetail()
    {
        try
        {
            if (NetworkTransport.SessionHasStarted) return;

            // Debug.LogWarning("Receive Incoming Detail");

            lobby = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
            var incomingSessionDetails = IncomingSessionDetails.FromUnityLobby(lobby);

            // This should be replaced with whatever logic you use to determine when a lobby is locked in.
            // if (this._playerCount > 1 && incomingSessionDetails.AddressBook.Count == lobby.Players.Count)
            if (incomingSessionDetails.AddressBook.Count == 2)
            {
                NetworkTransport.UpdateSessionDetails(incomingSessionDetails);
            }

        }
        catch (Exception)
        {
        }
    }

    public async void OnClickHost()
    {
        NetworkManager.Singleton.StartHost();
        AudioManager.Instance.PlaySoundEffect(m_confirmClip);
        LoadingSceneManager.Instance.LoadScene(nextScene);
    }
    public async void OnClickTashiHost()
    {
        NetworkManager.Singleton.StartHost();
        AudioManager.Instance.PlaySoundEffect(m_confirmClip);

        /* Create Lobby */
        int maxPlayerInRoom = 8;
        if (int.TryParse(_maxPlayerInRoomInputField.text, out int rs))
        {
            maxPlayerInRoom = rs;
        }
        else
        {
            maxPlayerInRoom = 8;
        }

        _maxPlayerInRoomInputField.text = maxPlayerInRoom.ToString();
        var lobbyOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
        };
        string lobbyName = this.LobbyName();
        await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayerInRoom, lobbyOptions);
        this.isHost = true;
        /* End Create Lobby */

        LoadingSceneManager.Instance.LoadScene(nextScene);
    }

    public string LobbyName()
    {
        return AuthenticationService.Instance.Profile + "_lobby_" + Random.Range(1, 100);
    }

    public void OnClickJoin()
    {
        AudioManager.Instance.PlaySoundEffect(m_confirmClip);
        StartCoroutine(Join());
    }

    public void OnClickQuit()
    {
        AudioManager.Instance.PlaySoundEffect(m_confirmClip);
        Application.Quit();
    }

    private void ClearAllCharacterData()
    {
        // Clean the all the data of the characters so we can start with a clean slate
        foreach (CharacterDataSO data in m_characterDatas)
        {
            data.EmptyData();
        }
    }

    private void TriggerMainMenuTransitionAnimation()
    {
        m_menuAnimator.SetTrigger(k_enterMenuTriggerAnim);
        AudioManager.Instance.PlaySoundEffect(m_confirmClip);
    }

    // We use a coroutine because the server is the one who makes the load
    // we need to make a fade first before calling the start client
    private IEnumerator Join()
    {
        LoadingFadeEffect.Instance.FadeAll();

        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        NetworkManager.Singleton.StartClient();
    }
}