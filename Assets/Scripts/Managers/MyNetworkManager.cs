using System;
using Tashi.NetworkTransport;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using UnityEngine;
using Unity.Services.Lobbies.Models;
using UnityEngine.Serialization;

public class MyNetworkManager : SingletonNetworkPersistent<MyNetworkManager>
{
    [Tooltip("Tick check if you are using Tashi Network Transport instead of Netcode Transport")]
    [SerializeField] private bool isTashi;
    private TashiNetworkTransport NetworkTransport => NetworkManager.Singleton.NetworkConfig.NetworkTransport as TashiNetworkTransport;
    [SerializeField] private Lobby _lobby;
    [SerializeField] public string CurrentLobbyId;
    
    [FormerlySerializedAs("isHost")] public bool isLobbyHost = false;
    public float nextHeartbeat;
    public float nextLobbyRefresh;
    public Lobby CurrentLobby
    {
        get {return _lobby; }
        set { _lobby = value;
            CurrentLobbyId = _lobby == null ? null : _lobby.Id;
        }
    }

    private void Update()
    {
        CheckLobbyUpdate();
    }
    public async void CheckLobbyUpdate()
    {
        if (!isTashi) return;
        if (CurrentLobby == null) return;
        if (Time.realtimeSinceStartup >= nextHeartbeat && isLobbyHost)
        {
            nextHeartbeat = Time.realtimeSinceStartup + 15;
            /* Keep connection to lobby alive */
            await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
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
            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                updatePlayerOptions);
        }

        if (isLobbyHost)
        {
            var updateLobbyOptions = new UpdateLobbyOptions();
            if (outgoingSessionDetails.AddTo(updateLobbyOptions))
            {
                // Debug.Log("= Lobby outgoingSessionDetails AddTo TRUE and Update Lobby Async.");
                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateLobbyOptions);
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

            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            var incomingSessionDetails = IncomingSessionDetails.FromUnityLobby(CurrentLobby);

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

    public async void ExitCurrentLobby()
    {
        Debug.Log("ExitCurrentLobby");
        if (CurrentLobby == null) return;
        /* Remove this player out of this lobby */
        if (CurrentLobby.Players.Count > 1)
        {
            await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
        }
        else
        {
            await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
        }

        isLobbyHost = false;
        CurrentLobby = null;
        NetworkManager.Singleton.Shutdown();
    }
    public void OnApplicationQuit()
    {
        ExitCurrentLobby();
    }
}