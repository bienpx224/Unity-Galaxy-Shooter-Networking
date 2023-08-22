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
                    Transform obj = _listRoomContentTransform.GetChild(i);
                    if (obj is not null)
                    {
                        roomItem = obj.GetComponent<RoomItem>();
                    }
                    else
                    {
                        Debug.Log("Ko tim thay item co san trong GetChild()");
                        roomItem = Instantiate(_roomItemPrefab, _listRoomContentTransform);
                    }
                }
                catch (Exception)
                {
                    Debug.Log("Ko tim thay item co san trong GetChild()");
                    roomItem = Instantiate(_roomItemPrefab, _listRoomContentTransform);
                }

                try
                {
                    roomItem.SetData(lobby.Id, lobby.LobbyCode, lobby.Name);
                    roomItem.SetJoinClick(OnClickJoinLobby);
                    listRoomItem.Add(roomItem);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("List Lobby : Exception at foreach List Room : " + e.ToString());
                }

                i++;
            }
        }
        catch (Exception)
        {
            // Debug.LogError("Exception : " + e.ToString());
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
        Lobby _lobby = null;
        try
        {
            _lobby = await LobbyService.Instance.JoinLobbyByIdAsync(id);
            Debug.Log("Joined lobby : " + _lobby.Id + " and Name : " + _lobby.Name);
        }
        catch (Exception e)
        {
            Debug.LogError("Join Lobby Error: " + e.ToString());
        }
        try
        {
            MenuManager.Instance.lobby = _lobby;
            // LoadingSceneManager.Instance.LoadScene(SceneName.CharacterSelection, false);
        }
        catch (Exception e)
        {
            Debug.LogError("Join Lobby Set data MenuManager Error: " + e.ToString());
        }
    }
}