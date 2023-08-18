using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class ListRoomManager : Singleton<ListRoomManager>
{
    [SerializeField] private Transform _listRoomContentTransform;
    [SerializeField] private RoomItem _roomItemPrefab;
    public List<RoomItem> listRoomItem = new();

    private void Start()
    {
        StartCoroutine(IEGetListLobbies());
    }

    IEnumerator IEGetListLobbies(float delayTime = 3f)
    {
        while (true)
        {
            if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
            {
                ListLobbies();
            }
            yield return new WaitForSeconds(delayTime);
        }
    }

    public async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    /* Just get the lobby's available slots using the filter. */
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);
            Debug.Log("= Get List Lobbies Count : " + queryResponse.Results.Count);
            /* Disative all old lobby item in list */
            foreach (Transform child in _listRoomContentTransform)
            {
                child.gameObject.SetActive(false);
            }
        
            listRoomItem.Clear();
            /* Show every lobby item in list */
            int i = 0;
            foreach (Lobby lobby in queryResponse.Results)
            {
                RoomItem roomItem;
                try
                {
                    roomItem = _listRoomContentTransform.GetChild(i).GetComponent<RoomItem>();
                }
                catch (Exception)
                {
                    roomItem = Instantiate(_roomItemPrefab, _listRoomContentTransform);
                }

                roomItem.SetData(lobby.Id, lobby.LobbyCode, lobby.Name);
                roomItem.SetJoinClick(OnClickJoinLobby);
                listRoomItem.Add(roomItem);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Exception : " + e.ToString());
        }
    }

    public void OnClickJoinLobby(string lobbyId)
    {
        JoinLobbyByLobbyId(lobbyId);
    }

    public async void JoinLobbyByLobbyId(string lobbyId)
    {
        StartCoroutine(Join(lobbyId));
        
    }
    private IEnumerator Join(string id)
    {
        LoadingFadeEffect.Instance.FadeAll();

        yield return new WaitUntil(() => LoadingFadeEffect.s_canLoad);

        NetworkManager.Singleton.StartClient();
        JoinLobby(id);
    }

    private async void JoinLobby(string id)
    {
        Lobby _lobby = await LobbyService.Instance.JoinLobbyByIdAsync(id);
        MenuManager.Instance.lobby = _lobby;
        Debug.Log($"Join lobby Id {_lobby.Id} has code {_lobby.LobbyCode}");
    }
}