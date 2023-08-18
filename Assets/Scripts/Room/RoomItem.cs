

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameRoomText;
    [SerializeField] private Button _joinButton;
    private string _id = "";
    private string _code = "";
    Action<string> onJoinClickCallback;

    private void Start()
    {
        _joinButton.onClick.AddListener(JoinClick);
    }

    public void SetData(string id, string code, string name)
    {
        _nameRoomText.text = name;
        _id = id;
        _code = code;
        this.gameObject.SetActive(true);
    }
    
    /* Set Callback when click Join button */
    public void SetJoinClick(Action<string> callback)
    {
        onJoinClickCallback = callback;
    }

    public void JoinClick()
    {
        onJoinClickCallback?.Invoke(_id);
    }
    
}