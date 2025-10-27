using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;

public enum CameraAngle {
    menu = 0,
    whiteTeam = 1,
    blackTeam = 2
}

public class GameUI : MonoBehaviour
{
    public static GameUI Instance {set; get;}

    public Server server;
    public Client client;

    [SerializeField] private Animator menuAnimator;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private GameObject[] cameraAngles;

    public Action<bool> SetLocalGame;

    private void Awake() {
        Instance = this;
        RegisterEvents();
    }

    //cameras

    public void ChangeCamera(CameraAngle index) {
        for (int i = 0; i < cameraAngles.Length; i++) 
            cameraAngles[i].SetActive(false);

        cameraAngles[(int)index].SetActive(true);
        
    }

    // buttons

    // connects all the wifi stuff with the router so you can play with others
    public void OnLocalGameButton() {
        menuAnimator.SetTrigger("InGameMenu");
        SetLocalGame?.Invoke(true);
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
    }

    public void OnOnlineGameButton() {
        menuAnimator.SetTrigger("OnlineMenu");
    }

    public void onOnlineHostButton() {
        SetLocalGame?.Invoke(false);
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
        menuAnimator.SetTrigger("HostMenu");

    }

    public void OnOnlineConnectButton() {
        SetLocalGame?.Invoke(false);
        client.Init(addressInput.text, 8007);
    }

    public void OnOnlineBackButton() {
        menuAnimator.SetTrigger("StartMenu");
    }

    public void OnHostBackButton() {
        server.Shutdown();
        client.Shutdown();
        menuAnimator.SetTrigger("OnlineMenu");
    }

    public void OnLeaveFromGameMenu() {
        ChangeCamera(CameraAngle.menu);
        menuAnimator.SetTrigger("StartMenu");
    }

    private void RegisterEvents() {
        NetUtility.C_START_GAME += OnStartGameClient;
    }

    private void UnRegisterEvents() {
        NetUtility.C_START_GAME -= OnStartGameClient;
    }

    private void OnStartGameClient(NetMessage obj) {
        menuAnimator.SetTrigger("InGameMenu");
    }

}
