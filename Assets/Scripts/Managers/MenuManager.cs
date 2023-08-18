using System;
using Unity.Netcode;

using UnityEngine;
using System.Collections;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField]
    private Animator m_menuAnimator;

    [SerializeField]
    private CharacterDataSO[] m_characterDatas;

    [SerializeField]
    private AudioClip m_confirmClip;

    private bool m_pressAnyKeyActive = true;
    private const string k_enterMenuTriggerAnim = "enter_menu";

    [SerializeField]
    private SceneName nextScene = SceneName.CharacterSelection;

    [Header("Sign in")] 
    [SerializeField] private GameObject _signInGroup;
    [SerializeField] private Button _signInButton;
    [SerializeField] private TMP_InputField _nameInputText;
    [SerializeField] private TextMeshProUGUI _nameText;

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
        _nameText.text = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.Profile : "-Not Sign In-";
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
                ListRoomManager.Instance.ListLobbies();
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

    public void OnClickHost()
    {
        NetworkManager.Singleton.StartHost();
        AudioManager.Instance.PlaySoundEffect(m_confirmClip);
        LoadingSceneManager.Instance.LoadScene(nextScene);
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